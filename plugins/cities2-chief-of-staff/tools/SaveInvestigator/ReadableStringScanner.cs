using System.Text;

namespace SaveInvestigator;

public static class ReadableStringScanner
{
    public static List<ReadableStringScanFact> Scan(byte[] buffer, int minimumLength)
    {
        var results = new List<ReadableStringScanFact>();
        results.AddRange(ScanAscii(buffer, minimumLength));
        results.AddRange(ScanUtf16Le(buffer, minimumLength));
        return results
            .OrderBy(item => item.Offset)
            .ThenBy(item => item.Encoding, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ReadableStringScanFact> ScanAscii(byte[] buffer, int minimumLength)
    {
        var results = new List<ReadableStringScanFact>();
        for (var index = 0; index < buffer.Length;)
        {
            if (!IsPrintableAscii(buffer[index]))
            {
                index += 1;
                continue;
            }

            var start = index;
            while (index < buffer.Length && IsPrintableAscii(buffer[index]))
            {
                index += 1;
            }

            var length = index - start;
            if (length < minimumLength)
            {
                continue;
            }

            results.Add(
                new ReadableStringScanFact(
                    "ascii",
                    Encoding.ASCII.GetString(buffer, start, length),
                    start));
        }

        return results;
    }

    private static List<ReadableStringScanFact> ScanUtf16Le(byte[] buffer, int minimumLength)
    {
        var results = new List<ReadableStringScanFact>();
        for (var index = 0; index + 1 < buffer.Length;)
        {
            if (!IsUtf16LePrintableAsciiPair(buffer, index))
            {
                index += 1;
                continue;
            }

            var start = index;
            var builder = new StringBuilder();
            while (index + 1 < buffer.Length && IsUtf16LePrintableAsciiPair(buffer, index))
            {
                builder.Append((char)buffer[index]);
                index += 2;
            }

            if (builder.Length < minimumLength)
            {
                continue;
            }

            results.Add(
                new ReadableStringScanFact(
                    "utf16le",
                    builder.ToString(),
                    start));
        }

        return results;
    }

    private static bool IsPrintableAscii(byte value)
    {
        return value is >= 32 and <= 126;
    }

    private static bool IsUtf16LePrintableAsciiPair(byte[] buffer, int offset)
    {
        return offset + 1 < buffer.Length &&
               IsPrintableAscii(buffer[offset]) &&
               buffer[offset + 1] == 0;
    }
}

public sealed record ReadableStringScanFact(
    string Encoding,
    string Value,
    int Offset);
