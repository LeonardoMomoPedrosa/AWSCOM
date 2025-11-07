namespace Personalize.Models;

public class RecommendationRecord
{
    public string ProductId { get; set; } = string.Empty;
    public List<RecommendedProduct> RecommendedProducts { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class RecommendedProduct
{
    public int ProductId { get; set; }
    public double Score { get; set; }
    public string ScoreType { get; set; } = string.Empty; // "count", "lift", "cosine"
}

