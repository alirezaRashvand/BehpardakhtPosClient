namespace BehpardakhtPosClient.Protocol;

public static class Bcd
{
    public static byte[] Encode(string digits)
    {
        if (digits.Length == 0)
        {
            return [];
        }

        foreach (var ch in digits)
        {
            if (ch is < '0' or > '9')
            {
                throw new FormatException($"'{digits}' is not a decimal BCD string.");
            }
        }

        if ((digits.Length & 1) == 1)
        {
            digits = "0" + digits;
        }

        var bytes = new byte[digits.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var hi = digits[i * 2] - '0';
            var lo = digits[(i * 2) + 1] - '0';
            bytes[i] = (byte)((hi << 4) | lo);
        }

        return bytes;
    }

    public static bool TryDecode(ReadOnlySpan<byte> data, out string digits)
    {
        var chars = new char[data.Length * 2];
        for (var i = 0; i < data.Length; i++)
        {
            var hi = data[i] >> 4;
            var lo = data[i] & 0x0F;
            if (hi > 9 || lo > 9)
            {
                digits = "";
                return false;
            }

            chars[i * 2] = (char)('0' + hi);
            chars[(i * 2) + 1] = (char)('0' + lo);
        }

        digits = new string(chars);
        return true;
    }

    public static string Significant(string digits)
    {
        var trimmed = digits.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
}
