using System.Buffers.Binary;
namespace HydroLink.Core;
public sealed record BleHeartMeasurement(int Bpm, bool? Contact, ushort? EnergyKj, double[] RrSeconds)
{
    public static BleHeartMeasurement Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) throw new FormatException("Incomplete heart rate measurement");
        byte flags = data[0]; int pos = 1;
        int bpm;
        if ((flags & 1) != 0) { if (data.Length < 3) throw new FormatException("Missing 16-bit BPM"); bpm = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos, 2)); pos += 2; }
        else bpm = data[pos++];
        bool? contact = (flags & 4) != 0 ? (flags & 2) != 0 : null;
        ushort? energy = null;
        if ((flags & 8) != 0) { if (data.Length < pos + 2) throw new FormatException("Missing energy"); energy = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos,2)); pos += 2; }
        var rr = new List<double>();
        if ((flags & 16) != 0)
        {
            if (data.Length == pos || (data.Length - pos) % 2 != 0) throw new FormatException("Incomplete RR intervals");
            while (pos < data.Length) { rr.Add(BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos,2)) / 1024d); pos += 2; }
        }
        return new(bpm, contact, energy, rr.ToArray());
    }
}
