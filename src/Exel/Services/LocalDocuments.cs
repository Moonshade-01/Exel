using System.Text.Json;
namespace Exel.Services;

internal sealed class LocalDocuments
{
    private const string RecentKey = "exel.recent.v2", FolderKey = "exel.folder.v2";
    public string? Folder => Preferences.Default.Get(FolderKey, "") is { Length: > 0 } folder && Directory.Exists(folder) ? folder : null;
    public void SetFolder(string path) => Preferences.Default.Set(FolderKey, path);
    public IReadOnlyList<string> Recent()
    {
        try { return (JsonSerializer.Deserialize<string[]>(Preferences.Default.Get(RecentKey, "[]")) ?? []).Where(File.Exists).Take(7).ToArray(); }
        catch (JsonException) { return []; }
    }
    public void Remember(string path)
    {
        var all = new[] { path }.Concat(Recent()).Distinct(StringComparer.OrdinalIgnoreCase).Take(7).ToArray();
        Preferences.Default.Set(RecentKey, JsonSerializer.Serialize(all));
    }
    public async Task<string[]> FolderFilesAsync()
    {
        string? folder = Folder;
        if (folder is null) return [];
        return await Task.Run(() => Directory.EnumerateFiles(folder, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(12).ToArray());
    }
}
