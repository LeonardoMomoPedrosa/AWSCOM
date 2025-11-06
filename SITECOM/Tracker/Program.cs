using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Tracker.Models;
using Tracker.Services;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
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
    var correiosKey = config["Correios:Key"] ?? string.Empty;
    var correiosCartaPostal = config["Correios:CartaPostal"] ?? string.Empty;
    var emailBaseUrl = config["EmailService:BaseUrl"] ?? "https://lion.aquanimal.com.br/ajax/OrderStatusAjaxHandler.ashx";

    dynamoService = new DynamoDBService(tableName, region);
    correiosService = new CorreiosService(correiosKey, correiosCartaPostal);
    sqlService = new SqlServerService(connectionString);
    emailService = new EmailService(emailBaseUrl);
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

            // 2.3.2.1 - Chamar serviço de email
            Console.WriteLine($"      📧 Agendando email...");
            var emailSuccess = await emailService.ScheduleEmailAsync(int.Parse(record.IdPedido), novoJson);

            if (!emailSuccess)
            {
                Console.WriteLine($"      ⚠️  Falha ao agendar email, mas continuando com atualização...");
            }
            else
            {
                Console.WriteLine($"      ✅ Email agendado com sucesso");
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
            var trackingRecord = new TrackingRecord
            {
                IdPedido = record.OrderId.ToString(),
                TipoEnvio = "C",
                CodRastreamento = record.Track,
                RastreamentoJson = rastreamentoJson,
                DataCriacao = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            await dynamoService.PutItemAsync(trackingRecord);
            Console.WriteLine($"      ✅ Inserido no DynamoDB");

            // 6.1.3 - Atualizar status no SQL Server
            Console.WriteLine($"      📝 Atualizando status no SQL Server...");
            await sqlService.UpdateTrackingStatusAsync(record.OrderId);
            Console.WriteLine($"      ✅ Status atualizado");

            // 6.1.4 - Chamar serviço de email
            Console.WriteLine($"      📧 Agendando email...");
            var emailSuccess = await emailService.ScheduleEmailAsync(record.OrderId, rastreamentoJson);

            if (!emailSuccess)
            {
                Console.WriteLine($"      ⚠️  Falha ao agendar email, mas registro foi criado");
            }
            else
            {
                Console.WriteLine($"      ✅ Email agendado com sucesso");
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

