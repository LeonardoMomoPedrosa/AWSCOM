namespace Tracker.Models;

public class TrackingRecord
{
    public string IdPedido { get; set; } = string.Empty;
    public string TipoEnvio { get; set; } = string.Empty;
    public string CodRastreamento { get; set; } = string.Empty;
    public string RastreamentoJson { get; set; } = string.Empty;
    public string DataCriacao { get; set; } = string.Empty;
}

