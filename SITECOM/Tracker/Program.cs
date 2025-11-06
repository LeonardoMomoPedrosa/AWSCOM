using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Tracker.Models;
using Tracker.Services;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables() // Permite usar variáveis de ambiente (ex: Correios__Key)
    .Build();

Console.WriteLine("===========================================");
Console.WriteLine("=== TRACKER JOB ===");
Console.WriteLine($"Iniciado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine("===========================================");

DynamoDBService? dynamoService = null;
CorreiosService? correiosService = null;
SqlServerService? sqlService = null;
EmailService? emailService = null;

try
{
    // 1. Obter connection string do SQL Server
    Console.WriteLine("\n[STEP 1] Obtendo connection string do SQL Server...");
    var connectionString = config["UseSecretsManager"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        ? await GetConnectionStringFromSecretsManager(config["SecretArn"]!)
        : config["ConnectionString"]!;

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new Exception("Connection string não configurada! Configure em appsettings.json");
    }
    Console.WriteLine("✅ Connection string obtida");

    // 2. Inicializar serviços
    Console.WriteLine("\n[STEP 2] Inicializando serviços...");
    var tableName = config["DynamoDB:TableName"] ?? "tracker-pedidos";
    var region = config["DynamoDB:Region"] ?? "us-east-1";
    var correiosUsuario = config["Correios:Usuario"] ?? string.Empty;
    var correiosSecretKey = config["Correios:SecretKey"] ?? string.Empty;
    var correiosCartaPostal = config["Correios:CartaPostal"] ?? string.Empty;

    // Log de debug (sem mostrar valores completos)
    Console.WriteLine($"   👤 Usuário dos Correios: {(string.IsNullOrWhiteSpace(correiosUsuario) ? "NÃO CONFIGURADO" : correiosUsuario)}");
    Console.WriteLine($"   🔑 Secret Key: {(string.IsNullOrWhiteSpace(correiosSecretKey) ? "NÃO CONFIGURADA" : "***" + correiosSecretKey.Substring(Math.Max(0, correiosSecretKey.Length - 4)))}");
    Console.WriteLine($"   📮 Cartão Postal: {correiosCartaPostal}");

    if (string.IsNullOrWhiteSpace(correiosUsuario))
    {
        throw new Exception("Usuário dos Correios não configurado! Configure 'Correios:Usuario' no appsettings.json ou via variável de ambiente 'Correios__Usuario'");
    }

    if (string.IsNullOrWhiteSpace(correiosSecretKey))
    {
        throw new Exception("Secret Key dos Correios não configurada! Configure 'Correios:SecretKey' no appsettings.json ou via variável de ambiente 'Correios__SecretKey'");
    }

    if (string.IsNullOrWhiteSpace(correiosCartaPostal))
    {
        throw new Exception("Cartão Postal dos Correios não configurado! Configure 'Correios:CartaPostal' no appsettings.json");
    }

    var sesFromEmail = config["SES:FromEmail"] ?? "aquanimal@aquanimal.com.br";
    var sesBccEmail = config["SES:CcEmail"] ?? string.Empty; // CCO (cópia oculta)
    var sesRegion = config["SES:Region"] ?? "us-east-1";

    dynamoService = new DynamoDBService(tableName, region);
    correiosService = new CorreiosService(correiosUsuario, correiosSecretKey, correiosCartaPostal);
    sqlService = new SqlServerService(connectionString);
    emailService = new EmailService(sesFromEmail, sesBccEmail, sesRegion);
    Console.WriteLine("✅ Serviços inicializados");

    // 3. Processar registros existentes no DynamoDB
    Console.WriteLine("\n[STEP 3] Processando registros existentes no DynamoDB...");
    await ProcessExistingRecordsAsync(dynamoService, correiosService, emailService);
    Console.WriteLine("✅ Processamento de registros existentes concluído");

    // 4. Inserir novos rastreamentos
    Console.WriteLine("\n[STEP 4] Inserindo novos rastreamentos...");
    await ProcessNewTrackingRecordsAsync(dynamoService, correiosService, sqlService, emailService);
    Console.WriteLine("✅ Inserção de novos rastreamentos concluída");

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
finally
{
    dynamoService?.Dispose();
    correiosService?.Dispose();
    emailService?.Dispose();
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

static async Task ProcessExistingRecordsAsync(
    DynamoDBService dynamoService,
    CorreiosService correiosService,
    EmailService emailService)
{
    var records = await dynamoService.ScanAllItemsAsync();
    Console.WriteLine($"   📊 Total de registros encontrados: {records.Count}");

    if (records.Count == 0)
    {
        Console.WriteLine("   ℹ️  Nenhum registro para processar");
        return;
    }

    var processedCount = 0;
    var deletedCount = 0;
    var updatedCount = 0;
    var errorCount = 0;

    foreach (var record in records)
    {
        try
        {
            processedCount++;
            Console.WriteLine($"\n   [{processedCount}/{records.Count}] Processando pedido {record.IdPedido}...");

            // 2.1 - Verificar se entrega foi concluída
            var isCompleted = TrackingHelper.IsDeliveryCompleted(record.RastreamentoJson);

            if (isCompleted)
            {
                // 2.2 - Remover do DynamoDB
                Console.WriteLine($"      ✅ Entrega concluída. Removendo do DynamoDB...");
                await dynamoService.DeleteItemAsync(record.IdPedido);
                deletedCount++;
                continue;
            }

            // 2.3 - Se não concluída, consultar API dos Correios novamente
            if (record.TipoEnvio != "C")
            {
                Console.WriteLine($"      ⚠️  Tipo de envio '{record.TipoEnvio}' não suportado (apenas 'C' - Correios)");
                continue;
            }

            if (string.IsNullOrEmpty(record.CodRastreamento))
            {
                Console.WriteLine($"      ⚠️  Código de rastreamento vazio. Pulando...");
                continue;
            }

            Console.WriteLine($"      🔍 Consultando API dos Correios para código {record.CodRastreamento}...");
            var novoRastreamento = await correiosService.GetRastreamentoAsync(record.CodRastreamento);
            var novoJson = TrackingHelper.SerializeRastreamento(novoRastreamento);

            // 2.3.2 - Verificar se houve mudança
            if (!TrackingHelper.HasTrackingChanged(record.RastreamentoJson, novoJson))
            {
                Console.WriteLine($"      ℹ️  Nenhuma mudança no rastreamento");
                continue;
            }

            Console.WriteLine($"      📝 Rastreamento atualizado detectado");

            // 2.3.2.1 - Enviar email
            Console.WriteLine($"      📧 Enviando email para {record.Email}...");
            var emailSuccess = await emailService.SendTrackingEmailAsync(record.Email, record.Nome, novoJson);

            if (!emailSuccess)
            {
                Console.WriteLine($"      ⚠️  Falha ao enviar email, mas continuando com atualização...");
            }
            else
            {
                Console.WriteLine($"      ✅ Email enviado com sucesso");
            }

            // 2.3.2.2 - Atualizar DynamoDB
            await dynamoService.UpdateItemAsync(record.IdPedido, novoJson);
            Console.WriteLine($"      ✅ DynamoDB atualizado");
            updatedCount++;
        }
        catch (Exception ex)
        {
            errorCount++;
            Console.WriteLine($"      ❌ Erro ao processar pedido {record.IdPedido}: {ex.Message}");
            // Continua processando os próximos registros
        }
    }

    Console.WriteLine($"\n   📊 Resumo:");
    Console.WriteLine($"      - Processados: {processedCount}");
    Console.WriteLine($"      - Removidos (entregues): {deletedCount}");
    Console.WriteLine($"      - Atualizados: {updatedCount}");
    Console.WriteLine($"      - Erros: {errorCount}");
}

static async Task ProcessNewTrackingRecordsAsync(
    DynamoDBService dynamoService,
    CorreiosService correiosService,
    SqlServerService sqlService,
    EmailService emailService)
{
    // 3 - Consultar novos rastreamentos no SQL Server
    Console.WriteLine("   🔍 Consultando novos rastreamentos no SQL Server...");
    var newRecords = await sqlService.GetNewTrackingRecordsAsync();
    Console.WriteLine($"   📊 Total de novos registros encontrados: {newRecords.Count}");

    if (newRecords.Count == 0)
    {
        Console.WriteLine("   ℹ️  Nenhum novo registro para processar");
        return;
    }

    // Filtrar apenas via = "C" (Correios)
    var correiosRecords = newRecords.Where(r => r.Via == "C" && !string.IsNullOrEmpty(r.Track)).ToList();
    Console.WriteLine($"   📦 Registros via Correios: {correiosRecords.Count}");

    if (correiosRecords.Count == 0)
    {
        Console.WriteLine("   ℹ️  Nenhum registro via Correios para processar");
        return;
    }

    var processedCount = 0;
    var successCount = 0;
    var errorCount = 0;

    foreach (var record in correiosRecords)
    {
        try
        {
            processedCount++;
            Console.WriteLine($"\n   [{processedCount}/{correiosRecords.Count}] Processando OrderId {record.OrderId}, Track: {record.Track}...");

            // 6.1.1 - Chamar API dos Correios
            Console.WriteLine($"      🔍 Consultando API dos Correios...");
            var rastreamento = await correiosService.GetRastreamentoAsync(record.Track);
            var rastreamentoJson = TrackingHelper.SerializeRastreamento(rastreamento);

            // 6.1.2 - Inserir no DynamoDB
            Console.WriteLine($"      💾 Inserindo no DynamoDB...");
            Console.WriteLine($"      👤 Cliente: {record.Nome} ({record.Email})");
            var trackingRecord = new TrackingRecord
            {
                IdPedido = record.OrderId.ToString(),
                TipoEnvio = "C",
                CodRastreamento = record.Track,
                RastreamentoJson = rastreamentoJson,
                DataCriacao = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Email = record.Email,
                Nome = record.Nome
            };

            await dynamoService.PutItemAsync(trackingRecord);
            Console.WriteLine($"      ✅ Inserido no DynamoDB");

            // 6.1.3 - Atualizar status no SQL Server
            Console.WriteLine($"      📝 Atualizando status no SQL Server...");
            try
            {
                await sqlService.UpdateTrackingStatusAsync(record.OrderId);
                Console.WriteLine($"      ✅ Status atualizado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️  Erro ao atualizar status no SQL Server: {ex.Message}");
                // Continua mesmo se falhar a atualização do status
            }

            // 6.1.4 - Enviar email (sempre enviar quando inserir novo registro)
            Console.WriteLine($"      📧 Enviando email para {record.Email}...");
            var emailSuccess = await emailService.SendTrackingEmailAsync(record.Email, record.Nome, rastreamentoJson);

            if (!emailSuccess)
            {
                Console.WriteLine($"      ⚠️  Falha ao enviar email, mas registro foi criado no DynamoDB");
            }
            else
            {
                Console.WriteLine($"      ✅ Email enviado com sucesso para {record.Email}");
            }

            successCount++;
        }
        catch (Exception ex)
        {
            errorCount++;
            Console.WriteLine($"      ❌ Erro ao processar OrderId {record.OrderId}: {ex.Message}");
            // Continua processando os próximos registros
        }
    }

    Console.WriteLine($"\n   📊 Resumo:");
    Console.WriteLine($"      - Processados: {processedCount}");
    Console.WriteLine($"      - Sucessos: {successCount}");
    Console.WriteLine($"      - Erros: {errorCount}");
}

