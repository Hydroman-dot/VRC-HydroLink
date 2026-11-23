using System.Windows.Controls;

namespace HydroLink.App;

public partial class MainWindow
{
    private void BuildHapticsControls(StackPanel panel) => BuildToyHaptics(panel);

    private void RefreshHaptics() => RefreshToyHaptics();
}
