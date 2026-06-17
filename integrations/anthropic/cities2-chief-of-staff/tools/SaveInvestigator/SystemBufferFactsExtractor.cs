using System.Collections.ObjectModel;
using System.Text;

namespace SaveInvestigator;

public static class SystemBufferFactsExtractor
{
    private const string NameSystemTypeName = "Game.UI.NameSystem";
    private const int MaxStringLength = 120;
    private const int MinRawPrintableStringLength = 4;

    public static SystemBufferFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TargetedSaveFacts targetedFacts)
    {
        var systemBufferCatalogFacts = SystemBufferCatalogFactsExtractor.Extract(payload, summary);
        var systemTableFacts = SystemTableFactsExtractor.Extract(payload, summary, systemBufferCatalogFacts);
        return Extract(payload, summary, targetedFacts, systemTableFacts);
    }

    public static SystemBufferFacts Extract(
        byte[] payload,
        SavePreludeSummary summary,
        TargetedSaveFacts targetedFacts,
        SystemTableFacts systemTableFacts)
    {
        var cursor = ArchetypeBufferWalker.Walk(payload, summary);

        var systemBuffers = new List<SystemBufferFact>(summary.SystemTypes.Count);
        NameSystemFact? nameSystem = null;
        var transportLineEntityIndexes = targetedFacts.TransportLines
            .Select(line => line.EntityIndex)
            .ToHashSet();

        for (var systemIndex = 0; systemIndex < summary.SystemTypes.Count; systemIndex += 1)
        {
            var systemType = summary.SystemTypes[systemIndex];
            var buffer = cursor.ReadNextOuterBufferWithMetadata(summary.BufferFormat);
            systemBuffers.Add(
                new SystemBufferFact(
                    systemIndex,
                    systemType.TypeName,
                    buffer.UncompressedSize,
                    buffer.CompressedSize));

            if (nameSystem == null &&
                systemType.TypeName.StartsWith(NameSystemTypeName, StringComparison.Ordinal))
            {
                nameSystem = BuildNameSystemFact(
                    systemIndex,
                    systemType.TypeName,
                    buffer,
                    systemTableFacts.NameSystem?.Entries,
                    transportLineEntityIndexes);
            }
        }

        return new SystemBufferFacts(
            new ReadOnlyCollection<SystemBufferFact>(systemBuffers),
            nameSystem);
    }

    private static NameSystemFact BuildNameSystemFact(
        int systemIndex,
        string typeName,
        OuterBufferBlock buffer,
        IReadOnlyList<NameSystemEntryFact>? exactEntries,
        IReadOnlySet<int> transportLineEntityIndexes)
    {
        var rawPrintableStrings = ExtractRawPrintableStrings(buffer.Data);
        var nameEntries = exactEntries is null
            ? []
            : exactEntries
                .Select(entry => new NameSystemEntryFact(entry.EntityIndex, entry.Value, entry.StringOffset))
                .ToList();
        var candidateNames = nameEntries
            .Select(
                entry => new NameSystemCandidateNameFact(
                    entry.Value,
                    entry.StringOffset,
                    new ReadOnlyCollection<int>([entry.EntityIndex]),
                    new ReadOnlyCollection<int>(
                        transportLineEntityIndexes.Contains(entry.EntityIndex)
                            ? [entry.EntityIndex]
                            : [])))
            .ToList();

        return new NameSystemFact(
            systemIndex,
            typeName,
            buffer.UncompressedSize,
            buffer.CompressedSize,
            new ReadOnlyCollection<RawPrintableStringFact>(rawPrintableStrings),
            new ReadOnlyCollection<NameSystemEntryFact>(nameEntries),
            new ReadOnlyCollection<NameSystemCandidateNameFact>(candidateNames),
            new ReadOnlyCollection<NameSystemCandidateNameFact>(
                candidateNames
                    .Where(candidate => candidate.NearbyTransportLineEntityIndexes.Count > 0)
                    .OrderBy(candidate => candidate.StringOffset)
                    .ToList()));
    }

    private static List<RawPrintableStringFact> ExtractRawPrintableStrings(byte[] buffer)
    {
        var results = new List<RawPrintableStringFact>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        while (index < buffer.Length)
        {
            if (!IsRawPrintableByte(buffer[index]))
            {
                index += 1;
                continue;
            }

            var start = index;
            while (index < buffer.Length && IsRawPrintableByte(buffer[index]))
            {
                index += 1;
            }

            var length = index - start;
            if (length < MinRawPrintableStringLength)
            {
                continue;
            }

            var value = Encoding.UTF8.GetString(buffer, start, length).Trim();
            if (!LooksLikeCandidateName(value))
            {
                continue;
            }

            var key = start.ToString() + "|" + value;
            if (!seen.Add(key))
            {
                continue;
            }

            results.Add(new RawPrintableStringFact(value, start));
        }

        return results;
    }

    private static bool LooksLikeCandidateName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length < 2 || candidate.Length > MaxStringLength)
        {
            return false;
        }

        if (candidate.StartsWith("Game.", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("Colossal.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasLetter = false;
        for (var i = 0; i < candidate.Length; i += 1)
        {
            var character = candidate[i];
            if (char.IsLetter(character))
            {
                hasLetter = true;
            }

            if (char.IsControl(character))
            {
                return false;
            }

            if (!char.IsLetterOrDigit(character) &&
                character is not ' ' and not '-' and not '\'' and not '.' and not '(' and not ')' and not '#' and not '&')
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static bool IsRawPrintableByte(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)' ' or
            (byte)'-' or
            (byte)'\'' or
            (byte)'.' or
            (byte)'(' or
            (byte)')' or
            (byte)'#' or
            (byte)'&';
    }
}
