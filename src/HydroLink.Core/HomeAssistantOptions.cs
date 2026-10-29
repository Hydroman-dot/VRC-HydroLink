namespace HydroLink.Core;
public sealed class HomeAssistantOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public bool Tls { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Host) || Host.Contains('/') || Port is < 1 or > 65535)
            throw new InvalidOperationException("Home Assistant: MQTT-Host ohne http:// und einen gültigen Port angeben.");
    }
}
