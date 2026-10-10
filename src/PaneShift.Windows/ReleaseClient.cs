using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PaneShift.Core;

namespace PaneShift.Windows;

public interface IReleaseClient
{
    Task<AvailableRelease?> CheckAsync(Version current, CancellationToken cancellationToken);
    Task<string> DownloadAsync(ReleaseAsset asset, string directory, CancellationToken cancellationToken);
}

public sealed class ReleaseClient : IReleaseClient, IDisposable
{
    private readonly HttpClient http;
    public ReleaseClient(HttpMessageHandler? handler = null)
    {
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PaneShift-UpdateCheck");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            if (!IsAllowedHost(uri)) throw new IOException("The download redirected outside GitHub's release hosts.");
            var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                uri = new Uri(uri, location); response.Dispose(); continue;
            }
            return response;
        }
        throw new IOException("Too many release download redirects.");
    }

    public static bool IsAllowedHost(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        uri.Host is "api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";

    public async Task<AvailableRelease?> CheckAsync(Version current, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await GetAsync(ReleaseUpdates.LatestUri, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        byte[] data = await ReadLimitedAsync(response, 2 * 1024 * 1024, timeout.Token);
        return ReleaseUpdates.Parse(Encoding.UTF8.GetString(data), current);
    }

    public async Task<string> DownloadAsync(ReleaseAsset asset, string directory, CancellationToken cancellationToken)
    {
        // Defense in depth for callers that did not originate in the release parser.
        if (Path.GetFileName(asset.Name) != asset.Name || !asset.Name.StartsWith("PaneShift-", StringComparison.Ordinal) ||
            !IsAllowedHost(asset.DownloadUri) || asset.DownloadUri.Host != "github.com" ||
            !asset.DownloadUri.AbsolutePath.StartsWith($"/{ReleaseUpdates.Repository}/releases/download/", StringComparison.Ordinal) ||
            asset.Sha256.Length != 64 || !asset.Sha256.All(Uri.IsHexDigit) || asset.Size is <= 0 or > 536870912)
            throw new ArgumentException("Invalid release asset.");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, asset.Name), temporary = destination + ".partial";
        try
        {
            using var response = await GetAsync(asset.DownloadUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920]; long total = 0; int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > asset.Size) throw new IOException("The downloaded file exceeded its expected size.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total != asset.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Release SHA-256 or size verification failed. The file will not be installed.");
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, destination, false);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, int maximum, CancellationToken token)
    {
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream(); byte[] buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (memory.Length + read > maximum) throw new IOException("The release response is too large.");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }
    public void Dispose() => http.Dispose();
}

public sealed record UpdateCheckState(DateTimeOffset? LastAttempt = null, string? NotifiedTag = null);
public sealed class UpdateCheckStore(string path)
{
    public UpdateCheckState Load()
    {
        try { return JsonSerializer.Deserialize<UpdateCheckState>(File.ReadAllText(path)) ?? new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(UpdateCheckState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
