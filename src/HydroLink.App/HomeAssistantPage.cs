using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private CheckBox haEnabled = null!, haTls = null!;
    private TextBox haHost = null!, haPort = null!, haUsername = null!;
    private PasswordBox haPassword = null!;
    private TextBlock haStatus = null!;
    private TabItem BuildHomeAssistantPage()
    {
        var cfg = settings.HomeAssistant;
        var panel = Page("Home Assistant", "Fitness-Live-Werte und Sessions über deinen MQTT-Broker übertragen.");
        panel.MaxWidth = 800; panel.HorizontalAlignment = HorizontalAlignment.Left;
        var fields = new StackPanel();
        haEnabled = new CheckBox { Content = "MQTT mit dem Fitness-Modul aktivieren", IsChecked = cfg.Enabled };
        haTls = new CheckBox { Content = "TLS verwenden (gültiges Serverzertifikat erforderlich)", IsChecked = cfg.Tls };
        haHost = new TextBox { Text = cfg.Host }; haPort = new TextBox { Text = cfg.Port.ToString() };
        haUsername = new TextBox { Text = cfg.Username }; haPassword = new PasswordBox { Password = cfg.Password };
        fields.Children.Add(haEnabled);
        foreach (var (title, control) in new (string, UIElement)[] { ("MQTT-Host (ohne http://)", haHost), ("Port", haPort), ("Benutzername", haUsername), ("Passwort", haPassword) })
        { fields.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 10, 0, 0) }); fields.Children.Add(control); }
        fields.Children.Add(haTls); panel.Children.Add(fields); launchEditors.Add(fields);
        panel.Children.Add(ActionButton("Einstellungen speichern", () => Save_Click(this, new RoutedEventArgs())));
        panel.Children.Add(new TextBlock { Text = "Zum Ändern laufende Module mit ‚Alles stoppen‘ beenden. Danach speichern und VR Fitness starten. Das Passwort wird mit deinem Windows-Benutzer verschlüsselt gespeichert.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        haStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Color("#7CE8CF") }; panel.Children.Add(haStatus);
        panel.Children.Add(new TextBlock { Text = "Live-Sensoren erscheinen über MQTT Discovery in Home Assistant. Für dauerhafte Sessions ist der bestehende VR-Fitness-Empfänger erforderlich: Er muss session_id + sequence bestätigen. Ohne Bestätigung bleiben die Meldungen lokal gespeichert. Die bisherige SQLite-Warteschlange aus VR Fitness wird nicht automatisch importiert.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 12) });
        panel.Children.Add(ActionButton("Anleitung öffnen", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppContext.BaseDirectory, "docs", "HOME-ASSISTANT.md")) { UseShellExecute = true })));
        return new TabItem { Header = "Home Assistant", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
    private HomeAssistantOptions ReadHomeAssistant()
    {
        if (!int.TryParse(haPort.Text, out var port)) throw new InvalidOperationException("Home Assistant: Port muss eine Zahl sein.");
        var cfg = new HomeAssistantOptions { Enabled = haEnabled.IsChecked == true, Host = haHost.Text.Trim(), Port = port, Tls = haTls.IsChecked == true, Username = haUsername.Text.Trim(), Password = haPassword.Password };
        cfg.Validate();
        // Refuse to redirect pending health data to another broker.
        if (cfg.Enabled) _ = HomeAssistantOutbox.For(new Settings { FitnessOptions = settings.FitnessOptions, HomeAssistant = cfg }).Count;
        return cfg;
    }
    private DateTimeOffset haRefreshAt;
    private void RefreshHomeAssistant()
    {
        if (haStatus == null || DateTimeOffset.UtcNow < haRefreshAt) return;
        haRefreshAt = DateTimeOffset.UtcNow.AddSeconds(3);
        var state = host?.Running.Contains("home-assistant") == true && latest.TryGetValue("home-assistant/status", out var value) ? value.Value.ToString() : settings.HomeAssistant.Enabled ? "Bereit · VR Fitness starten" : "Deaktiviert";
        try { state += "\nLokal gepuffert: " + HomeAssistantOutbox.For(settings).Count + " Meldungen"; }
        catch (Exception ex) { state += "\nPuffer prüfen: " + ex.Message; }
        if (latest.TryGetValue("home-assistant/queue-error", out var error) && error.Value.GetString() is { Length: > 0 } message) state += "\n" + message;
        haStatus.Text = state;
    }
}
