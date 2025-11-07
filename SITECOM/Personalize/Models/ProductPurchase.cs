namespace Personalize.Models;

public class ProductPurchase
{
    public int IdUsuario { get; set; }
    public int IdProduto { get; set; }
    public int Quantidade { get; set; }
    public int PKId { get; set; }
    public int PKIdCompra { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime SysCreationDate { get; set; }
    public DateTime? SysUpdateDate { get; set; }
}

