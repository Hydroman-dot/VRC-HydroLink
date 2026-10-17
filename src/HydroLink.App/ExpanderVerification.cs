using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace HydroLink.App;
public partial class MainWindow
{
    // Opt-in UI regression check: exercises actual header toggles with runtime edit locks,
    // without starting providers, saving settings or sending hardware commands.
    private void VerifyExpandableSections()
    {
        var selected = Tabs.SelectedItem;
        foreach (var editor in launchEditors) editor.IsEnabled = false;
        int checkedCount = 0;
        foreach (var name in new[] { "VR Fitness", "Haptics", "WindForce" })
        {
            Tabs.SelectedItem = Tabs.Items.OfType<TabItem>().Single(t => Equals(t.Header, name)); UpdateLayout();
            foreach (var section in FindExpanders((DependencyObject)((TabItem)Tabs.SelectedItem).Content).ToArray())
            {
                section.ApplyTemplate();
                if (section.Template.FindName("PART_Header", section) is not ToggleButton header || !header.IsEnabled) throw new InvalidOperationException(name + ": Aufklappkopf ist gesperrt.");
                var toggle = (IToggleProvider)new ToggleButtonAutomationPeer(header).GetPattern(PatternInterface.Toggle);
                section.IsExpanded = false; toggle.Toggle(); UpdateLayout();
                if (!section.IsExpanded) throw new InvalidOperationException(name + ": Öffnen fehlgeschlagen.");
                toggle.Toggle(); UpdateLayout();
                if (section.IsExpanded) throw new InvalidOperationException(name + ": Schließen fehlgeschlagen.");
                section.IsExpanded = name == "VR Fitness"; checkedCount++;
            }
        }
        if (checkedCount < 9) throw new InvalidOperationException("Nicht alle Aufklappbereiche geprüft.");
        Tabs.SelectedItem = selected; UpdateLayout();
    }
    private static IEnumerable<Expander> FindExpanders(DependencyObject root)
    {
        if (root is Expander expander) yield return expander;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in FindExpanders(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
