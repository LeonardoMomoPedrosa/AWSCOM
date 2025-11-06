using System.Text.Json;
using Tracker.Models;

namespace Tracker.Services;

public static class TrackingHelper
{
    public static bool IsDeliveryCompleted(string rastreamentoJson)
    {
        if (string.IsNullOrWhiteSpace(rastreamentoJson))
        {
            return false;
        }

        try
        {
            var rastreamento = JsonSerializer.Deserialize<CorreiosRastreamentoDTO>(rastreamentoJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rastreamento?.Objetos == null || rastreamento.Objetos.Count == 0)
            {
                return false;
            }

            // Verificar se há evento BDE contendo "Entregue ao Dest"
            foreach (var objeto in rastreamento.Objetos)
            {
                if (objeto.Eventos != null)
                {
                    foreach (var evento in objeto.Eventos)
                    {
                        // Verificar código BDE e descrição contendo "Entregue ao Dest"
                        if (evento.Codigo == "BDE" && 
                            !string.IsNullOrEmpty(evento.Descricao) &&
                            evento.Descricao.Contains("Entregue ao Dest", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch
        {
            // Se houver erro ao deserializar, considerar como não entregue
            return false;
        }
    }

    public static bool HasTrackingChanged(string oldJson, string newJson)
    {
        if (string.IsNullOrWhiteSpace(oldJson) && !string.IsNullOrWhiteSpace(newJson))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(newJson))
        {
            return false;
        }

        // Comparação simples de strings (JSONs devem estar normalizados)
        // Para comparação mais robusta, poderia deserializar e comparar objetos
        // mas para este caso, comparar strings é suficiente
        return oldJson.Trim() != newJson.Trim();
    }

    public static string SerializeRastreamento(CorreiosRastreamentoDTO rastreamento)
    {
        // Manter a estrutura original do JSON dos Correios (com propriedades em camelCase)
        return JsonSerializer.Serialize(rastreamento, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}

