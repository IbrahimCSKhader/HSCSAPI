
namespace HSCSAPI.Settings;

public class EmailSettings
{
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}
