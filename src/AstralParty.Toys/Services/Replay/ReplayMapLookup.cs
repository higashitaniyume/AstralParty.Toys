using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AstralParty.Toys.Services.Replay;

/// <summary>Read only the bounded opening of an official replay, without importing or saving it.</summary>
public static class ReplayMapLookup
{
    public const int MaxPrefixBytes = 1024 * 1024;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });

    public static async Task<int> FindMapIdAsync(string replayId, Func<byte[], int> decodeOpening,
        CancellationToken ct, HttpClient? client = null)
    {
        if (!ReplayCdn.IsValidReplayId(replayId, out _)) return 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReplayCdn.Url(ReplayCdnEndpoint.CnProd, replayId));
            request.Headers.Range = new RangeHeaderValue(0, MaxPrefixBytes - 1);
            using var response = await (client ?? Client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent) return 0;
            if (response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.From != 0) return 0;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return await ReadMapIdAsync(stream, decodeOpening, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return 0; }
    }

    public static async Task<int> ReadMapIdAsync(Stream stream, Func<byte[], int> decodeOpening, CancellationToken ct)
    {
        int consumed = 0;
        var header = new byte[6];
        for (int frame = 0; frame < 64 && consumed + header.Length <= MaxPrefixBytes; frame++)
        {
            await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
            consumed += header.Length;
            int command = BinaryPrimitives.ReadInt16BigEndian(header);
            int length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(2));
            if (length < 0 || length > MaxPrefixBytes - consumed) return 0;
            var payload = new byte[length];
            await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
            consumed += length;
            if (command == 1003) return Math.Max(0, decodeOpening(payload));
        }
        return 0;
    }
}
