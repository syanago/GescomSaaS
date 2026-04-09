namespace GescomSaas.Infrastructure.Configuration;

public sealed class PlatformNotificationEmailOptions
{
    public const string SectionName = "NotificationEmail";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "Gescom SaaS";
}
