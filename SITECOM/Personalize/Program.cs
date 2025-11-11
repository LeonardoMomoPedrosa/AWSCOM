using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Personalize.Models;
using Personalize.Services;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SLCOMLIB.Helpers;

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
ISiteApiService? siteApiService = null;

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
    var excludedProductIds = config.GetSection("Personalization:ExcludedProductIds").Get<int[]>() ?? Array.Empty<int>();
    var snapshotFilePath = config["Personalization:SnapshotFilePath"] ?? "personalize_snapshot.txt";
    var reportEmail = config["Personalization:ReportEmail"] ?? string.Empty;
    var reportEmailFrom = config["SES:FromEmail"] ?? reportEmail;
    var reportEmailRegion = config["SES:Region"] ?? region;

    var excludedProductIdsSet = excludedProductIds.Length > 0
        ? excludedProductIds.ToHashSet()
        : new HashSet<int>();
    var timings = new List<(string Step, TimeSpan Duration)>();
    var stepWatch = Stopwatch.StartNew();
    var totalWatch = Stopwatch.StartNew();

    void MarkStep(string step)
    {
        timings.Add((step, stepWatch.Elapsed));
        stepWatch.Restart();
    }

    dynamoService = new DynamoDBService(tableName, region);
    sqlService = new SqlServerService(connectionString);
    personalizationService = new PersonalizationService(topRecommendations, timeDecayHalfLifeDays);
    
    // Configurar SiteApiService para invalidação de cache
    var siteApiConfig = new SiteApiConfig();
    config.GetSection("SiteApi").Bind(siteApiConfig);
    if (siteApiConfig.Servers.Count > 0 && !string.IsNullOrWhiteSpace(siteApiConfig.Username))
    {
        siteApiService = new SiteApiService(siteApiConfig);
        Console.WriteLine($"   🔄 Cache invalidation: {siteApiConfig.Servers.Count} servidor(es) configurado(s)");
    }
    else
    {
        Console.WriteLine($"   ⚠️  Cache invalidation: não configurado (SiteApi.Servers vazio ou credenciais ausentes)");
    }

    Console.WriteLine($"   📊 Tabela DynamoDB: {tableName}");
    Console.WriteLine($"   🌍 Região: {region}");
    Console.WriteLine($"   🎯 Top recomendações: {topRecommendations}");
    Console.WriteLine($"   ⏱️  Meia-vida decaimento temporal: {timeDecayHalfLifeDays} dias");
    if (excludedProductIdsSet.Count > 0)
    {
        Console.WriteLine($"   🚫 Produtos excluídos (IDs): {string.Join(", ", excludedProductIdsSet)}");
    }
    Console.WriteLine($"   🗂️  Snapshot: {snapshotFilePath}");
    if (!string.IsNullOrWhiteSpace(reportEmail))
    {
        Console.WriteLine($"   📧 Relatório: {reportEmail}");
    }
    Console.WriteLine("✅ Serviços inicializados");
    MarkStep("Inicialização de serviços");

    // 3. Buscar dados de compras (histórico completo)
    Console.WriteLine("\n[STEP 3] Buscando dados de compras do SQL Server (histórico completo)...");
    Console.WriteLine("   🔍 Filtros: status = 'V' (enviados)");
    var purchases = await sqlService.GetPurchasesAsync(null);
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
    MarkStep("Busca SQL");

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
    MarkStep("Cálculo de recomendações");

    // 6. Carregar snapshot anterior
    Console.WriteLine("\n[STEP 6] Carregando snapshot anterior...");
    var previousSnapshot = await LoadSnapshotAsync(snapshotFilePath);
    Console.WriteLine($"   📄 Linhas encontradas: {previousSnapshot.Count}");
    MarkStep("Leitura snapshot anterior");

    // 7. Preparar snapshot atual e diff
    Console.WriteLine("\n[STEP 7] Preparando snapshot atual e realizando diff...");
    var allProductIds = recommendations.Keys
        .Union(purchases.SelectMany(p => p.Products).Select(pp => pp.IdProduto))
        .Union(previousSnapshot.Keys)
        .Distinct()
        .OrderBy(id => id)
        .ToList();

    var currentSnapshotMap = new Dictionary<int, string>(allProductIds.Count);
    var currentSnapshotLines = new List<string>(allProductIds.Count);

    foreach (var productId in allProductIds)
    {
        var orderedRecommendedIds = recommendations.TryGetValue(productId, out var recList) && recList.Count > 0
            ? recList.Select(r => r.ProductId).OrderBy(id => id).ToList()
            : new List<int>();

        var snapshotLine = orderedRecommendedIds.Count > 0
            ? $"{productId}:{string.Join(';', orderedRecommendedIds)}"
            : $"{productId}:";

        currentSnapshotMap[productId] = snapshotLine;
        currentSnapshotLines.Add(snapshotLine);
    }

    MarkStep("Preparação snapshot atual");

    var changedProductIds = new HashSet<int>();
    foreach (var productId in allProductIds)
    {
        var currentLine = currentSnapshotMap[productId];
        previousSnapshot.TryGetValue(productId, out var previousLine);

        if (!string.Equals(currentLine, previousLine, StringComparison.Ordinal))
        {
            changedProductIds.Add(productId);
        }
    }

    var unchangedCount = allProductIds.Count - changedProductIds.Count;
    Console.WriteLine($"   📈 Produtos avaliados: {allProductIds.Count}");
    Console.WriteLine($"   🔄 Produtos alterados: {changedProductIds.Count}");
    Console.WriteLine($"   💤 Sem mudanças: {unchangedCount}");
    MarkStep("Diff snapshot");

    // 8. Atualizar DynamoDB somente quando necessário
    Console.WriteLine("\n[STEP 8] Sincronizando alterações com DynamoDB...");
    var upsertCount = 0;
    var deleteCount = 0;
    var errorCount = 0;

    if (changedProductIds.Count > 0)
    {
        foreach (var productId in changedProductIds.OrderBy(id => id))
        {
            try
            {
                if (recommendations.TryGetValue(productId, out var recList) && recList.Count > 0)
                {
                    var record = new RecommendationRecord
                    {
                        ProductId = productId.ToString(),
                        RecommendedProducts = recList,
                        LastUpdated = DateTime.UtcNow
                    };

                    await dynamoService.PutRecommendationAsync(record);
                    upsertCount++;
                }
                else
                {
                    await dynamoService.DeleteRecommendationAsync(productId.ToString());
                    deleteCount++;
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                Console.WriteLine($"   ❌ Erro ao sincronizar produto {productId}: {ex.Message}");
            }
        }
    }
    else
    {
        Console.WriteLine("   ℹ️  Nenhuma alteração identificada. DynamoDB inalterado.");
    }

    Console.WriteLine($"   ✅ Upserts: {upsertCount}");
    Console.WriteLine($"   🗑️  Remoções: {deleteCount}");
    Console.WriteLine($"   ⚠️  Erros: {errorCount}");
    MarkStep("Atualização DynamoDB");

    // 8.5. Invalidar cache do e-commerce para produtos alterados
    var cacheInvalidationCount = 0;
    var cacheInvalidationSuccess = 0;
    var cacheInvalidationFail = 0;
    
    if (changedProductIds.Count > 0 && siteApiService != null)
    {
        Console.WriteLine("\n[STEP 8.5] Invalidando cache do e-commerce...");
        var cacheRequests = new List<CacheInvalidateRequest>();
        
        foreach (var productId in changedProductIds)
        {
            try
            {
                var cacheKeyObj = SiteCacheKeyUtil.GetRecomendationKey(productId);
                var request = new CacheInvalidateRequest
                {
                    Region = cacheKeyObj.Region,
                    Key = cacheKeyObj.Key,
                    CleanRegionInd = false
                };
                cacheRequests.Add(request);
                cacheInvalidationCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Erro ao gerar chave de cache para produto {productId}: {ex.Message}");
                cacheInvalidationFail++;
            }
        }
        
        if (cacheRequests.Count > 0)
        {
            try
            {
                Console.WriteLine($"   📋 Total de requisições de invalidação: {cacheRequests.Count}");
                var success = await siteApiService.InvalidateAsync(cacheRequests);
                if (success)
                {
                    cacheInvalidationSuccess = cacheRequests.Count;
                    Console.WriteLine($"   ✅ Cache invalidado com sucesso para {cacheRequests.Count} produto(s)");
                }
                else
                {
                    cacheInvalidationFail += cacheRequests.Count;
                    Console.WriteLine($"   ⚠️  Algumas invalidações de cache falharam");
                    Console.WriteLine($"   📊 Resumo: {cacheInvalidationSuccess} sucesso, {cacheInvalidationFail} falhas");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ ERRO CRÍTICO ao invalidar cache");
                Console.WriteLine($"   📋 Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"   📄 Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   📄 Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                    if (ex.InnerException.StackTrace != null)
                    {
                        Console.WriteLine($"   📚 Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                    }
                }
                Console.WriteLine($"   📚 Stack Trace:\n{ex.StackTrace}");
                cacheInvalidationFail += cacheRequests.Count;
            }
        }
    }
    else if (changedProductIds.Count > 0 && siteApiService == null)
    {
        Console.WriteLine("\n[STEP 8.5] Cache invalidation: pulado (não configurado)");
    }
    else
    {
        Console.WriteLine("\n[STEP 8.5] Cache invalidation: pulado (nenhum produto alterado)");
    }
    MarkStep("Invalidação de cache");

    // 9. Persistir snapshot atualizado
    Console.WriteLine("\n[STEP 9] Persistindo snapshot atualizado...");
    await WriteSnapshotAsync(snapshotFilePath, currentSnapshotLines);
    Console.WriteLine("   ✅ Snapshot salvo com sucesso");
    MarkStep("Persistência snapshot");

    // 10. Gerar relatório e enviar e-mail
    Console.WriteLine("\n[STEP 10] Gerando relatório de execução...");
    totalWatch.Stop();
    var reportBuilder = new StringBuilder();
    var executionEnd = DateTime.Now;

    reportBuilder.AppendLine("Resumo da execução do Personalize");
    reportBuilder.AppendLine($"Início: {executionEnd - totalWatch.Elapsed:yyyy-MM-dd HH:mm:ss}");
    reportBuilder.AppendLine($"Término: {executionEnd:yyyy-MM-dd HH:mm:ss}");
    reportBuilder.AppendLine($"Duração total: {totalWatch.Elapsed}");
    reportBuilder.AppendLine();
    reportBuilder.AppendLine("Tempos por etapa:");
    foreach (var (step, duration) in timings)
    {
        reportBuilder.AppendLine($"- {step}: {duration}");
    }
    reportBuilder.AppendLine();
    reportBuilder.AppendLine("Estatísticas:");
    reportBuilder.AppendLine($"- Compras processadas: {purchases.Count}");
    reportBuilder.AppendLine($"- Produtos avaliados: {allProductIds.Count}");
    reportBuilder.AppendLine($"- Produtos alterados: {changedProductIds.Count}");
    reportBuilder.AppendLine($"- Produtos sem mudança: {unchangedCount}");
    reportBuilder.AppendLine($"- Upserts no DynamoDB: {upsertCount}");
    reportBuilder.AppendLine($"- Remoções no DynamoDB: {deleteCount}");
    reportBuilder.AppendLine($"- Erros no DynamoDB: {errorCount}");
    if (cacheInvalidationCount > 0)
    {
        reportBuilder.AppendLine($"- Invalidações de cache: {cacheInvalidationCount} (sucesso: {cacheInvalidationSuccess}, falhas: {cacheInvalidationFail})");
    }
    reportBuilder.AppendLine();
    reportBuilder.AppendLine($"Snapshot: {snapshotFilePath}");

    var reportText = reportBuilder.ToString();
    Console.WriteLine(reportText);

    var emailSent = false;
    if (!string.IsNullOrWhiteSpace(reportEmail) && !string.IsNullOrWhiteSpace(reportEmailFrom))
    {
        try
        {
            using var emailService = new EmailService(reportEmailFrom, reportEmailRegion);
            emailSent = await emailService.SendReportAsync(
                reportEmail,
                $"Personalize - resumo {executionEnd:yyyy-MM-dd HH:mm}",
                reportText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Falha ao enviar e-mail de relatório: {ex.Message}");
        }
    }
    else if (!string.IsNullOrWhiteSpace(reportEmail))
    {
        Console.WriteLine("   ⚠️  Relatório não enviado por e-mail: remetente não configurado.");
    }

    if (emailSent)
    {
        Console.WriteLine("   ✅ Relatório enviado por e-mail.");
    }
    else if (!string.IsNullOrWhiteSpace(reportEmail))
    {
        Console.WriteLine("   ⚠️  Relatório não foi enviado por e-mail (verifique logs).");
    }
    MarkStep("Envio de relatório");

    Console.WriteLine("\n===========================================");
    Console.WriteLine("=== CONCLUÍDO COM SUCESSO ===");
    Console.WriteLine($"Finalizado em: {executionEnd:yyyy-MM-dd HH:mm:ss}");
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
    if (siteApiService is IDisposable disposable)
    {
        disposable.Dispose();
    }
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

static async Task<Dictionary<int, string>> LoadSnapshotAsync(string path)
{
    var snapshot = new Dictionary<int, string>();

    if (!File.Exists(path))
    {
        return snapshot;
    }

    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new StreamReader(stream, Encoding.UTF8);

    while (await reader.ReadLineAsync() is { } line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            continue;
        }

        var separatorIndex = trimmed.IndexOf(':');
        if (separatorIndex <= 0)
        {
            continue;
        }

        if (int.TryParse(trimmed.AsSpan(0, separatorIndex), out var productId))
        {
            snapshot[productId] = trimmed;
        }
    }

    return snapshot;
}

static async Task WriteSnapshotAsync(string path, IEnumerable<string> lines)
{
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var tempPath = path + ".tmp";
    await File.WriteAllLinesAsync(tempPath, lines, Encoding.UTF8);
    File.Move(tempPath, path, true);
}

