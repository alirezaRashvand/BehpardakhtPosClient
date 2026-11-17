namespace BehpardakhtPosClient.Protocol;

/// <summary>
/// Iran System encoding used by POS-PC tag 0xB6 (merchant message printed on the receipt).
/// Persian runs are stored in visual order. ASCII digits are mapped to 0x80-0x89.
/// </summary>
public static class IranSystem
{
    private readonly record struct Glyph(byte Isolated, byte Final, byte Initial, byte Medial);

    private static readonly HashSet<char> NonJoining = ['ا', 'آ', 'د', 'ذ', 'ر', 'ز', 'ژ', 'و', 'ء'];

    private static readonly Dictionary<char, Glyph> Glyphs = BuildGlyphs();

    public static byte[] Encode(string text)
    {
        var output = new List<byte>(text.Length);
        var persian = new List<byte>();

        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (current == 'ل' && i + 1 < text.Length && text[i + 1] is 'ا' or 'آ')
            {
                persian.Add(0xF2);
                i++;
                continue;
            }

            if (Glyphs.TryGetValue(current, out var glyph))
            {
                var previousJoins = i > 0 && JoinsForward(text[i - 1]);
                var nextJoins = JoinsForward(current) && i + 1 < text.Length && Glyphs.ContainsKey(text[i + 1]);
                persian.Add((previousJoins, nextJoins) switch
                {
                    (false, false) => glyph.Isolated,
                    (true, false) => glyph.Final,
                    (false, true) => glyph.Initial,
                    (true, true) => glyph.Medial
                });
                continue;
            }

            Flush(persian, output);
            if (current is >= '0' and <= '9')
            {
                output.Add((byte)(0x80 + current - '0'));
            }
            else if (current < 128)
            {
                output.Add((byte)current);
            }
        }

        Flush(persian, output);
        return output.ToArray();
    }

    private static void Flush(List<byte> persian, List<byte> output)
    {
        if (persian.Count == 0)
        {
            return;
        }

        persian.Reverse();
        output.AddRange(persian);
        persian.Clear();
    }

    private static bool JoinsForward(char value) => Glyphs.ContainsKey(value) && !NonJoining.Contains(value);

    private static Dictionary<char, Glyph> BuildGlyphs()
    {
        Glyph Pair(byte end, byte start) => new(end, end, start, start);
        Glyph Same(byte value) => new(value, value, value, value);

        return new Dictionary<char, Glyph>
        {
            ['ا'] = new(0x90, 0x91, 0x90, 0x91),
            ['آ'] = Same(0x8D),
            ['ء'] = Same(0x8F),
            ['ئ'] = Same(0x8E),
            ['ب'] = Pair(0x92, 0x93),
            ['پ'] = Pair(0x94, 0x95),
            ['ت'] = Pair(0x96, 0x97),
            ['ث'] = Pair(0x98, 0x99),
            ['ج'] = Pair(0x9A, 0x9B),
            ['چ'] = Pair(0x9C, 0x9D),
            ['ح'] = Pair(0x9E, 0x9F),
            ['خ'] = Pair(0xA0, 0xA1),
            ['د'] = Same(0xA2),
            ['ذ'] = Same(0xA3),
            ['ر'] = Same(0xA4),
            ['ز'] = Same(0xA5),
            ['ژ'] = Same(0xA6),
            ['س'] = Pair(0xA7, 0xA8),
            ['ش'] = Pair(0xA9, 0xAA),
            ['ص'] = Pair(0xAB, 0xAC),
            ['ض'] = Pair(0xAD, 0xAE),
            ['ط'] = Same(0xAF),
            ['ظ'] = Same(0xE0),
            ['ع'] = new(0xE1, 0xE2, 0xE4, 0xE3),
            ['غ'] = new(0xE5, 0xE6, 0xE8, 0xE7),
            ['ف'] = Pair(0xE9, 0xEA),
            ['ق'] = Pair(0xEB, 0xEC),
            ['ک'] = Pair(0xED, 0xEE),
            ['ك'] = Pair(0xED, 0xEE),
            ['گ'] = Pair(0xEF, 0xF0),
            ['ل'] = Pair(0xF1, 0xF3),
            ['م'] = Pair(0xF4, 0xF5),
            ['ن'] = Pair(0xF6, 0xF7),
            ['و'] = Same(0xF8),
            ['ه'] = new(0xF9, 0xF9, 0xFB, 0xFA),
            ['ی'] = new(0xFD, 0xFC, 0xFE, 0xFE),
            ['ي'] = new(0xFD, 0xFC, 0xFE, 0xFE),
            ['،'] = Same(0x8A),
            ['؟'] = Same(0x8C)
        };
    }
}
