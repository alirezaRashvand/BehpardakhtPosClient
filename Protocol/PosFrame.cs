using System.Globalization;
using System.Text;
using BehpardakhtPosClient;

namespace BehpardakhtPosClient.Protocol;

public enum PosFraming
{
    /// <summary>
    /// Protocol revision 4.7: 4 ASCII digits of body length, TLV body, CRC-16 little-endian.
    /// </summary>
    Protocol47,

    /// <summary>
    /// POS_PC.dll 1.4.10: 4 ASCII digits of the whole frame length, TLV body, CRC-16 little-endian.
    /// </summary>
    LegacyLibrary
}

public readonly record struct Tlv(byte Tag, byte[] Value);

public sealed class PosMessage
{
    public PosMessage(IReadOnlyList<Tlv> fields)
    {
        Fields = fields;
    }

    public IReadOnlyList<Tlv> Fields { get; }

    public byte[]? Get(byte tag)
    {
        for (var i = Fields.Count - 1; i >= 0; i--)
        {
            if (Fields[i].Tag == tag)
            {
                return Fields[i].Value;
            }
        }

        return null;
    }

    public string? Ascii(byte tag)
    {
        var value = Get(tag);
        return value is null ? null : Encoding.ASCII.GetString(value);
    }

    public string? Bcd(byte tag, bool significant = true)
    {
        var value = Get(tag);
        if (value is null || !global::BehpardakhtPosClient.Protocol.Bcd.TryDecode(value, out var digits))
        {
            return null;
        }

        return significant ? global::BehpardakhtPosClient.Protocol.Bcd.Significant(digits) : digits;
    }
}

public readonly record struct FrameDecode(int ErrorCode, PosMessage? Message);

public static class PosFrame
{
    public const int MaxBodyLength = 4096;

    public static byte[] Encode(ReadOnlySpan<byte> body, PosFraming framing)
    {
        if (body.Length > MaxBodyLength)
        {
            throw new ArgumentException("POS-PC body is too long.", nameof(body));
        }

        var crc = Crc16.LittleEndian(body);
        var totalLength = framing == PosFraming.Protocol47 ? body.Length : 4 + body.Length + crc.Length;
        if (totalLength > 9999)
        {
            throw new ArgumentException("POS-PC frame length does not fit in 4 ASCII digits.");
        }

        var length = Encoding.ASCII.GetBytes(totalLength.ToString("D4", CultureInfo.InvariantCulture));
        var frame = new byte[4 + body.Length + crc.Length];
        length.CopyTo(frame, 0);
        body.CopyTo(frame.AsSpan(4));
        crc.CopyTo(frame.AsSpan(4 + body.Length));
        return frame;
    }

    public static byte[] Encode(IReadOnlyList<Tlv> fields, PosFraming framing)
    {
        var body = new MemoryStream();
        foreach (var field in fields)
        {
            WriteTlv(body, field.Tag, field.Value);
        }

        return Encode(body.ToArray(), framing);
    }

    public static FrameDecode Decode(ReadOnlySpan<byte> frame, PosFraming framing)
    {
        if (frame.Length < 6 || !TryReadLength(frame, out var declared))
        {
            return new FrameDecode((int)ReturnCode.ERR_PC_INVALID_REC_SIZE, null);
        }

        var bodyLength = framing == PosFraming.Protocol47 ? declared : declared - 6;
        var expected = 4 + bodyLength + 2;
        if (bodyLength < 0 || bodyLength > MaxBodyLength || frame.Length != expected)
        {
            return new FrameDecode((int)ReturnCode.ERR_PC_INVALID_REC_SIZE, null);
        }

        var body = frame.Slice(4, bodyLength);
        var actualCrc = Crc16.LittleEndian(body);
        if (frame[^2] != actualCrc[0] || frame[^1] != actualCrc[1])
        {
            return new FrameDecode((int)ReturnCode.ERR_POS_PC_CRCERROR_INVALID_DATA, null);
        }

        if (!TryReadTlvs(body, out var fields))
        {
            return new FrameDecode((int)ReturnCode.ERR_PC_PREPARE_TLV_MSG_FAILED, null);
        }

        return new FrameDecode(0, new PosMessage(fields));
    }

    public static bool TryMatch(ReadOnlySpan<byte> buffer, out PosFraming framing, out int frameLength)
    {
        framing = default;
        frameLength = 0;
        if (!TryReadLength(buffer, out var declared))
        {
            return false;
        }

        if (declared is >= 6 and <= 9999 && declared <= buffer.Length && CrcMatches(buffer[..declared], PosFraming.LegacyLibrary))
        {
            framing = PosFraming.LegacyLibrary;
            frameLength = declared;
            return true;
        }

        var protocolLength = declared + 6;
        if (declared is >= 0 and <= MaxBodyLength && protocolLength <= buffer.Length && CrcMatches(buffer[..protocolLength], PosFraming.Protocol47))
        {
            framing = PosFraming.Protocol47;
            frameLength = protocolLength;
            return true;
        }

        return false;
    }

    private static bool CrcMatches(ReadOnlySpan<byte> frame, PosFraming framing)
    {
        if (!TryReadLength(frame, out var declared))
        {
            return false;
        }

        var bodyLength = framing == PosFraming.Protocol47 ? declared : declared - 6;
        if (bodyLength < 0 || frame.Length != 4 + bodyLength + 2)
        {
            return false;
        }

        var actual = Crc16.LittleEndian(frame.Slice(4, bodyLength));
        return frame[^2] == actual[0] && frame[^1] == actual[1];
    }

    public static bool IsComplete(ReadOnlySpan<byte> buffer, PosFraming framing)
    {
        if (!TryReadLength(buffer, out var declared))
        {
            return false;
        }

        var bodyLength = framing == PosFraming.Protocol47 ? declared : declared - 6;
        if (bodyLength < 0 || bodyLength > MaxBodyLength)
        {
            return false;
        }

        return buffer.Length >= 4 + bodyLength + 2;
    }

    private static bool TryReadLength(ReadOnlySpan<byte> buffer, out int length)
    {
        length = 0;
        if (buffer.Length < 4)
        {
            return false;
        }

        Span<char> chars = stackalloc char[4];
        for (var i = 0; i < 4; i++)
        {
            var value = buffer[i];
            if (value is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            chars[i] = (char)value;
        }

        return int.TryParse(chars, NumberStyles.None, CultureInfo.InvariantCulture, out length);
    }

    private static void WriteTlv(Stream stream, byte tag, ReadOnlySpan<byte> value)
    {
        stream.WriteByte(tag);
        if (tag == PosTags.MultiPaymentDetails && value.Length >= 255)
        {
            stream.WriteByte(255);
            stream.WriteByte((byte)value.Length);
            stream.WriteByte((byte)(value.Length >> 8));
        }
        else
        {
            if (value.Length > 254)
            {
                throw new ArgumentException($"TLV tag 0x{tag:X2} is longer than 254 bytes.");
            }

            stream.WriteByte((byte)value.Length);
        }

        stream.Write(value);
    }

    private static bool TryReadTlvs(ReadOnlySpan<byte> body, out List<Tlv> fields)
    {
        fields = [];
        var index = 0;
        while (index < body.Length)
        {
            var tag = body[index++];
            if (index >= body.Length)
            {
                return false;
            }

            var length = (int)body[index++];
            if (tag == PosTags.MultiPaymentDetails && length == 255)
            {
                if (index + 2 > body.Length)
                {
                    return false;
                }

                length = body[index] | (body[index + 1] << 8);
                index += 2;
            }

            if (length < 0 || index + length > body.Length)
            {
                return false;
            }

            fields.Add(new Tlv(tag, body.Slice(index, length).ToArray()));
            index += length;
        }

        return true;
    }
}
