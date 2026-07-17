using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.TTS;

/// <summary>
/// Small, dependency-free client for the public NTTS v1 synthesis contract.
/// </summary>
public sealed class NTTSClient : IDisposable
{
    private const int MaxAudioBytes = 2 * 1024 * 1024;

    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _requestSlots = new(4, 4);
    private readonly IConfigurationManager _cfg;
    private readonly ISawmill _sawmill;

    public NTTSClient(IConfigurationManager cfg, ILogManager logManager)
    {
        _cfg = cfg;
        _sawmill = logManager.GetSawmill("ntts");
    }

    public async Task<byte[]?> Synthesize(string speaker, string text, string? effect = null)
    {
        var token = _cfg.GetCVar(CCVars.TTSApiToken).Trim();
        if (token.Length == 0)
            return null;

        var requestUri = CreateRequestUri(_cfg.GetCVar(CCVars.TTSApiUrl), speaker, text, effect);
        if (requestUri == null)
        {
            _sawmill.Error("TTS API URL is invalid");
            return null;
        }

        await _requestSlots.WaitAsync();
        try
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Clamp(_cfg.GetCVar(CCVars.TTSApiTimeout), 1, 60)));
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    _sawmill.Warning("NTTS rate limit reached");
                else
                    _sawmill.Error($"NTTS returned HTTP {(int) response.StatusCode}");

                return null;
            }

            if (response.Content.Headers.ContentLength is > MaxAudioBytes)
            {
                _sawmill.Error("NTTS returned an audio response larger than the safety limit");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                    break;

                if (output.Length + read > MaxAudioBytes)
                {
                    _sawmill.Error("NTTS returned an audio response larger than the safety limit");
                    return null;
                }

                output.Write(buffer, 0, read);
            }

            return output.Length == 0 ? null : output.ToArray();
        }
        catch (OperationCanceledException)
        {
            _sawmill.Warning("NTTS request timed out");
            return null;
        }
        catch (HttpRequestException e)
        {
            _sawmill.Error($"NTTS request failed: {e.Message}");
            return null;
        }
        finally
        {
            _requestSlots.Release();
        }
    }

    internal static Uri? CreateRequestUri(string apiUrl, string speaker, string text, string? effect)
    {
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return null;

        var builder = new UriBuilder(uri);
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(builder.Query))
            query.Add(builder.Query.TrimStart('?'));

        query.Add($"speaker={Uri.EscapeDataString(speaker)}");
        query.Add($"text={Uri.EscapeDataString(text)}");
        query.Add("ext=ogg");
        if (!string.IsNullOrWhiteSpace(effect))
            query.Add($"effect={Uri.EscapeDataString(effect)}");

        builder.Query = string.Join('&', query);
        return builder.Uri;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
