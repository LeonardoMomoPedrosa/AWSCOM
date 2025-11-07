using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Personalize.Models;
using Personalize.Services;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine("===========================================");
Console.WriteLine("=== PERSONALIZE JOB ===");
Console.WriteLine($"Iniciado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine("===========================================");

DynamoDBService? dynamoService = null;
SqlServerService? sqlService = null;
PersonalizationService? personalizationService = null;
ExecutionStateService? stateService = null;

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
    var tableName = config["DynamoDB:TableName"] ?? "dynamo-personalize";
    var region = config["DynamoDB:Region"] ?? "us-east-1";
    var topRecommendations = int.Parse(config["Personalization:TopRecommendations"] ?? "5");
    var timeDecayHalfLifeDays = double.Parse(config["Personalization:TimeDecayHalfLifeDays"] ?? "30");
    var safetyMarginMinutes = int.Parse(config["Personalization:SafetyMarginMinutes"] ?? "60");
    var stateFilePath = config["Personalization:LastProcessedDateFile"] ?? "last_processed_date.txt";
    var isFirstRun = config["Personalization:FirstRun"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    var excludedProductIds = config.GetSection("Personalization:ExcludedProductIds").Get<int[]>() ?? Array.Empty<int>();
    var excludedProductIdsSet = excludedProductIds.Length > 0
        ? excludedProductIds.ToHashSet()
        : new HashSet<int>();

    dynamoService = new DynamoDBService(tableName, region);
    sqlService = new SqlServerService(connectionString);
    personalizationService = new PersonalizationService(topRecommendations, timeDecayHalfLifeDays);
    stateService = new ExecutionStateService(stateFilePath);

    Console.WriteLine($"   📊 Tabela DynamoDB: {tableName}");
    Console.WriteLine($"   🌍 Região: {region}");
    Console.WriteLine($"   🎯 Top recomendações: {topRecommendations}");
    Console.WriteLine($"   ⏱️  Meia-vida decaimento temporal: {timeDecayHalfLifeDays} dias");
    if (excludedProductIdsSet.Count > 0)
    {
        Console.WriteLine($"   🚫 Produtos excluídos (IDs): {string.Join(", ", excludedProductIdsSet)}");
    }
    Console.WriteLine("✅ Serviços inicializados");

    // 3. Determinar data inicial para busca
    Console.WriteLine("\n[STEP 3] Determinando período de processamento...");
    DateTime? fromDate = null;

    if (isFirstRun)
    {
        Console.WriteLine("   ℹ️  Primeira execução: processando todo o histórico");
    }
    else
    {
        var lastProcessedDate = await stateService.GetLastProcessedDateAsync();
        if (lastProcessedDate.HasValue)
        {
            // Processar desde a última data processada, com pequena margem de segurança (minutos)
            // para cobrir possíveis atrasos em inserções de dados no banco
            fromDate = lastProcessedDate.Value.AddMinutes(-safetyMarginMinutes);
            Console.WriteLine($"   📅 Última execução: {lastProcessedDate.Value:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   📅 Margem de segurança: {safetyMarginMinutes} minutos");
            Console.WriteLine($"   📅 Processando desde: {fromDate.Value:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            Console.WriteLine("   ⚠️  Arquivo de estado não encontrado. Processando todo o histórico.");
        }
    }

    // 4. Buscar dados de compras
    Console.WriteLine("\n[STEP 4] Buscando dados de compras do SQL Server...");
    Console.WriteLine("   🔍 Filtros: status = 'V' (enviados)");
    if (fromDate.HasValue)
    {
        Console.WriteLine($"   🔍 Filtro incremental: COALESCE(dataMdSt, data) >= {fromDate.Value:yyyy-MM-dd HH:mm:ss}");
    }
    var purchases = await sqlService.GetPurchasesAsync(fromDate);
    if (excludedProductIdsSet.Count > 0)
    {
        foreach (var purchase in purchases)
        {
            purchase.Products = purchase.Products
                .Where(pp => !excludedProductIdsSet.Contains(pp.IdProduto))
                .ToList();
        }

        purchases = purchases
            .Where(p => p.Products.Any())
            .ToList();
    }
    Console.WriteLine($"   📊 Total de compras encontradas: {purchases.Count}");

    if (purchases.Count == 0)
    {
        Console.WriteLine("   ℹ️  Nenhuma compra encontrada. Finalizando.");
        return 0;
    }

    var totalProducts = purchases.SelectMany(p => p.Products).Select(pp => pp.IdProduto).Distinct().Count();
    Console.WriteLine($"   📦 Total de produtos únicos: {totalProducts}");

    // 5. Calcular recomendações
    Console.WriteLine("\n[STEP 5] Calculando recomendações (SIMS co-purchase)...");
    var recommendations = personalizationService.CalculateRecommendations(purchases);

    if (excludedProductIdsSet.Count > 0 && recommendations.Count > 0)
    {
        foreach (var key in recommendations.Keys.ToList())
        {
            if (excludedProductIdsSet.Contains(key))
            {
                recommendations.Remove(key);
                continue;
            }

            var filteredList = recommendations[key]
                .Where(r => !excludedProductIdsSet.Contains(r.ProductId))
                .ToList();

            if (filteredList.Count == 0)
            {
                recommendations.Remove(key);
            }
            else
            {
                recommendations[key] = filteredList;
            }
        }
    }

    Console.WriteLine($"   ✅ Recomendações calculadas para {recommendations.Count} produtos (após exclusões)");

    // 6. Salvar/atualizar no DynamoDB
    Console.WriteLine("\n[STEP 6] Salvando recomendações no DynamoDB...");
    var savedCount = 0;
    var updatedCount = 0;
    var errorCount = 0;

    foreach (var (productId, recommendedProducts) in recommendations)
    {
        try
        {
            var existingRecord = await dynamoService.GetRecommendationAsync(productId.ToString());

            var record = new RecommendationRecord
            {
                ProductId = productId.ToString(),
                RecommendedProducts = recommendedProducts,
                LastUpdated = DateTime.UtcNow
            };

            await dynamoService.PutRecommendationAsync(record);

            if (existingRecord != null)
            {
                updatedCount++;
            }
            else
            {
                savedCount++;
            }

            if ((savedCount + updatedCount) % 100 == 0)
            {
                Console.WriteLine($"   📊 Processados: {savedCount + updatedCount} produtos...");
            }
        }
        catch (Exception ex)
        {
            errorCount++;
            Console.WriteLine($"   ❌ Erro ao salvar produto {productId}: {ex.Message}");
        }
    }

    Console.WriteLine($"\n   📊 Resumo:");
    Console.WriteLine($"      - Novos registros: {savedCount}");
    Console.WriteLine($"      - Atualizados: {updatedCount}");
    Console.WriteLine($"      - Erros: {errorCount}");

    // 7. Salvar data de última execução
    Console.WriteLine("\n[STEP 7] Salvando estado da execução...");
    await stateService.SaveLastProcessedDateAsync(DateTime.UtcNow);

    // 8. Se não era primeira execução, marcar como false no config para próximas execuções
    if (isFirstRun)
    {
        Console.WriteLine("\n   ℹ️  Primeira execução concluída. Configure 'Personalization:FirstRun' como 'false' no appsettings.json para próximas execuções.");
    }

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

