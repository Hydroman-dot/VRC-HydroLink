using System.Buffers.Binary;
using System.Text;

namespace HydroLink.Core;

public sealed record OscMessage(string Address, object[] Arguments);
public static class OscCodec
{
    public static byte[] Encode(string address, params object[] args)
    {
        if (!address.StartsWith('/')) throw new ArgumentException("OSC address must start with /");
        using var stream = new MemoryStream();
        void Text(string value) { var bytes = Encoding.UTF8.GetBytes(value); stream.Write(bytes); stream.WriteByte(0); while (stream.Length % 4 != 0) stream.WriteByte(0); }
        Text(address);
        Text("," + string.Concat(args.Select(a => a switch { int => "i", float or double => "f", bool b => b ? "T" : "F", string => "s", _ => throw new ArgumentException("Unsupported OSC type") })));
        foreach (var a in args)
        {
            var bytes = new byte[4];
            switch (a)
            {
                case int i: BinaryPrimitives.WriteInt32BigEndian(bytes, i); stream.Write(bytes); break;
                case float or double: BinaryPrimitives.WriteSingleBigEndian(bytes, Convert.ToSingle(a)); stream.Write(bytes); break;
                case string s: Text(s); break;
            }
        }
        return stream.ToArray();
    }
    public static IReadOnlyList<OscMessage> Decode(byte[] packet) => DecodePacket(packet, 0);
    private static List<OscMessage> DecodePacket(byte[] packet, int depth)
    {
        if (depth > 16) throw new FormatException("OSC nesting limit");
        int offset = 0;
        string Text()
        {
            var end = Array.IndexOf(packet, (byte)0, offset);
            if (end < 0) throw new FormatException("Unterminated OSC string");
            var value = Encoding.UTF8.GetString(packet, offset, end - offset);
            offset = (end + 4) & ~3;
            if (offset > packet.Length) throw new FormatException("Truncated OSC string");
            return value;
        }
        void Need(int n) { if (n < 0 || offset > packet.Length - n) throw new FormatException("Truncated OSC packet"); }
        var address = Text();
        if (address == "#bundle")
        {
            Need(8); offset += 8;
            var messages = new List<OscMessage>();
            while (offset < packet.Length)
            {
                Need(4); int size = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset)); offset += 4;
                Need(size); if (size == 0) throw new FormatException("Empty bundle item");
                messages.AddRange(DecodePacket(packet.AsSpan(offset, size).ToArray(), depth + 1)); offset += size;
            }
            return messages;
        }
        if (!address.StartsWith('/')) throw new FormatException("Invalid OSC address");
        var tags = Text(); if (!tags.StartsWith(',')) throw new FormatException("Missing OSC type tags");
        var args = new List<object>();
        foreach (var tag in tags.Skip(1))
            switch (tag)
            {
                case 'i': Need(4); args.Add(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset))); offset += 4; break;
                case 'f':
                    Need(4); var number = BinaryPrimitives.ReadSingleBigEndian(packet.AsSpan(offset));
                    if (!float.IsFinite(number)) throw new FormatException("Non-finite OSC number");
                    args.Add(number); offset += 4; break;
                case 's': args.Add(Text()); break;
                case 'T': args.Add(true); break;
                case 'F': args.Add(false); break;
                default: throw new FormatException($"Unsupported OSC tag: {tag}");
            }
        return [new(address, args.ToArray())];
    }
}
