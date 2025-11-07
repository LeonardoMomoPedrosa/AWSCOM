using Personalize.Models;

namespace Personalize.Services;

public class PersonalizationService
{
    private readonly int _topRecommendations;
    private readonly double _timeDecayHalfLifeDays;

    public PersonalizationService(int topRecommendations, double timeDecayHalfLifeDays)
    {
        _topRecommendations = topRecommendations;
        _timeDecayHalfLifeDays = timeDecayHalfLifeDays;
    }

    /// <summary>
    /// Calcula recomendações usando SIMS (co-purchase) com escores count, lift e cosine
    /// </summary>
    public Dictionary<int, List<RecommendedProduct>> CalculateRecommendations(
        List<Purchase> purchases)
    {
        Console.WriteLine("   📊 Calculando co-compras...");

        // 1. Construir matriz de co-compras com decaimento temporal
        var coPurchaseMatrix = BuildCoPurchaseMatrix(purchases);

        // 2. Calcular estatísticas de produtos
        var productStats = CalculateProductStatistics(purchases);

        // 3. Calcular recomendações para cada produto
        var recommendations = new Dictionary<int, List<RecommendedProduct>>();

        foreach (var productId in coPurchaseMatrix.Keys)
        {
            var productRecommendations = CalculateProductRecommendations(
                productId, 
                coPurchaseMatrix, 
                productStats);

            if (productRecommendations.Any())
            {
                recommendations[productId] = productRecommendations
                    .OrderByDescending(r => r.Score)
                    .Take(_topRecommendations)
                    .ToList();
            }
        }

        Console.WriteLine($"   ✅ Calculadas recomendações para {recommendations.Count} produtos");
        return recommendations;
    }

    /// <summary>
    /// Constrói matriz de co-compras com decaimento temporal
    /// </summary>
    private Dictionary<int, Dictionary<int, double>> BuildCoPurchaseMatrix(
        List<Purchase> purchases)
    {
        var matrix = new Dictionary<int, Dictionary<int, double>>();
        var referenceDate = DateTime.UtcNow;

        foreach (var purchase in purchases)
        {
            var products = purchase.Products.Select(p => p.IdProduto).Distinct().ToList();
            
            // Calcular peso temporal para esta compra
            var daysSincePurchase = (referenceDate - purchase.Data).TotalDays;
            var timeWeight = CalculateTimeDecay(daysSincePurchase);

            // Para cada par de produtos na mesma compra
            for (int i = 0; i < products.Count; i++)
            {
                var productA = products[i];
                
                if (!matrix.ContainsKey(productA))
                {
                    matrix[productA] = new Dictionary<int, double>();
                }

                for (int j = i + 1; j < products.Count; j++)
                {
                    var productB = products[j];

                    // Adicionar co-compra com peso temporal
                    if (!matrix[productA].ContainsKey(productB))
                    {
                        matrix[productA][productB] = 0;
                    }
                    matrix[productA][productB] += timeWeight;

                    // Matriz simétrica
                    if (!matrix.ContainsKey(productB))
                    {
                        matrix[productB] = new Dictionary<int, double>();
                    }
                    if (!matrix[productB].ContainsKey(productA))
                    {
                        matrix[productB][productA] = 0;
                    }
                    matrix[productB][productA] += timeWeight;
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// Calcula decaimento temporal exponencial
    /// </summary>
    private double CalculateTimeDecay(double daysSincePurchase)
    {
        if (daysSincePurchase < 0) return 1.0;
        
        // Decaimento exponencial: weight = 2^(-days/halfLife)
        return Math.Pow(2, -daysSincePurchase / _timeDecayHalfLifeDays);
    }

    /// <summary>
    /// Calcula estatísticas de produtos (total de compras)
    /// </summary>
    private Dictionary<int, double> CalculateProductStatistics(List<Purchase> purchases)
    {
        var stats = new Dictionary<int, double>();
        var referenceDate = DateTime.UtcNow;

        foreach (var purchase in purchases)
        {
            var timeWeight = CalculateTimeDecay((referenceDate - purchase.Data).TotalDays);
            var productIds = purchase.Products.Select(p => p.IdProduto).Distinct();

            foreach (var productId in productIds)
            {
                if (!stats.ContainsKey(productId))
                {
                    stats[productId] = 0;
                }
                stats[productId] += timeWeight;
            }
        }

        return stats;
    }

    /// <summary>
    /// Calcula recomendações para um produto usando múltiplos escores
    /// </summary>
    private List<RecommendedProduct> CalculateProductRecommendations(
        int productId,
        Dictionary<int, Dictionary<int, double>> coPurchaseMatrix,
        Dictionary<int, double> productStats)
    {
        if (!coPurchaseMatrix.ContainsKey(productId))
        {
            return new List<RecommendedProduct>();
        }

        var recommendations = new Dictionary<int, (double count, double lift, double cosine)>();
        var productFrequency = productStats.ContainsKey(productId) ? productStats[productId] : 0;
        
        if (productFrequency == 0)
        {
            return new List<RecommendedProduct>();
        }

        var totalPurchases = productStats.Values.Sum();

        // Calcular escores para cada produto co-comprado
        foreach (var (coPurchasedProductId, coPurchaseCount) in coPurchaseMatrix[productId])
        {
            if (!productStats.ContainsKey(coPurchasedProductId))
            {
                continue;
            }

            var coPurchasedProductFrequency = productStats[coPurchasedProductId];
            
            if (coPurchasedProductFrequency == 0)
            {
                continue;
            }

            // Score COUNT: número de co-compras (com decaimento temporal)
            var countScore = coPurchaseCount;

            // Score LIFT: mede a força da associação
            // lift = P(A and B) / (P(A) * P(B))
            // = (coPurchaseCount / totalPurchases) / ((productFrequency / totalPurchases) * (coPurchasedProductFrequency / totalPurchases))
            // = (coPurchaseCount * totalPurchases) / (productFrequency * coPurchasedProductFrequency)
            var liftScore = totalPurchases > 0 
                ? (coPurchaseCount * totalPurchases) / (productFrequency * coPurchasedProductFrequency)
                : 0;

            // Score COSINE: similaridade cosseno (normalizado entre 0 e 1)
            // cosine = coPurchaseCount / sqrt(productFrequency * coPurchasedProductFrequency)
            var cosineScore = Math.Sqrt(productFrequency * coPurchasedProductFrequency) > 0
                ? coPurchaseCount / Math.Sqrt(productFrequency * coPurchasedProductFrequency)
                : 0;

            recommendations[coPurchasedProductId] = (countScore, liftScore, cosineScore);
        }

        // Normalizar escores para mesma escala (0-1) antes de combinar
        var countValues = recommendations.Values.Select(r => r.count).ToList();
        var liftValues = recommendations.Values.Select(r => r.lift).ToList();
        var cosineValues = recommendations.Values.Select(r => r.cosine).ToList();

        var maxCount = countValues.Any() ? countValues.Max() : 1;
        var maxLift = liftValues.Any() ? liftValues.Max() : 1;
        var maxCosine = cosineValues.Any() ? cosineValues.Max() : 1;

        var result = new List<RecommendedProduct>();

        foreach (var (coPurchasedProductId, (countScore, liftScore, cosineScore)) in recommendations)
        {
            // Normalizar cada score para 0-1
            var normalizedCount = maxCount > 0 ? countScore / maxCount : 0;
            var normalizedLift = maxLift > 0 ? liftScore / maxLift : 0;
            var normalizedCosine = maxCosine > 0 ? cosineScore / maxCosine : 0;

            // Combinar escores normalizados (média ponderada: 30% count, 40% lift, 30% cosine)
            var combinedScore = 0.3 * normalizedCount + 0.4 * normalizedLift + 0.3 * normalizedCosine;

            result.Add(new RecommendedProduct
            {
                ProductId = coPurchasedProductId,
                Score = combinedScore,
                ScoreType = "combined"
            });
        }

        return result;
    }
}

