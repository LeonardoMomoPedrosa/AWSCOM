using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Tracker.Models;
using System.Text.Json;

namespace Tracker.Services;

public class EmailService
{
    private readonly AmazonSimpleEmailServiceClient _sesClient;
    private readonly string _fromEmail;
    private readonly string _bccEmail;

    public EmailService(string fromEmail, string bccEmail, string region)
    {
        _fromEmail = fromEmail;
        _bccEmail = bccEmail;
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        _sesClient = new AmazonSimpleEmailServiceClient(regionEndpoint);
    }

    public async Task<bool> SendTrackingEmailAsync(string toEmail, string nomeCliente, string rastreamentoJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine($"      ⚠️  Email vazio para {nomeCliente}. Pulando...");
                return false;
            }

            // Deserializar o JSON de rastreamento
            CorreiosRastreamentoDTO? rastreamento;
            try
            {
                rastreamento = JsonSerializer.Deserialize<CorreiosRastreamentoDTO>(rastreamentoJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"      ⚠️  Erro ao deserializar JSON de rastreamento: {ex.Message}");
                return false;
            }

            if (rastreamento == null || rastreamento.Objetos == null || rastreamento.Objetos.Count == 0)
            {
                Console.WriteLine($"      ⚠️  Dados de rastreamento inválidos. Pulando email...");
                return false;
            }

            var objeto = rastreamento.Objetos[0];
            var htmlBody = GenerateTrackingEmailHtml(nomeCliente, objeto);

            var destination = new Destination
            {
                ToAddresses = new List<string> { toEmail }
            };

            // Adicionar CCO (cópia oculta) se configurado
            if (!string.IsNullOrWhiteSpace(_bccEmail))
            {
                destination.BccAddresses = new List<string> { _bccEmail };
            }

            var request = new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = destination,
                Message = new Message
                {
                    Subject = new Content($"Atualização do Rastreamento - {objeto.CodObjeto ?? "Sua Encomenda"}"),
                    Body = new Body
                    {
                        Html = new Content { Charset = "UTF-8", Data = htmlBody }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(request);
            Console.WriteLine($"      ✅ Email enviado para {toEmail} (MessageId: {response.MessageId})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      ❌ Erro ao enviar email para {toEmail}: {ex.Message}");
            return false;
        }
    }

    private string GenerateTrackingEmailHtml(string nomeCliente, ObjetoRastreamentoDTO objeto)
    {
        var eventos = objeto.Eventos ?? new List<EventoDTO>();
        
        // Ordenar eventos: mais recente primeiro (mais recente em cima)
        eventos = eventos.OrderByDescending(e => e.DtHrCriado).ToList();

        var codObjeto = objeto.CodObjeto ?? "N/A";
        var tipoPostal = objeto.TipoPostal?.Descricao ?? objeto.TipoPostal?.Sigla ?? "N/A";
        var dtPrevista = objeto.DtPrevista?.ToString("dd/MM/yyyy") ?? "N/A";

        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Atualização de Rastreamento</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            margin-bottom: 25px;
            padding-bottom: 15px;
            border-bottom: 1px solid #dee2e6;
        }}
        .header-logo {{
            margin-bottom: 10px;
        }}
        .header-logo img {{
            width: 220px;
            height: auto;
        }}
        .header h1 {{
            color: #333;
            margin: 0;
            font-size: 18px;
            font-weight: 600;
        }}
        .greeting {{
            font-size: 16px;
            margin-bottom: 20px;
        }}
        .info-card {{
            background-color: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 8px;
            padding: 15px;
            margin-bottom: 30px;
        }}
        .info-card h3 {{
            margin-top: 0;
            color: #333;
            font-size: 18px;
            border-bottom: 1px solid #dee2e6;
            padding-bottom: 10px;
        }}
        .info-row {{
            display: flex;
            margin-bottom: 10px;
        }}
        .info-label {{
            font-weight: bold;
            width: 120px;
            color: #666;
        }}
        .info-value {{
            flex: 1;
            color: #333;
        }}
        .timeline-container {{
            margin-top: 20px;
        }}
        .timeline-item {{
            display: flex;
            align-items: flex-start;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 1px solid #f0f0f0;
        }}
        .timeline-item:last-child {{
            border-bottom: none;
            margin-bottom: 0;
            padding-bottom: 0;
        }}
        .timeline-icon {{
            flex-shrink: 0;
            width: 50px;
            height: 50px;
            display: flex;
            align-items: center;
            justify-content: center;
            background-color: rgba(13, 110, 253, 0.1);
            border: 2px solid #0d6efd;
            border-radius: 50%;
            margin-right: 15px;
            font-size: 28px;
        }}
        .event-content {{
            flex: 1;
        }}
        .event-card {{
            background-color: #f8f9ff;
            border-left: 3px solid #0d6efd;
            border-radius: 4px;
            padding: 12px 15px;
        }}
        .event-card-old {{
            background-color: #f8f9fa;
            border-left: 3px solid #dee2e6;
            border-radius: 4px;
            padding: 12px 15px;
        }}
        .event-title {{
            font-size: 15px;
            font-weight: 600;
            color: #0d6efd;
            margin-bottom: 8px;
        }}
        .event-title-old {{
            font-size: 15px;
            font-weight: 500;
            color: #333;
            margin-bottom: 8px;
        }}
        .event-detail {{
            font-size: 13px;
            color: #666;
            margin-top: 5px;
        }}
        .event-location {{
            font-size: 13px;
            color: #666;
            margin-top: 3px;
        }}
        .event-date {{
            font-size: 13px;
            color: #888;
            margin-top: 3px;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #dee2e6;
            text-align: center;
            color: #666;
            font-size: 14px;
        }}
        .footer a {{
            color: #0d6efd;
            text-decoration: none;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""header-logo"">
                <img src=""https://aquanimal.com.br/images/mailogo.jpg"" alt=""Aquanimal"" style=""width: 220px;"">
            </div>
            <h1>Atualização de Rastreamento</h1>
        </div>
        
        <div class=""greeting"">
            <p>Olá <strong>{nomeCliente}</strong>!</p>
            <p>Estamos felizes em informar a atualização do rastreamento da sua encomenda.</p>
        </div>

        <div class=""info-card"">
            <h3>Informações do Objeto</h3>
            <div class=""info-row"">
                <div class=""info-label"">Código:</div>
                <div class=""info-value""><strong>{codObjeto}</strong></div>
            </div>
            <div class=""info-row"">
                <div class=""info-label"">Tipo:</div>
                <div class=""info-value"">{tipoPostal}</div>
            </div>
            <div class=""info-row"">
                <div class=""info-label"">Previsão:</div>
                <div class=""info-value"">{dtPrevista}</div>
            </div>
        </div>

        <h3 style=""margin-top: 30px; margin-bottom: 15px; font-size: 16px; color: #333;"">Histórico de Rastreamento</h3>
        
        <div class=""timeline-container"">";

        for (int i = 0; i < eventos.Count; i++)
        {
            var evento = eventos[i];
            var isFirst = i == 0; // Primeiro = mais recente (ordenado do mais recente para o mais antigo)
            var eventoDate = evento.DtHrCriado.ToString("dd/MM/yyyy HH:mm:ss");
            
            var localizacao = "";
            if (evento.Unidade?.Endereco != null)
            {
                var cidade = evento.Unidade.Endereco.Cidade ?? "";
                var uf = evento.Unidade.Endereco.Uf ?? "";
                localizacao = $"{cidade}/{uf}".Trim('/');
            }

            // Peixe apenas no primeiro evento (mais recente) - ao lado esquerdo
            var icone = isFirst 
                ? @"<div class=""timeline-icon"">🐟</div>" 
                : @"<div style=""width: 50px; margin-right: 15px;""></div>"; // Espaço vazio para alinhar

            var cardClass = isFirst ? "event-card" : "event-card-old";
            var titleClass = isFirst ? "event-title" : "event-title-old";

            html += $@"
            <div class=""timeline-item"">
                {icone}
                <div class=""event-content"">
                    <div class=""{cardClass}"">
                        <div class=""{titleClass}"">{evento.Descricao ?? "N/A"}</div>
                        <div class=""event-date"">📅 {eventoDate}</div>
                        {(string.IsNullOrEmpty(localizacao) ? "" : $@"<div class=""event-location"">📍 {localizacao}</div>")}
                        {(string.IsNullOrEmpty(evento.Detalhe) ? "" : $@"<div class=""event-detail"">{evento.Detalhe}</div>")}
                    </div>
                </div>
            </div>";
        }

        html += $@"
        </div>

        <div class=""footer"">
            <p>Atenciosamente,<br><strong>Equipe Aquanimal</strong></p>
            <p><a href=""https://aquanimal.com.br"">aquanimal.com.br</a></p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    public async Task<bool> SendJadlogEmailAsync(string toEmail, string nomeCliente, string codRastreamento)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine($"      ⚠️  Email vazio para {nomeCliente}. Pulando...");
                return false;
            }

            if (string.IsNullOrWhiteSpace(codRastreamento))
            {
                Console.WriteLine($"      ⚠️  Código de rastreamento vazio. Pulando email...");
                return false;
            }

            var htmlBody = GenerateJadlogEmailHtml(nomeCliente, codRastreamento);

            var destination = new Destination
            {
                ToAddresses = new List<string> { toEmail }
            };

            // Adicionar CCO (cópia oculta) se configurado
            if (!string.IsNullOrWhiteSpace(_bccEmail))
            {
                destination.BccAddresses = new List<string> { _bccEmail };
            }

            var request = new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = destination,
                Message = new Message
                {
                    Subject = new Content($"Rastreamento Disponível - {codRastreamento}"),
                    Body = new Body
                    {
                        Html = new Content { Charset = "UTF-8", Data = htmlBody }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(request);
            Console.WriteLine($"      ✅ Email enviado para {toEmail} (MessageId: {response.MessageId})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      ❌ Erro ao enviar email para {toEmail}: {ex.Message}");
            return false;
        }
    }

    private string GenerateJadlogEmailHtml(string nomeCliente, string codRastreamento)
    {
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Rastreamento Disponível</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            margin-bottom: 25px;
            padding-bottom: 15px;
            border-bottom: 1px solid #dee2e6;
        }}
        .header-logo {{
            margin-bottom: 10px;
        }}
        .header-logo img {{
            width: 220px;
            height: auto;
        }}
        .greeting {{
            font-size: 16px;
            margin-bottom: 20px;
        }}
        .tracking-code {{
            background-color: #f8f9fa;
            border: 2px solid #0d6efd;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
            text-align: center;
            font-size: 18px;
            font-weight: bold;
            color: #0d6efd;
        }}
        .info-text {{
            font-size: 15px;
            margin: 20px 0;
            color: #333;
        }}
        .link {{
            color: #0d6efd;
            text-decoration: none;
            font-weight: bold;
        }}
        .link:hover {{
            text-decoration: underline;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #dee2e6;
            text-align: center;
            color: #666;
            font-size: 14px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""header-logo"">
                <img src=""https://aquanimal.com.br/images/mailogo.jpg"" alt=""Aquanimal"" style=""width: 220px;"">
            </div>
        </div>
        
        <div class=""greeting"">
            <p>Olá <strong>{nomeCliente}</strong>,</p>
            <p>Gostaríamos de informar que seu rastreamento já está disponível.</p>
        </div>

        <div class=""tracking-code"">
            {codRastreamento}
        </div>

        <div class=""info-text"">
            <p>Para verificar os detalhes do rastreamento, entre em <a href=""https://jadlog.com.br"" class=""link"">https://jadlog.com.br</a> e digite o número acima.</p>
        </div>

        <div class=""footer"">
            <p>A Aquanimal agradece.</p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    public async Task<bool> SendBuslogEmailAsync(string toEmail, string nomeCliente)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine($"      ⚠️  Email vazio para {nomeCliente}. Pulando...");
                return false;
            }

            var htmlBody = GenerateBuslogEmailHtml();

            var destination = new Destination
            {
                ToAddresses = new List<string> { toEmail }
            };

            // Adicionar CCO (cópia oculta) se configurado
            if (!string.IsNullOrWhiteSpace(_bccEmail))
            {
                destination.BccAddresses = new List<string> { _bccEmail };
            }

            var request = new SendEmailRequest
            {
                Source = _fromEmail,
                Destination = destination,
                Message = new Message
                {
                    Subject = new Content("Rastreamento Disponível - Buslog"),
                    Body = new Body
                    {
                        Html = new Content { Charset = "UTF-8", Data = htmlBody }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(request);
            Console.WriteLine($"      ✅ Email enviado para {toEmail} (MessageId: {response.MessageId})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      ❌ Erro ao enviar email para {toEmail}: {ex.Message}");
            return false;
        }
    }

    private string GenerateBuslogEmailHtml()
    {
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Rastreamento Disponível</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            margin-bottom: 25px;
            padding-bottom: 15px;
            border-bottom: 1px solid #dee2e6;
        }}
        .header-logo {{
            margin-bottom: 10px;
        }}
        .header-logo img {{
            width: 220px;
            height: auto;
        }}
        .content {{
            font-size: 15px;
            margin: 20px 0;
            color: #333;
        }}
        .link {{
            color: #0d6efd;
            text-decoration: none;
            font-weight: bold;
        }}
        .link:hover {{
            text-decoration: underline;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #dee2e6;
            text-align: center;
            color: #666;
            font-size: 14px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""header-logo"">
                <img src=""https://aquanimal.com.br/images/mailogo.jpg"" alt=""Aquanimal"" style=""width: 220px;"">
            </div>
        </div>
        
        <div class=""content"">
            <p>Prezado Cliente, gostaríamos de informar que o rastreio de sua encomenda está disponível no site da transportadora Buslog.</p>
            <p><a href=""https://www.track3r.com.br/rastreamento/"" class=""link"">https://www.track3r.com.br/rastreamento/</a></p>
            <p>Digite o seu CPF e o cep destino para obter as informações.</p>
        </div>

        <div class=""footer"">
            <p>A Aquanimal agradece.</p>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    public void Dispose()
    {
        _sesClient?.Dispose();
    }
}
