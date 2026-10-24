using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Exel.Services;

internal sealed class LoopbackCallback : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    public string RedirectUri { get; }
    public LoopbackCallback()
    {
        listener.Start();
        RedirectUri = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
    }
    public async Task<Dictionary<string,string>> ReceiveAsync(string expectedState, CancellationToken cancellation)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellation);
        await using var stream = client.GetStream();
        using var bytes = new MemoryStream();
        var buffer = new byte[1];
        string header = "";
        while (bytes.Length < 8192)
        {
            int count = await stream.ReadAsync(buffer, cancellation);
            if (count == 0) throw new InvalidDataException("Порожня відповідь OAuth.");
            bytes.WriteByte(buffer[0]);
            if (buffer[0] == '\n')
            {
                header = Encoding.ASCII.GetString(bytes.GetBuffer(),0,(int)bytes.Length);
                if (header.EndsWith("\r\n\r\n",StringComparison.Ordinal)) break;
            }
        }
        if (!header.EndsWith("\r\n\r\n",StringComparison.Ordinal)) throw new InvalidDataException("Відповідь OAuth перевищує ліміт заголовків.");
        string[] firstLine = header.Split("\r\n",2)[0].Split(' ');
        var fields = new Dictionary<string,string>(StringComparer.Ordinal);
        if (firstLine.Length == 3 && firstLine[0] == "GET" && firstLine[1].StartsWith("/?",StringComparison.Ordinal))
        {
            foreach (string item in firstLine[1][2..].Split('&',StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = item.Split('=',2);
                if (!fields.TryAdd(WebUtility.UrlDecode(pair[0]),WebUtility.UrlDecode(pair.Length > 1 ? pair[1] : "")))
                    throw new InvalidDataException("Повторені параметри відповіді OAuth.");
            }
        }
        bool valid = fields.GetValueOrDefault("state") == expectedState && !string.IsNullOrEmpty(fields.GetValueOrDefault("code"));
        byte[] body = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><p>Поверніться до Exel. Цю вкладку можна закрити.</p>");
        string response = $"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response),cancellation);
        await stream.WriteAsync(body,cancellation);
        return fields;
    }
    public void Dispose() => listener.Stop();
}
