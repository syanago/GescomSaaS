using GescomSaas.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace GescomSaas.Infrastructure.Services;

public class PlatformNotificationEmailService(
    IOptions<PlatformNotificationEmailOptions> options,
    ILogger<PlatformNotificationEmailService> logger)
{
    private readonly PlatformNotificationEmailOptions settings = options.Value;

    public bool IsConfigured =>
        settings.Enabled &&
        !string.IsNullOrWhiteSpace(settings.Host) &&
        !string.IsNullOrWhiteSpace(settings.FromAddress);

    public async Task<bool> TrySendAsync(
        IReadOnlyCollection<string> recipients,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("NotificationEmail desactive ou incomplet. Aucun e-mail de quota n'a ete envoye.");
            return false;
        }

        var distinctRecipients = recipients
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctRecipients.Length == 0)
        {
            logger.LogInformation("Aucun destinataire e-mail valide pour l'alerte de quota.");
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        foreach (var recipient in distinctRecipients)
        {
            message.To.Add(recipient);
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }
        else
        {
            client.UseDefaultCredentials = settings.UseDefaultCredentials;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);

        logger.LogInformation(
            "Notification de quota envoyee a {RecipientCount} destinataire(s) avec le sujet {Subject}.",
            distinctRecipients.Length,
            subject);

        return true;
    }
}
