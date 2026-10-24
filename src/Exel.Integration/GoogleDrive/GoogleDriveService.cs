using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Exel.Core;
namespace Exel.Services;

public sealed record DriveFile(string Id, string Name);
public sealed class GoogleDriveService : IDisposable
{
    private readonly HttpClient http;
    private readonly bool ownsHttp;
    private readonly Func<Uri, Task> openBrowser;
    private readonly TimeProvider clock;
    public GoogleDriveService(Func<Uri, Task> openBrowser, HttpClient? http = null, TimeProvider? clock = null)
    {
        this.openBrowser = openBrowser;
        this.http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        ownsHttp = http is null;
        this.clock = clock ?? TimeProvider.System;
    }
    private string clientId = "", clientSecret = "", accessToken = "", refreshToken = "";
    private DateTimeOffset expires;
    public bool Connected => accessToken.Length > 0;
    public void Configure(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("installed", out var installed))
            throw new InvalidDataException("Потрібен OAuth JSON типу Desktop app (поле installed).");
        clientId = installed.GetProperty("client_id").GetString() ?? "";
        clientSecret = installed.TryGetProperty("client_secret", out var secret) ? secret.GetString() ?? "" : "";
        if (!clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)) throw new InvalidDataException("Некоректний client_id.");
        Disconnect();
    }
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public async Task SignInAsync()
    {
        if (clientId.Length == 0) throw new InvalidOperationException("Спочатку імпортуйте OAuth JSON.");
        using var callback = new LoopbackCallback();
        string redirect = callback.RedirectUri;
        string state = Base64(RandomNumberGenerator.GetBytes(32)), verifier = Base64(RandomNumberGenerator.GetBytes(48));
        string challenge = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var values = new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["redirect_uri"] = redirect, ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/drive.file", ["state"] = state,
            ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["access_type"] = "offline", ["prompt"] = "consent"
        };
        var query = string.Join("&", values.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        await openBrowser(new Uri("https://accounts.google.com/o/oauth2/v2/auth?" + query));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var reply = await callback.ReceiveAsync(state, timeout.Token);
        bool valid = reply.GetValueOrDefault("state") == state;
        string? code = reply.GetValueOrDefault("code");
        if (!valid || string.IsNullOrEmpty(code)) throw new InvalidOperationException("Вхід скасовано або відповідь OAuth некоректна.");
        await TokenAsync(new()
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier
        });
    }
    private async Task TokenAsync(Dictionary<string, string> fields)
    {
        fields["client_id"] = clientId;
        if (clientSecret.Length > 0) fields["client_secret"] = clientSecret;
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(fields));
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Google OAuth: HTTP {(int)response.StatusCode}. Перевірте налаштування OAuth або увійдіть знову.");
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        accessToken = token.RootElement.GetProperty("access_token").GetString()!;
        if (token.RootElement.TryGetProperty("refresh_token", out var refresh)) refreshToken = refresh.GetString()!;
        expires = clock.GetUtcNow().AddSeconds(token.RootElement.GetProperty("expires_in").GetInt32() - 60);
    }
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        if (!Connected) throw new InvalidOperationException("Спочатку увійдіть у Google Drive.");
        if (clock.GetUtcNow() >= expires)
        {
            if (refreshToken.Length == 0) throw new InvalidOperationException("Сеанс завершено. Увійдіть знову.");
            await TokenAsync(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken });
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode; response.Dispose();
            throw new HttpRequestException($"Google Drive: HTTP {(int)status}. Перевірте доступ, мережу та увімкнення Drive API.");
        }
        return response;
    }
    public async Task<List<DriveFile>> ListAsync()
    {
        List<DriveFile> files = []; string? page = null;
        do
        {
            string q = "trashed = false and appProperties has { key='integerSheet' and value='1' }";
            string url = "https://www.googleapis.com/drive/v3/files?pageSize=100&fields=nextPageToken,files(id,name)&q=" + Uri.EscapeDataString(q);
            if (page is not null) url += "&pageToken=" + Uri.EscapeDataString(page);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SendAsync(request);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var f in doc.RootElement.GetProperty("files").EnumerateArray()) files.Add(new(f.GetProperty("id").GetString()!, f.GetProperty("name").GetString()!));
            page = doc.RootElement.TryGetProperty("nextPageToken", out var p) ? p.GetString() : null;
        } while (page is not null);
        return files.OrderBy(f => f.Name).ToList();
    }
    public async Task<Workbook> DownloadAsync(string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(id)}?alt=media");
        using var response = await SendAsync(request);
        return WorkbookFile.Deserialize(await response.Content.ReadAsStringAsync());
    }
    public async Task<string> SaveAsync(Workbook workbook, string? existingId)
    {
        string metadata = JsonSerializer.Serialize(new { name = workbook.Name + ".isheet.json", appProperties = new { integerSheet = "1" } });
        using var content = new MultipartContent("related");
        content.Add(new StringContent(metadata, Encoding.UTF8, "application/json"));
        content.Add(new StringContent(WorkbookFile.Serialize(workbook), Encoding.UTF8, "application/json"));
        string suffix = existingId is null ? "" : "/" + Uri.EscapeDataString(existingId);
        using var request = new HttpRequestMessage(existingId is null ? HttpMethod.Post : HttpMethod.Patch,
            "https://www.googleapis.com/upload/drive/v3/files" + suffix + "?uploadType=multipart&fields=id") { Content = content };
        using var response = await SendAsync(request);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }
    public void Disconnect() { accessToken = ""; refreshToken = ""; }
    public void Dispose() { Disconnect(); if (ownsHttp) http.Dispose(); }
}
