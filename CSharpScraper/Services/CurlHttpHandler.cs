using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace EwrcScraper.Services;

// eWRC is behind Cloudflare, which blocks the .NET TLS fingerprint with 403 — even when
// the User-Agent and headers are made to look like a browser. The native Windows
// curl.exe uses a different TLS stack that Cloudflare accepts. Verified on the target
// machine, same IP and moment: curl.exe -> 200, .NET HttpClient -> 403.
//
// This handler runs every request through curl.exe so the rest of the app keeps using a
// normal HttpClient (GetStringAsync / GetAsync / GetByteArrayAsync) unchanged.
public class CurlHttpHandler : HttpMessageHandler
{
    private readonly string _curlPath;
    private readonly int _maxSeconds;

    public CurlHttpHandler(int maxSeconds = 45, string? curlPath = null)
    {
        _maxSeconds = maxSeconds;
        _curlPath = curlPath ?? FindCurl();
    }

    public static bool IsAvailable() => File.Exists(FindCurl());

    private static string FindCurl()
    {
        var sys = Path.Combine(Environment.SystemDirectory, "curl.exe");
        return File.Exists(sys) ? sys : "curl.exe"; // fall back to PATH lookup
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var bodyFile = Path.Combine(Path.GetTempPath(), "ewrc_curl_" + Guid.NewGuid().ToString("N") + ".bin");

        var psi = new ProcessStartInfo
        {
            FileName = _curlPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-sS");                 // silent, but still report errors
        psi.ArgumentList.Add("-L");                  // follow redirects (e.g. photo host)
        psi.ArgumentList.Add("--compressed");        // let curl negotiate+decode gzip/br
        psi.ArgumentList.Add("--max-time");
        psi.ArgumentList.Add(_maxSeconds.ToString());
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(bodyFile);              // body to file (keeps binary intact)
        psi.ArgumentList.Add("-w");
        psi.ArgumentList.Add("%{http_code}\n%{content_type}");

        if (request.Method != HttpMethod.Get)
        {
            psi.ArgumentList.Add("-X");
            psi.ArgumentList.Add(request.Method.Method);
        }

        // Forward request headers (curl manages encoding/host itself).
        foreach (var h in request.Headers)
        {
            if (h.Key.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
            if (h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add($"{h.Key}: {string.Join(", ", h.Value)}");
        }

        psi.ArgumentList.Add(request.RequestUri!.AbsoluteUri);

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
                throw new HttpRequestException($"curl faalde (exit {proc.ExitCode}): {stderr.Trim()}");

            var lines = stdout.Replace("\r", "").Split('\n');
            int statusCode = int.TryParse(lines.ElementAtOrDefault(0)?.Trim(), out var sc) ? sc : 0;
            string contentType = lines.ElementAtOrDefault(1)?.Trim() ?? string.Empty;

            byte[] body = File.Exists(bodyFile)
                ? await File.ReadAllBytesAsync(bodyFile, ct)
                : Array.Empty<byte>();

            var resp = new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(body),
            };

            var mediaType = contentType.Split(';')[0].Trim();
            if (!string.IsNullOrEmpty(mediaType))
            {
                try { resp.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType); }
                catch { /* ignore an unparsable content-type */ }
            }
            return resp;
        }
        finally
        {
            try { if (File.Exists(bodyFile)) File.Delete(bodyFile); } catch { /* temp cleanup best-effort */ }
        }
    }
}
