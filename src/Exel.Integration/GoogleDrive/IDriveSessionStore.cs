namespace Exel.Services;

public sealed record DriveSession(string ClientId, string ClientSecret, string RefreshToken);

public interface IDriveSessionStore
{
    Task<string?> LoadAsync();
    Task SaveAsync(string session);
    Task ClearAsync();
}
