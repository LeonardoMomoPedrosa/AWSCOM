using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Data.SqlClient;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaAviseme;

public class Function
{
    private readonly string _secretArn;
    private readonly AmazonSecretsManagerClient _secretsManagerClient;

    public Function()
    {
        // Configurar cliente do Secrets Manager com timeout
        var config = new AmazonSecretsManagerConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.USEast1,
            Timeout = TimeSpan.FromSeconds(5) // Timeout de 5 segundos
        };
        _secretsManagerClient = new AmazonSecretsManagerClient(config);
        
        // Buscar SECRET_ARN da variável de ambiente, com fallback para valor padrão
        var envSecretArn = Environment.GetEnvironmentVariable("SECRET_ARN");
        _secretArn = envSecretArn ?? "arn:aws:secretsmanager:us-east-1:615283740315:secret:prod/sqlserver/ecom-QABqVU";
        
    }

    public async Task<string> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var logger = context.Logger;
        logger.LogInformation("=== INICIANDO LAMBDA AVISEME ===");
        logger.LogInformation($"Request ID: {context.AwsRequestId}");
        logger.LogInformation($"Function Name: {context.FunctionName}");
        logger.LogInformation($"Function Version: {context.FunctionVersion}");
        logger.LogInformation($"Remaining Time: {context.RemainingTime.TotalMilliseconds} ms");
        logger.LogInformation($"Memory Limit: {context.MemoryLimitInMB} MB");
        logger.LogInformation($"SECRET_ARN: {_secretArn}");
        
        var startTime = DateTime.UtcNow;

        try
        {
            logger.LogInformation("[STEP 1] Buscando string de conexão do Secrets Manager...");
            var connectionString = await GetConnectionStringAsync(logger);
            logger.LogInformation("[STEP 1] String de conexão obtida com sucesso");
            
            logger.LogInformation("[STEP 2] Iniciando execução da query de aviseme...");
            var results = await ExecuteAvisemeQueryAsync(connectionString, logger);
            
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            logger.LogInformation($"=== EXECUÇÃO CONCLUÍDA COM SUCESSO ===");
            logger.LogInformation($"Total de registros encontrados: {results.Count}");
            logger.LogInformation($"Tempo total de execução: {duration:F2} segundos");
            
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Consulta executada com sucesso",
                totalRecords = results.Count,
                data = results,
                durationSeconds = duration,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            logger.LogError($"=== ERRO NA EXECUÇÃO ===");
            logger.LogError($"Tempo até erro: {duration:F2} segundos");
            logger.LogError($"Erro: {ex.Message}");
            logger.LogError($"Tipo do erro: {ex.GetType().Name}");
            logger.LogError($"Stack trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                logger.LogError($"Inner exception: {ex.InnerException.Message}");
            }
            
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = ex.Message,
                errorType = ex.GetType().Name,
                durationSeconds = duration,
                timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task<string> GetConnectionStringAsync(ILambdaLogger logger)
    {
        try
        {
            logger.LogInformation($"🔍 Buscando string de conexão do Secrets Manager");
            logger.LogInformation($"Secret ARN: {_secretArn}");
            logger.LogInformation($"📍 Região: {_secretsManagerClient.Config.RegionEndpoint.DisplayName}");
            logger.LogInformation($"⏱️ Timeout configurado: {_secretsManagerClient.Config.Timeout?.TotalSeconds ?? 0}s");
            
            var requestStartTime = DateTime.UtcNow;
            var request = new GetSecretValueRequest
            {
                SecretId = _secretArn
            };

            logger.LogInformation($"📤 Enviando requisição para Secrets Manager...");
            logger.LogInformation($"⏰ Tempo restante antes da requisição: {DateTime.UtcNow - requestStartTime}");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _secretsManagerClient.GetSecretValueAsync(request, cts.Token);
            
            var requestDuration = (DateTime.UtcNow - requestStartTime).TotalSeconds;
            logger.LogInformation($"✅ Resposta recebida do Secrets Manager em {requestDuration:F2}s");
            
            if (string.IsNullOrEmpty(response.SecretString))
            {
                throw new Exception("String de conexão não encontrada no Secrets Manager");
            }

            logger.LogInformation($"📄 Tamanho da resposta: {response.SecretString.Length} caracteres");
            logger.LogInformation($"📝 Primeiros 50 caracteres: {response.SecretString.Substring(0, Math.Min(50, response.SecretString.Length))}...");

            // O Secret Manager retorna um JSON, precisamos parsear
            logger.LogInformation("🔄 Parseando JSON do secret...");
            
            try
            {
                var secretJson = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
                var keys = secretJson?.Keys?.ToList() ?? new List<string>();
                logger.LogInformation($"📋 JSON parseado com sucesso. Chaves encontradas: {string.Join(", ", keys)}");
                
                // Procurar pela chave de connection string
                if (secretJson != null && secretJson.ContainsKey("lambda_ecom_db"))
                {
                    var connectionString = secretJson["lambda_ecom_db"];
                    logger.LogInformation($"✅ Chave 'lambda_ecom_db' encontrada");
                    logger.LogInformation($"📝 Connection string (primeiros 30 chars): {connectionString.Substring(0, Math.Min(30, connectionString.Length))}...");
                    return connectionString;
                }
                else
                {
                    logger.LogWarning($"⚠️ Chave 'lambda_ecom_db' não encontrada. Chaves disponíveis: {string.Join(", ", keys)}");
                    return response.SecretString;
                }
            }
            catch (JsonException jsonEx)
            {
                logger.LogInformation($"ℹ️ Valor não é JSON válido ({jsonEx.Message}), usando diretamente como connection string");
                return response.SecretString;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"❌ Erro ao buscar string de conexão: {ex.Message}");
            logger.LogError($"❌ Tipo: {ex.GetType().Name}");
            throw;
        }
    }

    private async Task<List<AvisemeRecord>> ExecuteAvisemeQueryAsync(string connectionString, ILambdaLogger logger)
    {
        var results = new List<AvisemeRecord>();
        
        try
        {
            logger.LogInformation("🔌 Tentando conectar ao banco de dados...");
            logger.LogInformation($"📍 Connection string (primeiros 50 chars): {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");
            
            var connectStartTime = DateTime.UtcNow;
            using var connection = new SqlConnection(connectionString);
            logger.LogInformation("📤 Abrindo conexão...");
            await connection.OpenAsync();
            var connectDuration = (DateTime.UtcNow - connectStartTime).TotalSeconds;
            logger.LogInformation($"✅ Conexão aberta com sucesso em {connectDuration:F2}s");
            logger.LogInformation($"🖥️ Servidor: {connection.DataSource}");
            logger.LogInformation($"🗄️ Database: {connection.Database}");
            logger.LogInformation($"👤 Server Version: {connection.ServerVersion}");
            
            const string query = @"
                SELECT
                    a.user_id,
                    p.estoque,
                    u.nome,
                    u.email,
                    a.product_id,
                    p.nome_new as nome_prod
                FROM tbavise a 
                LEFT JOIN tbProdutos p ON a.product_id = p.pkid
                LEFT JOIN tbUsuarios u ON a.user_id = u.id";
            
            logger.LogInformation("📝 Executando query SQL...");
            var queryStartTime = DateTime.UtcNow;
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            var queryDuration = (DateTime.UtcNow - queryStartTime).TotalSeconds;
            logger.LogInformation($"✅ Query executada em {queryDuration:F2}s");
            
            logger.LogInformation("📊 Lendo resultados...");
            var recordCount = 0;
            while (await reader.ReadAsync())
            {
                var estoqueOrdinal = reader.GetOrdinal("estoque");
                var estoqueValue = reader.IsDBNull(estoqueOrdinal) ? 0 : 
                                   Convert.ToInt32(reader.GetValue(estoqueOrdinal));
                
                var record = new AvisemeRecord
                {
                    UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("user_id")),
                    Estoque = estoqueValue,
                    Nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? string.Empty : reader.GetString(reader.GetOrdinal("nome")),
                    Email = reader.IsDBNull(reader.GetOrdinal("email")) ? string.Empty : reader.GetString(reader.GetOrdinal("email")),
                    ProductId = reader.IsDBNull(reader.GetOrdinal("product_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("product_id")),
                    NomeProd = reader.IsDBNull(reader.GetOrdinal("nome_prod")) ? string.Empty : reader.GetString(reader.GetOrdinal("nome_prod"))
                };
                
                results.Add(record);
                recordCount++;
                
                // Log apenas dos primeiros 5 registros para não poluir logs
                if (recordCount <= 5)
                {
                    logger.LogInformation($"📄 [{recordCount}] User: {record.Nome} ({record.Email}) | Produto: {record.NomeProd} | Estoque: {record.Estoque}");
                }
            }
            
            logger.LogInformation("🔌 Fechando conexão...");
            await connection.CloseAsync();
            
            logger.LogInformation($"✅ Query executada com sucesso! Total de registros: {results.Count}");
        }
        catch (Exception ex)
        {
            logger.LogError($"❌ Erro ao executar query: {ex.Message}");
            logger.LogError($"❌ Tipo: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                logger.LogError($"❌ Inner Exception: {ex.InnerException.Message}");
            }
            throw;
        }
        
        return results;
    }
    
    private class AvisemeRecord
    {
        public int UserId { get; set; }
        public int Estoque { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string NomeProd { get; set; } = string.Empty;
    }
}

