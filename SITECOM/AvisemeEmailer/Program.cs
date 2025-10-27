using Microsoft.Data.SqlClient;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

Console.WriteLine("===========================================");
Console.WriteLine("=== AVISEME EMAIL SENDER ===");
Console.WriteLine($"Iniciado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine("===========================================");

try
{
    // 1. Obter connection string
    Console.WriteLine("\n[STEP 1] Obtendo connection string...");
    
    var connectionString = config["UseSecretsManager"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        ? await GetConnectionStringFromSecretsManager(config["SecretArn"]!)
        : config["ConnectionString"]!;
    
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new Exception("Connection string não configurada! Configure em appsettings.json");
    }
    
    Console.WriteLine("✅ Connection string obtida");

    // 2. Consultar banco
    Console.WriteLine("\n[STEP 2] Consultando banco de dados...");
    var records = await GetAvisemeRecords(connectionString);
    Console.WriteLine($"✅ Total de registros encontrados: {records.Count}");
    
    // 3. Filtrar estoque = 1
    Console.WriteLine("\n[STEP 3] Filtrando registros com estoque disponível...");
    var estoqueRecords = records.Where(r => r.Estoque == 1).ToList();
    Console.WriteLine($"✅ Registros com estoque = 1: {estoqueRecords.Count}");
    
    if (estoqueRecords.Count == 0)
    {
        Console.WriteLine("\nℹ️  Nenhum produto em estoque para notificar. Finalizando.");
        Console.WriteLine("===========================================");
        return 0;
    }

    // Mostrar lista de produtos
    Console.WriteLine("\n📦 Produtos a serem notificados:");
    foreach (var record in estoqueRecords)
    {
        Console.WriteLine($"   - {record.NomeProd} → {record.Nome} ({record.Email})");
    }

    // 4. Enviar emails
    Console.WriteLine("\n[STEP 4] Enviando emails via AWS SES...");
    await SendEmails(estoqueRecords, config);
    Console.WriteLine($"✅ {estoqueRecords.Count} email(s) enviado(s) com sucesso");

    // 5. Deletar registros
    Console.WriteLine("\n[STEP 5] Removendo registros do banco de dados...");
    await DeleteRecords(connectionString);
    Console.WriteLine("✅ Registros removidos do banco");

    Console.WriteLine("\n===========================================");
    Console.WriteLine("=== CONCLUÍDO COM SUCESSO ===");
    Console.WriteLine($"Finalizado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine("===========================================");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine("\n===========================================");
    Console.WriteLine("❌ ERRO NA EXECUÇÃO");
    Console.WriteLine("===========================================");
    Console.WriteLine($"Erro: {ex.Message}");
    Console.WriteLine($"Tipo: {ex.GetType().Name}");
    Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
    }
    
    Console.WriteLine("===========================================");
    return 1;
}

// ===== FUNÇÕES =====

static async Task<string> GetConnectionStringFromSecretsManager(string secretArn)
{
    Console.WriteLine($"   📍 ARN: {secretArn}");
    Console.WriteLine("   🔍 Buscando no AWS Secrets Manager...");
    
    var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.USEast1);
    var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretArn });
    
    var secret = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
    
    if (secret == null || !secret.ContainsKey("lambda_ecom_db"))
    {
        throw new Exception("Chave 'lambda_ecom_db' não encontrada no secret");
    }
    
    Console.WriteLine("   ✅ Secret obtido com sucesso");
    return secret["lambda_ecom_db"];
}

static async Task<List<AvisemeRecord>> GetAvisemeRecords(string connectionString)
{
    var results = new List<AvisemeRecord>();
    
    using var connection = new SqlConnection(connectionString);
    Console.WriteLine("   🔌 Conectando ao banco...");
    await connection.OpenAsync();
    Console.WriteLine($"   ✅ Conectado: {connection.Database}");
    
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
    
    using var command = new SqlCommand(query, connection);
    Console.WriteLine("   📝 Executando query...");
    using var reader = await command.ExecuteReaderAsync();
    
    while (await reader.ReadAsync())
    {
        results.Add(new AvisemeRecord
        {
            UserId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            Estoque = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
            Nome = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
            ProductId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            NomeProd = reader.IsDBNull(5) ? "" : reader.GetString(5)
        });
    }
    
    return results;
}

static async Task SendEmails(List<AvisemeRecord> records, IConfiguration config)
{
    var fromEmail = config["SES:FromEmail"]!;
    var ccEmail = config["SES:CcEmail"]!;
    var region = Amazon.RegionEndpoint.GetBySystemName(config["SES:Region"]!);
    
    Console.WriteLine($"   📧 From: {fromEmail}");
    Console.WriteLine($"   📧 CC: {ccEmail}");
    Console.WriteLine($"   📍 Region: {region.DisplayName}");
    
    using var sesClient = new AmazonSimpleEmailServiceClient(region);
    
    var successCount = 0;
    var failCount = 0;
    
    foreach (var record in records)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(record.Email))
            {
                Console.WriteLine($"   ⚠️  Email vazio para {record.Nome}. Pulando...");
                failCount++;
                continue;
            }
            
            var htmlBody = $@"
                <html>
                <body>
                    <h2>Produto Disponível em Estoque</h2>
                    <p>Olá <strong>{record.Nome}</strong>!</p>
                    <p>Temos o prazer de informar que o produto <strong>{record.NomeProd}</strong> está novamente disponível em nosso estoque.</p>
                    <p>Não perca esta oportunidade!</p>
                    <p>Atenciosamente,<br>Equipe Aquanimal</p>
                    <p><a href='https://aquanimal.com.br'>aquanimal.com.br</a></p>
                    <hr>
                </body>
                </html>";
            
            var request = new SendEmailRequest
            {
                Source = fromEmail,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { record.Email },
                    CcAddresses = new List<string> { ccEmail }
                },
                Message = new Message
                {
                    Subject = new Content($"Produto Disponível: {record.NomeProd}"),
                    Body = new Body
                    {
                        Html = new Content { Charset = "UTF-8", Data = htmlBody }
                    }
                }
            };
            
            var response = await sesClient.SendEmailAsync(request);
            Console.WriteLine($"   ✅ Email enviado para {record.Email} (MessageId: {response.MessageId})");
            successCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erro ao enviar para {record.Email}: {ex.Message}");
            failCount++;
            throw; // Propaga o erro para não deletar registros
        }
    }
    
    Console.WriteLine($"\n   📊 Resumo: {successCount} enviados, {failCount} falhas");
}

static async Task DeleteRecords(string connectionString)
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    
    const string deleteQuery = @"
        DELETE a
        FROM tbAvise a
        JOIN tbProdutos p ON p.PKId = a.product_id
        WHERE p.estoque = 1";
    
    using var command = new SqlCommand(deleteQuery, connection);
    var rowsAffected = await command.ExecuteNonQueryAsync();
    Console.WriteLine($"   🗑️  {rowsAffected} registro(s) removido(s)");
}

record AvisemeRecord
{
    public int UserId { get; set; }
    public int Estoque { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public int ProductId { get; set; }
    public string NomeProd { get; set; } = "";
}

