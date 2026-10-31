using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using HydroLink.Core;
namespace HydroLink.App;

internal static class SessionReportView
{
    private static Brush Ink(bool print) => print ? Brushes.Black : new SolidColorBrush(System.Windows.Media.Color.FromRgb(229,237,247));
    private static TextBlock Text(string text, double size, bool print) => new() { Text = text, FontSize = size, Foreground = Ink(print), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,6) };
    private static string Value(SessionReport report, string key, string unit = "", bool duration = false)
    {
        if (!report.Summary.TryGetValue(key, out var raw) || raw == null || !double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) return "—";
        return duration ? DurationDisplay.Format(value) : value.ToString("0.##", CultureInfo.GetCultureInfo("de-DE")) + unit;
    }
    public static FrameworkElement Overview(SessionReport report, bool print = false)
    {
        var panel = new StackPanel();
        panel.Children.Add(Text(report.Summary.GetValueOrDefault("session_name")?.ToString() ?? "Fitness-Session", 24, print));
        var started = report.Summary.GetValueOrDefault("start")?.ToString();
        panel.Children.Add(Text(DateTimeOffset.TryParse(started, out var date) ? date.ToLocalTime().ToString("dd.MM.yyyy · HH:mm") : "Startzeit nicht verfügbar", 13, print));
        var cards = new WrapPanel();
        foreach (var (label, key, unit, duration) in new[] {
            ("Dauer", "duration_s", "", true), ("Aktive Zeit", "active_s", "", true),
            ("Distanz", "distance_km", " km", false), ("Schritte", "steps", "", false),
            ("Puls Ø", "avg_bpm", " bpm", false), ("Puls maximal", "max_bpm", " bpm", false),
            ("Tempo Ø in Bewegung", "avg_active_speed_kmh", " km/h", false), ("Gültige Pulswerte", "valid_pulse_pct", " %", false) })
        {
            var content = new StackPanel(); content.Children.Add(Text(label, 12, print)); content.Children.Add(Text(Value(report, key, unit, duration), 21, print));
            cards.Children.Add(new Border { Child = content, Width = print ? 145 : 205, Padding = new Thickness(12), Margin = new Thickness(0,0,8,8), CornerRadius = new CornerRadius(7), Background = print ? Brushes.WhiteSmoke : new SolidColorBrush(System.Windows.Media.Color.FromRgb(32,50,72)) });
        }
        panel.Children.Add(cards);
        panel.Children.Add(Text($"{report.Rows.Count:N0} Messpunkte · Zeiten in h:mm:ss · Ø-Werte beziehen sich auf vorhandene Messpunkte.", 12, print));
        return panel;
    }
    public static FrameworkElement Chart(SessionReport report, string key, string title, bool print = false)
    {
        const double width = 850, height = 215, left = 65, right = 825, top = 20, bottom = 175;
        var panel = new StackPanel { Margin = new Thickness(0,12,0,8) };
        panel.Children.Add(Text(title, 18, print));
        var points = SessionCharts.Points(report, key);
        var valid = points.Where(p => p.Value.HasValue).ToArray();
        if (valid.Length == 0) { panel.Children.Add(Text("Keine gültigen Messwerte mit Zeitangabe vorhanden.", 13, print)); return panel; }
        var canvas = new Canvas { Width = width, Height = height, Background = print ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(14,23,36)) };
        double min = key == "clean_speed_kmh" ? 0 : Math.Max(0, Math.Floor(valid.Min(p => p.Value!.Value) / 10) * 10 - 10);
        double max = Math.Max(min + 1, valid.Max(p => p.Value!.Value)); max += Math.Max(1, (max-min)*.1);
        var end = Math.Max(1, points.Last().Seconds);
        void Label(string text, double x, double y) { var label = Text(text, 11, print); label.Margin = new Thickness(0); Canvas.SetLeft(label,x); Canvas.SetTop(label,y); canvas.Children.Add(label); }
        for (int i = 0; i <= 4; i++)
        {
            double y = bottom - i * (bottom-top)/4;
            canvas.Children.Add(new Line { X1=left,X2=right,Y1=y,Y2=y,Stroke=print ? Brushes.LightGray : Brushes.DimGray,StrokeThickness=.5 });
            Label((min+(max-min)*i/4).ToString("0.#",CultureInfo.GetCultureInfo("de-DE")),5,y-7);
            double x = left+i*(right-left)/4;
            Label(DurationDisplay.Format(end*i/4), Math.Min(x-20,right-50),bottom+12);
        }
        var intervals = points.Zip(points.Skip(1),(a,b)=>b.Seconds-a.Seconds).Where(d=>d>0).Order().ToArray();
        var gap = intervals.Length == 0 ? 5 : Math.Max(5, intervals[intervals.Length/2]*3);
        var color = print ? Brushes.Teal : new SolidColorBrush(System.Windows.Media.Color.FromRgb(100,218,192));
        // Bucket by display pixel: retain minimum/maximum so brief peaks remain visible.
        var segments = new List<List<SessionChartPoint>>(); var segment = new List<SessionChartPoint>(); double last = -1;
        foreach (var point in points)
        {
            if (!point.Value.HasValue || last >= 0 && point.Seconds-last > gap) { if (segment.Count>0) segments.Add(segment); segment=[]; }
            if (point.Value.HasValue) segment.Add(point);
            last=point.Seconds;
        }
        if (segment.Count>0) segments.Add(segment);
        foreach (var run in segments)
        {
            var reduced = run.GroupBy(p=>(int)(p.Seconds/end*(right-left))).SelectMany(g=>new[] { g.First(),g.MinBy(p=>p.Value)!,g.MaxBy(p=>p.Value)!,g.Last() }.Distinct().OrderBy(p=>p.Seconds));
            var line = new Polyline { Stroke=color,StrokeThickness=1.8,Points=new PointCollection(reduced.Select(p=>new Point(left+p.Seconds/end*(right-left),bottom-(p.Value!.Value-min)/(max-min)*(bottom-top)))) };
            canvas.Children.Add(line);
            if (run.Count==1) { var dot=new Ellipse {Width=4,Height=4,Fill=color}; Canvas.SetLeft(dot,line.Points[0].X-2); Canvas.SetTop(dot,line.Points[0].Y-2); canvas.Children.Add(dot); }
        }
        panel.Children.Add(new Viewbox { Child=canvas,Stretch=Stretch.Uniform,MaxWidth=900,HorizontalAlignment=HorizontalAlignment.Left });
        panel.Children.Add(Text($"Minimum {valid.Min(p=>p.Value):0.##} · Maximum {valid.Max(p=>p.Value):0.##} · Zeit seit Sessionstart. Fehlende Werte werden nicht verbunden.",12,print));
        return panel;
    }
    public static void RenderPrintPreview(SessionReport report, string output)
    {
        var document=Print(report,"Vorschau mit synthetischen Testdaten",794,1123);
        var paginator=((IDocumentPaginatorSource)document).DocumentPaginator; paginator.ComputePageCount();
        for (int i=0;i<paginator.PageCount;i++)
        {
            var page=paginator.GetPage(i); var image=new System.Windows.Media.Imaging.RenderTargetBitmap(794,1123,96,96,PixelFormats.Pbgra32);
            image.Render(page.Visual); var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using var stream=System.IO.File.Create(output+".page"+(i+1)+".png"); encoder.Save(stream);
        }
    }
    public static FlowDocument Print(SessionReport report, string note, double pageWidth, double pageHeight)
    {
        var document = new FlowDocument { FontFamily=new FontFamily("Segoe UI"),FontSize=12,Foreground=Brushes.Black,Background=Brushes.White,PageWidth=pageWidth,PageHeight=pageHeight,PagePadding=new Thickness(32),ColumnWidth=double.PositiveInfinity };
        var title = new Paragraph(new Run("VRC HydroLink · Fitness-Bericht")) { FontSize=26 }; document.Blocks.Add(title);
        void Add(FrameworkElement element) { element.Width=Math.Max(200,pageWidth-64); document.Blocks.Add(new BlockUIContainer(element)); }
        Add(Overview(report,true));
        Add(Chart(report,"clean_bpm","Herzfrequenz · bpm",true));
        Add(Chart(report,"clean_speed_kmh","Geschwindigkeit · km/h",true));
        if (!string.IsNullOrWhiteSpace(note)) { document.Blocks.Add(new Paragraph(new Run("Notizen")) { FontSize=18 }); document.Blocks.Add(new Paragraph(new Run(note))); }
        return document;
    }
}
