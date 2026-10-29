using System.Windows;
using System.Windows.Controls;
namespace HydroLink.App;
public partial class MainWindow
{
    private TabControl settingsTabs = null!;
    private void OrganizeSettings()
    {
        var root = Tabs.Items.OfType<TabItem>().Single(t => Equals(t.Header, "Einstellungen"));
        var general = root.Content; root.Content = null;
        settingsTabs = new TabControl { Margin = new Thickness(12), Background = Color("#172334"), BorderThickness = new Thickness(0) };
        settingsTabs.Items.Add(new TabItem { Header = "Allgemein & Fitness", Content = general });
        foreach (var name in new[] { "Verbindungen", "OSCQuery", "Programme", "Plugins" })
        {
            var tab = Tabs.Items.OfType<TabItem>().Single(t => Equals(t.Header, name));
            Tabs.Items.Remove(tab); settingsTabs.Items.Add(tab);
        }
        settingsTabs.Items.Insert(2, BuildHomeAssistantPage());
        root.Content = settingsTabs;
    }
    private void SelectPage(string name)
    {
        var direct = Tabs.Items.OfType<TabItem>().FirstOrDefault(t => Equals(t.Header, name));
        if (direct != null) { Tabs.SelectedItem = direct; return; }
        var nested = settingsTabs.Items.OfType<TabItem>().FirstOrDefault(t => Equals(t.Header, name));
        if (nested == null) return;
        Tabs.SelectedItem = Tabs.Items.OfType<TabItem>().Single(t => Equals(t.Header, "Einstellungen"));
        settingsTabs.SelectedItem = nested;
    }
}
