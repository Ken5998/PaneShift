using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using PaneShift.Core;
using PaneShift.Windows;

namespace PaneShift.Windows.Tests;

public sealed class ReleaseClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(respond(request)); }
    }
    [Theory]
    [InlineData("https://github.com", true)]
    [InlineData("https://release-assets.githubusercontent.com/path", true)]
    [InlineData("https://github.com.evil.test/path", false)]
    [InlineData("http://github.com/path", false)]
    [InlineData("https://github.com:444/path", false)]
    [InlineData("https://user@github.com/path", false)]
    public void RedirectAllowlistIsExact(string uri, bool allowed) => Assert.Equal(allowed, ReleaseClient.IsAllowedHost(new(uri)));

    [Fact]
    public async Task DownloadVerifiesBytesBeforeReturningAndDeletesBadPartial()
    {
        byte[] bytes = [1, 2, 3, 4];
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        using var client = new ReleaseClient(handler);
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var asset = new ReleaseAsset("PaneShift-0.4.0-Setup-x64.msi", new("https://github.com/Ken5998/PaneShift/releases/download/v0.4.0/PaneShift-0.4.0-Setup-x64.msi"),
            4, Convert.ToHexString(SHA256.HashData(bytes)));
        try
        {
            string file = await client.DownloadAsync(asset, directory, default);
            Assert.Equal(bytes, File.ReadAllBytes(file));
            File.Delete(file);
            await Assert.ThrowsAsync<IOException>(() => client.DownloadAsync(asset with { Sha256 = new string('0', 64) }, directory, default));
            Assert.Empty(Directory.GetFiles(directory));
            await Assert.ThrowsAsync<IOException>(() => client.DownloadAsync(asset with { Size = 3 }, directory, default));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ForeignRedirectIsRejectedBeforeFollowingIt()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://evil.test/package.msi") } });
        using var client = new ReleaseClient(handler);
        await Assert.ThrowsAsync<IOException>(() => client.CheckAsync(new(0, 3, 0), default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    public async Task NoReleaseIsDistinctFromRateLimit(HttpStatusCode code, bool error)
    {
        using var handler = new Handler(_ => new(code));
        using var client = new ReleaseClient(handler);
        if (error) await Assert.ThrowsAsync<HttpRequestException>(() => client.CheckAsync(new(0, 3, 0), default));
        else Assert.Null(await client.CheckAsync(new(0, 3, 0), default));
    }
}
