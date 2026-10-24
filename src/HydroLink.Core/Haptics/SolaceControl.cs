using Buttplug.Core.Messages;
namespace VrcHapticsBridge.Core;

public sealed record SolaceSettings(bool PositionMode, double StrokeStart, double StrokeEnd, double Position, double TravelTime)
{
    public uint DurationMilliseconds => (uint)Math.Round(100 + TravelTime * 4900);
}

public sealed partial class UniversalHaptics
{
    private bool solaceControl, solaceMoving, solaceNextHigh = true;
    private DateTimeOffset solaceNextMove;
    private double? solaceLastPosition;
    private uint solaceLastDuration;
    public SolaceSettings ActiveSolaceSettings { get { lock (sync) return ReadSolaceSettings(); } }
    private SolaceSettings ReadSolaceSettings()
    {
        var p = manualToy ? "Manual" : "";
        return new(controls.GetValueOrDefault(p + "PositionMode") >= .5,
            controls.GetValueOrDefault(p + "StrokeStart"), controls.GetValueOrDefault(p + "StrokeEnd", 1),
            controls.GetValueOrDefault(p + "Position", manualToy ? .25 : 1), controls.GetValueOrDefault(p + "TravelTime", 400d / 4900));
    }
    private void InitSolaceControls()
    {
        if (!solaceControl) return;
        foreach (var p in new[] { "", "Manual" })
        {
            controls[p + "StrokeEnd"] = 1;
            controls[p + "Position"] = p.Length == 0 ? 1 : .25;
            controls[p + "TravelTime"] = 400d / 4900;
        }
    }
    private void ResetSolaceMovement()
    {
        solaceMoving = false; solaceNextHigh = true; solaceNextMove = default;
        solaceLastPosition = null; solaceLastDuration = 0;
    }
    private void StopSolaceMovement()
    {
        generation++;
        foreach (var r in routes) pendingStops.Add(r.Feature.DeviceId);
    }
    private bool SetSolaceControl(string channel, double value)
    {
        if (!solaceControl) return false;
        var key = channel.StartsWith("Manual", StringComparison.Ordinal) ? channel[6..] : channel;
        if (key is not ("StrokeStart" or "StrokeEnd" or "Position" or "TravelTime" or "PositionMode")) return false;
        value = key == "PositionMode" ? value >= .5 ? 1 : 0 : Math.Clamp(value, 0, 1);
        var old = controls.GetValueOrDefault(channel);
        controls[channel] = value;
        // Stop before changing mode or bounds; never overlap autonomous oscillation with positioning.
        if (old != value && (key is "PositionMode" or "StrokeStart" or "StrokeEnd")
            && channel.StartsWith("Manual", StringComparison.Ordinal) == manualToy) StopSolaceMovement();
        return true;
    }
    public void SetActiveSolaceControl(string key, double value)
    {
        lock (sync) SetSpsChannel((manualToy ? "Manual" : "") + key, value);
    }
    // Called only under the same serialized output gate as normal toy writes and device stops.
    private async Task<bool> TickSolaceAsync()
    {
        HapticFeature? positionFeature = null;
        double target = 0;
        uint duration = 0;
        long ticket = 0;
        bool needsStop = false, cycle = false;
        lock (sync)
        {
            if (!solaceControl) return false;
            if (!armed || routes.Length == 0) return true;
            if (!backend.Connected || routes.Any(r => !backend.Features.Contains(r.Feature)))
            { Disarm("Gerätefunktionen geändert"); needsStop = true; }
            else
            {
                var settings = ReadSolaceSettings();
                var bounded = settings.StrokeStart > 0 || settings.StrokeEnd < 1;
                if (!settings.PositionMode && !bounded) { status = "Hubbetrieb · native Geschwindigkeit"; return false; }
                var route = routes[0];
                positionFeature = backend.Features.FirstOrDefault(f => f.DeviceId == route.Feature.DeviceId
                    && f.FeatureId == route.Feature.FeatureId && f.Type == OutputType.HwPositionWithDuration && f.Supported);
                var signal = manualToy ? 1 : sourceLevel;
                var speed = Desired(route);
                var usable = positionFeature != null && settings.StrokeStart < settings.StrokeEnd;
                var active = (avatarEnabled || desktopManual && manualToy) && signal > 0 && (settings.PositionMode || speed > 0);
                if (!usable || !active)
                {
                    status = !usable ? "Position nicht verfügbar oder Hubgrenzen ungültig" : "Warte auf Freigabe oder Kontakt";
                    if (solaceMoving) { StopSolaceMovement(); needsStop = true; }
                    else return true;
                }
                else
                {
                    cycle = !settings.PositionMode;
                    duration = cycle ? (uint)Math.Round(200 + (1 - Math.Clamp(speed, 0, 1)) * 1800) : settings.DurationMilliseconds;
                    duration = (uint)Math.Clamp((long)duration, Math.Max(100, positionFeature!.DurationMin), Math.Min(5000, positionFeature.DurationMax));
                    target = settings.PositionMode
                        ? settings.StrokeStart + (settings.StrokeEnd - settings.StrokeStart) * settings.Position * signal
                        : solaceNextHigh ? settings.StrokeEnd : settings.StrokeStart;
                    if (time.GetUtcNow() < solaceNextMove) return true;
                    if (!cycle && solaceLastPosition == target && solaceLastDuration == duration) return true;
                    ticket = generation;
                    status = cycle ? "Begrenzter Hub · softwaregesteuert" : "Positionssteuerung";
                }
            }
        }
        if (needsStop) { await FlushStops(); return true; }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await backend.SetPositionAsync(positionFeature!, target, duration, timeout.Token);
        bool flush;
        lock (sync)
        {
            if (ticket == generation)
            {
                solaceMoving = true; solaceLastPosition = target; solaceLastDuration = duration;
                sent[positionFeature!.Key] = target;
                solaceNextMove = time.GetUtcNow().AddMilliseconds(Math.Max(duration, Math.Max(50, positionFeature.TimingGap)));
                if (cycle) solaceNextHigh = !solaceNextHigh;
            }
            flush = pendingStops.Count > 0;
        }
        if (flush) await FlushStops();
        return true;
    }
}
