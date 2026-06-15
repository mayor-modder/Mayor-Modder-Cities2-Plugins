using System.IO.Compression;
using System.Text.Json;

namespace SaveInvestigator;

public static class SaveContainerReader
{
    public static async Task<SaveContainer> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var metadataEntry = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".SaveGameMetadata", StringComparison.Ordinal));
        var saveGameDataEntry = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".SaveGameData", StringComparison.Ordinal));

        if (saveGameDataEntry is null)
        {
            throw new InvalidOperationException("The save container does not include a .SaveGameData entry.");
        }

        Dictionary<string, object?>? metadata = null;
        if (metadataEntry is not null)
        {
            await using var metadataStream = metadataEntry.Open();
            metadata = await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(
                metadataStream,
                cancellationToken: cancellationToken);
        }

        byte[] saveGameData;
        await using (var dataStream = saveGameDataEntry.Open())
        using (var memoryStream = new MemoryStream())
        {
            await dataStream.CopyToAsync(memoryStream, cancellationToken);
            saveGameData = memoryStream.ToArray();
        }

        return new SaveContainer(filePath, metadata, saveGameData);
    }
}
