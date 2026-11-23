using System.Windows;
using System.Windows.Controls;
using VrcHapticsBridge.Core;

namespace HydroLink.App;

public partial class MainWindow
{
    private async void EditToySpsSources(ToyKind toy)
    {
        var monitor = host?.Osc?.Monitor;
        var avatar = monitor?.Avatar ?? "";
        if (avatar.Length == 0)
        {
            MessageBox.Show(this, "Lade zuerst einen Avatar in VRChat und warte auf die OSC-Erkennung.");
            return;
        }

        var saved = settings.ToySpsSources.GetValueOrDefault(avatar)?.GetValueOrDefault(toy);
        var dialog = new Window
        {
            Owner = this, Title = ToyProfiles.Label(toy) + " · SPS-Quellen",
            Width = 540, Height = 470, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Die Auswahl gilt für den aktuellen Avatar. Nach dem Speichern das Toy erneut aktivieren.", TextWrapping = TextWrapping.Wrap });
        var mode = new ComboBox { ItemsSource = new[] { "Alle Plugs und Sockets", "Nur ausgewählte Quellen", "Keine SPS-Quelle" },
            SelectedIndex = saved is null ? 0 : saved.Length == 0 ? 2 : 1 };
        panel.Children.Add(mode);
        var sources = SpsSource.Discover(monitor!.Snapshot().Select(parameter => parameter.Address));
        var choices = new StackPanel();
        var boxes = sources.Select(source => new CheckBox
        {
            Content = source.ToString(), Tag = source.Prefix, IsChecked = saved?.Contains(source.Prefix) == true
        }).ToList();
        foreach (var prefix in (saved ?? []).Except(sources.Select(source => source.Prefix)))
            boxes.Add(new CheckBox { Content = prefix.Split('/')[^2] + " · zurzeit nicht erkannt", Tag = prefix, IsChecked = true });
        foreach (var box in boxes) choices.Children.Add(box);
        if (boxes.Count == 0) choices.Children.Add(new TextBlock { Text = "Derzeit keine SPS-Quellen erkannt." });
        panel.Children.Add(new ScrollViewer { Content = choices, Height = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var save = new Button { Content = "Zuordnung speichern", IsDefault = true };
        panel.Children.Add(save);
        void RefreshSelection()
        {
            choices.IsEnabled = mode.SelectedIndex == 1;
            save.IsEnabled = mode.SelectedIndex != 1 || boxes.Any(box => box.IsChecked == true);
        }
        mode.SelectionChanged += (_, _) => RefreshSelection();
        foreach (var box in boxes) { box.Checked += (_, _) => RefreshSelection(); box.Unchecked += (_, _) => RefreshSelection(); }
        RefreshSelection();
        save.Click += (_, _) => dialog.DialogResult = true;
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (host?.Osc?.Monitor.Avatar != avatar) throw new InvalidOperationException("Der Avatar wurde gewechselt. Öffne die Zuordnung erneut.");
            if (host?.Haptics is { } service) await service.StopToyAsync(toy);
            if (host?.Osc?.Monitor.Avatar != avatar) throw new InvalidOperationException("Der Avatar wurde gewechselt. Öffne die Zuordnung erneut.");

            if (!settings.ToySpsSources.TryGetValue(avatar, out var profile))
                settings.ToySpsSources[avatar] = profile = [];
            var previous = profile.GetValueOrDefault(toy);
            if (mode.SelectedIndex == 0) profile.Remove(toy);
            else profile[toy] = mode.SelectedIndex == 2 ? [] : boxes.Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToArray();
            try { settings.Save(); }
            catch { if (previous is null) profile.Remove(toy); else profile[toy] = previous; throw; }
            RefreshToyHaptics();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "SPS-Zuordnung"); }
    }
}
