namespace BehpardakhtPosClient.Protocol;

/// <summary>
/// CRC-16/ARC (poly 0x8005 reflected, init 0). POS-PC stores the value little-endian.
/// </summary>
public static class Crc16
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)(((crc & 1) != 0) ? (crc >> 1) ^ 0xA001 : crc >> 1);
            }
        }

        return crc;
    }

    public static byte[] LittleEndian(ReadOnlySpan<byte> data)
    {
        var crc = Compute(data);
        return [(byte)crc, (byte)(crc >> 8)];
    }
}
