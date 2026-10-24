using Exel.Services;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Exel.Tests;

public class DriveTests
{
    private const string Credentials = """{"installed":{"client_id":"test-client.apps.googleusercontent.com","client_secret":"synthetic-test-value"}}""";
    private sealed record Request(string Method, Uri Uri, string Body, string? Authorization);
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler(Func<Request,HttpResponseMessage> handle) : HttpMessageHandler
    {
        public List<Request> Requests { get; }=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            var copy=new Request(request.Method.Method,request.RequestUri!,request.Content is null?"":await request.Content.ReadAsStringAsync(cancellation),request.Headers.Authorization?.ToString());
            Requests.Add(copy);return handle(copy);
        }
    }
    private sealed class Session : IDisposable
    {
        public Clock Clock {get;}=new();
        public Handler Transport {get;}
        public HttpClient Client {get;}
        public GoogleDriveService Drive {get;}
        public Uri? AuthorizationUri;
        private Task? callback;
        private readonly bool validState;
        public Session(Func<Request,HttpResponseMessage>? handle=null,bool validState=true)
        {
            this.validState=validState;
            int tokens=0;
            Transport=new Handler(request=>request.Uri.Host=="oauth2.googleapis.com"
                ? Json("""{"access_token":"access-TOKEN","refresh_token":"refresh-TOKEN","expires_in":3600}""".Replace("access-TOKEN","access-"+(++tokens)))
                : (handle??(_=>Json("{}")))(request));
            Client=new HttpClient(Transport);
            Drive=new GoogleDriveService(OpenBrowser,Client,Clock);
            Drive.Configure(Credentials);
        }
        private Task OpenBrowser(Uri uri)
        {
            AuthorizationUri=uri;var query=Fields(uri.Query.TrimStart('?'));
            string state=validState?query["state"]:"invalid-state";
            callback=Task.Run(async()=>{using var http=new HttpClient();using var response=await http.GetAsync(query["redirect_uri"]+"?state="+Uri.EscapeDataString(state)+"&code=synthetic-code");});
            return Task.CompletedTask;
        }
        public async Task SignIn(){try{await Drive.SignInAsync();}finally{if(callback is not null)await callback;}}
        public void Dispose(){Drive.Dispose();Client.Dispose();}
    }
    private static Dictionary<string,string> Fields(string encoded)=>encoded.Split('&',StringSplitOptions.RemoveEmptyEntries).Select(p=>p.Split('=',2)).ToDictionary(p=>WebUtility.UrlDecode(p[0]),p=>WebUtility.UrlDecode(p.Length>1?p[1]:""));
    private static HttpResponseMessage Json(string json,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"web":{"client_id":"test.apps.googleusercontent.com"}}""")]
    [InlineData("""{"installed":{"client_id":"untrusted.example"}}""")]
    public void InvalidOAuthConfigurationIsRejected(string json)
    {
        using var s=new Session();Assert.Throws<InvalidDataException>(()=>s.Drive.Configure(json));
    }
    [Fact]
    public async Task DisconnectedServiceRefusesNetworkOperations()
    {
        using var s=new Session();Assert.False(s.Drive.Connected);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.Drive.ListAsync());Assert.Empty(s.Transport.Requests);
    }
    [Fact]
    public async Task OAuthUsesPkceStateAndNarrowScope()
    {
        using var s=new Session();await s.SignIn();Assert.True(s.Drive.Connected);
        var query=Fields(s.AuthorizationUri!.Query[1..]);Assert.Equal("S256",query["code_challenge_method"]);
        Assert.Equal("https://www.googleapis.com/auth/drive.file",query["scope"]);Assert.True(query["state"].Length>=32);
        Assert.StartsWith("http://127.0.0.1:",query["redirect_uri"]);
        var form=Fields(Assert.Single(s.Transport.Requests).Body);Assert.Equal("synthetic-code",form["code"]);Assert.Equal("authorization_code",form["grant_type"]);
        string expected=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]))).TrimEnd('=').Replace('+','-').Replace('/','_');
        Assert.Equal(expected,query["code_challenge"]);
    }
    [Fact]
    public async Task WrongOAuthStateDoesNotExchangeCode()
    {
        using var s=new Session(validState:false);await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SignIn());Assert.Empty(s.Transport.Requests);Assert.False(s.Drive.Connected);
    }
    [Fact]
    public async Task FileListFollowsPaginationAndSortsNames()
    {
        using var s=new Session(r=>r.Uri.Query.Contains("pageToken=")?Json("""{"files":[{"id":"2","name":"A"}]}"""):Json("""{"nextPageToken":"next page","files":[{"id":"1","name":"Z"}]}"""));
        await s.SignIn();var list=await s.Drive.ListAsync();Assert.Equal(new[]{"A","Z"},list.Select(f=>f.Name));
        var requests=s.Transport.Requests.Where(r=>r.Uri.Host=="www.googleapis.com").ToArray();Assert.Equal(2,requests.Length);
        Assert.Equal("Bearer access-1",requests[0].Authorization);Assert.Contains("trashed = false",Fields(requests[0].Uri.Query[1..])["q"]);
        Assert.Equal("next page",Fields(requests[1].Uri.Query[1..])["pageToken"]);
    }
    [Fact]
    public async Task SaveCreatesThenUpdatesSameCloudId()
    {
        using var s=new Session(_=>Json("""{"id":"cloud-id"}"""));await s.SignIn();var b=new Workbook{Name="Їжак"};b.Set("A1","=5^8");
        Assert.Equal("cloud-id",await s.Drive.SaveAsync(b,null));Assert.Equal("cloud-id",await s.Drive.SaveAsync(b,"cloud-id"));
        var requests=s.Transport.Requests.Where(r=>r.Uri.Host=="www.googleapis.com").ToArray();Assert.Equal("POST",requests[0].Method);Assert.Equal("PATCH",requests[1].Method);
        Assert.EndsWith("/cloud-id",requests[1].Uri.AbsolutePath);Assert.Contains("=5^8",requests[0].Body);Assert.Contains("integerSheet",requests[0].Body);
        Assert.Contains("uploadType=multipart",requests[0].Uri.Query);
    }
    [Fact]
    public async Task DownloadRebuildsWorkbookAndEncodesId()
    {
        var b=new Workbook{Name="Хмара"};b.Set("A1","2^100");using var s=new Session(_=>Json(WorkbookFile.Serialize(b)));await s.SignIn();
        var actual=await s.Drive.DownloadAsync("id/with?symbols");Assert.Equal("Хмара",actual.Name);Assert.Equal("2^100",actual.Get("A1"));
        Assert.Contains("id%2Fwith%3Fsymbols",s.Transport.Requests.Last().Uri.OriginalString);
    }
    [Fact]
    public async Task ExpiredAccessTokenRefreshesBeforeRequest()
    {
        using var s=new Session(_=>Json("""{"files":[]}"""));await s.SignIn();s.Clock.Now=s.Clock.Now.AddHours(2);await s.Drive.ListAsync();
        var tokens=s.Transport.Requests.Where(r=>r.Uri.Host=="oauth2.googleapis.com").ToArray();Assert.Equal(2,tokens.Length);
        Assert.Equal("refresh_token",Fields(tokens[1].Body)["grant_type"]);Assert.Equal("refresh-TOKEN",Fields(tokens[1].Body)["refresh_token"]);
        Assert.Equal("Bearer access-2",s.Transport.Requests.Last().Authorization);
    }
    [Theory][InlineData(HttpStatusCode.Unauthorized)][InlineData(HttpStatusCode.Forbidden)][InlineData(HttpStatusCode.InternalServerError)]
    public async Task DriveErrorsDoNotBecomeSuccessfulResults(HttpStatusCode status)
    {
        using var s=new Session(_=>Json("{}",status));await s.SignIn();var error=await Assert.ThrowsAsync<HttpRequestException>(()=>s.Drive.ListAsync());Assert.Contains(((int)status).ToString(),error.Message);
    }
    [Fact]
    public async Task DisconnectClearsSession()
    {
        using var s=new Session();await s.SignIn();s.Drive.Disconnect();Assert.False(s.Drive.Connected);await Assert.ThrowsAsync<InvalidOperationException>(()=>s.Drive.ListAsync());
    }
    [Fact]
    public async Task ReconfiguringAccountDropsOldTokens()
    {
        using var s=new Session();await s.SignIn();s.Drive.Configure(Credentials);Assert.False(s.Drive.Connected);
    }
}
