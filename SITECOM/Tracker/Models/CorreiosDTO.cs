using System.Text.Json.Serialization;

namespace Tracker.Models;

public class CorreiosRastreamentoDTO
{
    [JsonPropertyName("versao")]
    public string? Versao { get; set; }
    
    [JsonPropertyName("quantidade")]
    public int Quantidade { get; set; }
    
    [JsonPropertyName("objetos")]
    public List<ObjetoRastreamentoDTO>? Objetos { get; set; }
    
    [JsonPropertyName("tipoResultado")]
    public string? TipoResultado { get; set; }
}

public class ObjetoRastreamentoDTO
{
    [JsonPropertyName("codObjeto")]
    public string? CodObjeto { get; set; }
    
    [JsonPropertyName("tipoPostal")]
    public TipoPostalDTO? TipoPostal { get; set; }
    
    [JsonPropertyName("dtPrevista")]
    public DateTime? DtPrevista { get; set; }
    
    [JsonPropertyName("contrato")]
    public string? Contrato { get; set; }
    
    [JsonPropertyName("eventos")]
    public List<EventoDTO>? Eventos { get; set; }
}

public class EventoDTO
{
    [JsonPropertyName("codigo")]
    public string? Codigo { get; set; }
    
    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }
    
    [JsonPropertyName("dtHrCriado")]
    public DateTime DtHrCriado { get; set; }
    
    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }
    
    [JsonPropertyName("detalhe")]
    public string? Detalhe { get; set; }
    
    [JsonPropertyName("unidade")]
    public UnidadeDTO? Unidade { get; set; }
    
    [JsonPropertyName("unidadeDestino")]
    public UnidadeDTO? UnidadeDestino { get; set; }
}

public class TipoPostalDTO
{
    [JsonPropertyName("sigla")]
    public string? Sigla { get; set; }
    
    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }
    
    [JsonPropertyName("categoria")]
    public string? Categoria { get; set; }
}

public class UnidadeDTO
{
    [JsonPropertyName("codSro")]
    public string? CodSro { get; set; }
    
    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }
    
    [JsonPropertyName("endereco")]
    public EnderecoDTO? Endereco { get; set; }
}

public class EnderecoDTO
{
    [JsonPropertyName("cep")]
    public string? Cep { get; set; }
    
    [JsonPropertyName("logradouro")]
    public string? Logradouro { get; set; }
    
    [JsonPropertyName("numero")]
    public string? Numero { get; set; }
    
    [JsonPropertyName("bairro")]
    public string? Bairro { get; set; }
    
    [JsonPropertyName("cidade")]
    public string? Cidade { get; set; }
    
    [JsonPropertyName("uf")]
    public string? Uf { get; set; }
}

public class CorreiosAuthDTO
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

