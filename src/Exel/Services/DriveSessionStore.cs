using Microsoft.Maui.Storage;
namespace Exel.Services;

public sealed class DriveSessionStore : IDriveSessionStore
{
    private const string Key = "exel.google-drive.session";
    public Task<string?> LoadAsync() => SecureStorage.Default.GetAsync(Key);
    public Task SaveAsync(string session) => SecureStorage.Default.SetAsync(Key, session);
    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
