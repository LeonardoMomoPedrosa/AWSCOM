using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

namespace Personalize.Services;

public class EmailService : IDisposable
{
    private readonly AmazonSimpleEmailServiceClient _sesClient;
    private readonly string _fromEmail;

    public EmailService(string fromEmail, string region)
    {
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new ArgumentException("fromEmail must be informed", nameof(fromEmail));
        }

        _fromEmail = fromEmail;
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        _sesClient = new AmazonSimpleEmailServiceClient(regionEndpoint);
    }

    public async Task<bool> SendReportAsync(string toEmail, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("toEmail must be informed", nameof(toEmail));
        }

        var request = new SendEmailRequest
        {
            Source = _fromEmail,
            Destination = new Destination
            {
                ToAddresses = new List<string> { toEmail }
            },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Text = new Content
                    {
                        Charset = "UTF-8",
                        Data = body
                    }
                }
            }
        };

        var response = await _sesClient.SendEmailAsync(request);
        Console.WriteLine($"   📬 Relatório SES MessageId: {response.MessageId}");
        return true;
    }

    public void Dispose()
    {
        _sesClient?.Dispose();
    }
}


