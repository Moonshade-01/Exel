using Exel.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace Exel.Tests;
public class DriveTests
{
    private const string Credentials = """{"installed":{"client_id":"test.apps.googleusercontent.com","client_secret":"synthetic"}}""";
    private sealed record Request(string Method, Uri Uri, string Body, string? Authorization);
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class Store : IDriveSessionStore
    { public string? Value; public Task<string?> LoadAsync()=>Task.FromResult(Value); public Task SaveAsync(string s){Value=s;return Task.CompletedTask;} public Task ClearAsync(){Value=null;return Task.CompletedTask;} }
    private sealed class Handler(Func<Request,HttpResponseMessage> handle) : HttpMessageHandler
    {
        public List<Request> Requests {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)
        { var copy=new Request(r.Method.Method,r.RequestUri!,r.Content is null?"":await r.Content.ReadAsStringAsync(c),r.Headers.Authorization?.ToString()); Requests.Add(copy);return handle(copy); }
    }
    private sealed class Session : IDisposable
    {
        public Clock Clock {get;}=new(); public Store Store {get;}=new(); public Handler Handler {get;} public HttpClient Http {get;} public GoogleDriveService Drive {get;}
        public Uri? AuthorizationUri; private Task? callback;
        public Session(Func<Request,HttpResponseMessage>? api=null,bool validState=true)
        {
            int tokens=0;
            Handler=new Handler(r=>r.Uri.Host=="oauth2.googleapis.com"?Json(JsonSerializer.Serialize(new{access_token="access-"+(++tokens),refresh_token="synthetic-refresh",expires_in=3600})):(api??(_=>Json("{}")))(r));
            Http=new HttpClient(Handler);
            Drive=new GoogleDriveService(uri=>{
                AuthorizationUri=uri;var fields=Fields(uri.Query[1..]);
                string state=validState?fields["state"]:"wrong-state";
                callback=Task.Run(async()=>{using var http=new HttpClient();using var response=await http.GetAsync(fields["redirect_uri"]+"?state="+Uri.EscapeDataString(state)+"&code=synthetic-code");});
                return Task.CompletedTask;
            },Http,Clock,Store);
            Drive.Configure(Credentials);
        }
        public async Task SignIn(){try{await Drive.SignInAsync();}finally{if(callback is not null)await callback;}}
        public void Dispose(){Drive.Dispose();Http.Dispose();}
    }
    private static HttpResponseMessage Json(string text,HttpStatusCode code=HttpStatusCode.OK)=>new(code){Content=new StringContent(text,Encoding.UTF8,"application/json")};
    private static Dictionary<string,string> Fields(string text)=>text.Split('&',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split('=',2)).ToDictionary(x=>WebUtility.UrlDecode(x[0]),x=>WebUtility.UrlDecode(x.Length>1?x[1]:""));
    private static Store Saved()=>new(){Value=JsonSerializer.Serialize(new DriveSession("test.apps.googleusercontent.com","synthetic","saved-refresh"))};
    [Theory]
    [InlineData("{}")][InlineData("null")][InlineData("not json")]
    [InlineData("""{"installed":{}}""")]
    [InlineData("""{"web":{"client_id":"test.apps.googleusercontent.com"}}""")]
    [InlineData("""{"installed":{"client_id":"untrusted.example"}}""")]
    public void InvalidCredentialsProduceFriendlyErrors(string json)
    { using var s=new Session(); Assert.Throws<InvalidDataException>(()=>s.Drive.Configure(json)); }
    [Fact] public async Task DisconnectedServiceDoesNotSendRequests()
    { using var s=new Session(); await Assert.ThrowsAsync<InvalidOperationException>(()=>s.Drive.ListAsync()); Assert.Empty(s.Handler.Requests); }
    [Fact] public async Task AuthorizationUsesPkceStateAndNarrowScope()
    {
        using var s=new Session();await s.SignIn();var q=Fields(s.AuthorizationUri!.Query[1..]);
        Assert.Equal("S256",q["code_challenge_method"]);Assert.Equal("https://www.googleapis.com/auth/drive.file",q["scope"]);Assert.True(q["state"].Length>=32);Assert.StartsWith("http://127.0.0.1:",q["redirect_uri"]);
        var form=Fields(Assert.Single(s.Handler.Requests).Body);string challenge=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]))).TrimEnd('=').Replace('+','-').Replace('/','_');Assert.Equal(challenge,q["code_challenge"]);
    }
    [Fact] public async Task WrongStateNeverExchangesTheCode()
    { using var s=new Session(validState:false);await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SignIn());Assert.Empty(s.Handler.Requests);Assert.False(s.Drive.Connected); }
    [Fact] public async Task ListingFollowsPaginationAndSortsFiles()
    { using var s=new Session(r=>r.Uri.Query.Contains("pageToken=")?Json("""{"files":[{"id":"2","name":"A"}]}"""):Json("""{"nextPageToken":"next page","files":[{"id":"1","name":"Z"}]}"""));await s.SignIn();var list=await s.Drive.ListAsync();Assert.Equal(new[]{"A","Z"},list.Select(f=>f.Name));Assert.Contains("next%20page",s.Handler.Requests.Last().Uri.Query); }
    [Fact] public async Task SaveCreatesAndThenUpdatesTheSameFile()
    { using var s=new Session(_=>Json("""{"id":"cloud-id"}"""));await s.SignIn();var b=new Workbook();b.Set("A1","2^100");Assert.Equal("cloud-id",await s.Drive.SaveAsync(b,null));await s.Drive.SaveAsync(b,"cloud-id");Assert.Equal("POST",s.Handler.Requests[1].Method);Assert.Equal("PATCH",s.Handler.Requests[2].Method);Assert.Contains("integerSheet",s.Handler.Requests[1].Body);Assert.Contains("2^100",s.Handler.Requests[1].Body); }
    [Fact] public async Task DownloadEncodesIdsAndPreservesUnicode()
    { var b=new Workbook{Name="Їжак"};b.Set("A1","2^100");using var s=new Session(_=>Json(WorkbookFile.Serialize(b)));await s.SignIn();var copy=await s.Drive.DownloadAsync("id/with?symbols");Assert.Equal("Їжак",copy.Name);Assert.Equal("2^100",copy.Get("A1"));Assert.Contains("id%2Fwith%3Fsymbols",s.Handler.Requests.Last().Uri.OriginalString); }
    [Fact] public async Task ExpiredTokensAreRefreshedBeforeDriveRequests()
    { using var s=new Session(_=>Json("""{"files":[]}"""));await s.SignIn();s.Clock.Now=s.Clock.Now.AddHours(2);await s.Drive.ListAsync();Assert.Equal("refresh_token",Fields(s.Handler.Requests[1].Body)["grant_type"]);Assert.Equal("Bearer access-2",s.Handler.Requests[2].Authorization); }
    [Fact] public async Task MalformedTokenResponseDoesNotCreateAConnectedSession()
    { using var h=new Handler(_=>Json("""{"access_token":null,"expires_in":3600}"""));using var http=new HttpClient(h);var store=Saved();using var drive=new GoogleDriveService(_=>throw new Exception("Browser must not open"),http,sessionStore:store);await drive.RestoreSessionAsync();await Assert.ThrowsAsync<InvalidDataException>(()=>drive.ListAsync());Assert.Single(h.Requests); }
    [Fact] public async Task InvalidReconfigurationPreservesTheExistingSession()
    { using var s=new Session();await s.SignIn();Assert.Throws<InvalidDataException>(()=>s.Drive.Configure("{}"));Assert.True(s.Drive.Connected); }
    [Fact] public async Task SignInPersistsOnlyRefreshCredentials()
    { using var s=new Session();await s.SignIn();Assert.Contains("synthetic-refresh",s.Store.Value!);Assert.DoesNotContain("access-1",s.Store.Value!); }
    [Fact] public async Task RestoringIsOfflineUntilFirstDriveOperation()
    { using var s=new Session(_=>Json("""{"files":[]}"""));await s.SignIn();using var next=new GoogleDriveService(_=>throw new Exception("Browser must not open"),s.Http,s.Clock,s.Store);s.Handler.Requests.Clear();Assert.True(await next.RestoreSessionAsync());Assert.Empty(s.Handler.Requests);await next.ListAsync();Assert.Equal("refresh_token",Fields(s.Handler.Requests[0].Body)["grant_type"]); }
    [Fact] public async Task SignOutDeletesSavedAuthorization()
    { using var s=new Session();await s.SignIn();await s.Drive.SignOutAsync();Assert.Null(s.Store.Value);Assert.False(s.Drive.Connected);Assert.False(await s.Drive.RestoreSessionAsync()); }
    [Fact] public async Task RevokedRefreshTokenRemovesSavedAuthorization()
    { var store=Saved();using var h=new Handler(_=>Json("""{"error":"invalid_grant"}""",HttpStatusCode.BadRequest));using var http=new HttpClient(h);using var drive=new GoogleDriveService(_=>Task.CompletedTask,http,sessionStore:store);await drive.RestoreSessionAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>drive.ListAsync());Assert.Null(store.Value);Assert.False(drive.Connected); }
    [Fact] public async Task TransientFailureKeepsSavedAuthorization()
    { var store=Saved();using var h=new Handler(_=>Json("{}",HttpStatusCode.InternalServerError));using var http=new HttpClient(h);using var drive=new GoogleDriveService(_=>Task.CompletedTask,http,sessionStore:store);await drive.RestoreSessionAsync();await Assert.ThrowsAsync<HttpRequestException>(()=>drive.ListAsync());Assert.NotNull(store.Value);Assert.True(drive.Connected); }
}
