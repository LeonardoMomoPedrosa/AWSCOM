namespace Personalize.Models;

public class Purchase
{
    public int PKId { get; set; }
    public int PKIdUsuario { get; set; }
    public string Status { get; set; } = string.Empty;
    public int IdDados { get; set; }
    public DateTime Data { get; set; }
    public DateTime? DataMdSt { get; set; }
    public List<ProductPurchase> Products { get; set; } = new();
}

