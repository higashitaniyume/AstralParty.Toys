using System.Buffers.Binary;
using System.Net.Sockets;

namespace AstralParty.Toys.Services.Account;

public sealed record AccountFrame(short Command, long Upsn, short Error, byte Version1, byte Version2, byte[] Payload)
{
    public const int HeaderLength = 35;
    public const int MaxPayloadLength = 8 * 1024 * 1024;
    public static byte[] Encode(short command, long session, long upsn, byte[] payload)
    {
        if (payload.Length > MaxPayloadLength) throw new InvalidDataException();
        var bytes = new byte[HeaderLength + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes, payload.Length);
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(4), session);
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(12), command);
        bytes[14] = 1;
        BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(17), upsn);
        payload.CopyTo(bytes, HeaderLength);
        return bytes;
    }

    public static async Task<AccountFrame> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 0 or > MaxPayloadLength) throw new InvalidDataException();
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return new(BinaryPrimitives.ReadInt16BigEndian(header.AsSpan(12)), BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(17)),
            BinaryPrimitives.ReadInt16BigEndian(header.AsSpan(33)), header[14], header[15], payload);
    }
}

/// <summary>Single official connection. Correlated RPCs, bounded timeouts, no retries or logging.</summary>
public sealed class AccountTransport(AccountCredentials credentials) : IAsyncDisposable
{
    private readonly TcpClient _tcp = new() { NoDelay = true };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _calls = new(1, 1);
    private readonly object _gate = new();
    private TaskCompletionSource<AccountFrame>? _pending;
    private short _expected;
    private long _sequence = 100, _pendingSequence;
    private Task _reader = Task.CompletedTask;
    private Exception? _failure;
    public long SessionId { get; set; }
    public bool Connected => !_lifetime.IsCancellationRequested;

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (!AccountCredentials.IsOfficialEndpoint(credentials.Host, credentials.Port)) throw new InvalidDataException();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await _tcp.ConnectAsync(credentials.Host, credentials.Port, deadline.Token).ConfigureAwait(false);
        _reader = ReadLoopAsync();
    }

    public async Task<AccountFrame> CallAsync(short command, byte[] payload, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await _calls.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            var completion = new TaskCompletionSource<AccountFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            long sequence = Interlocked.Increment(ref _sequence);
            lock (_gate) { _pending = completion; _expected = (short)(command + 1); _pendingSequence = sequence; }
            await _tcp.GetStream().WriteAsync(AccountFrame.Encode(command, SessionId, sequence, payload), deadline.Token).ConfigureAwait(false);
            return await completion.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Stop();
            throw _failure ?? new AccountQueryException("rpc_timeout", command);
        }
        catch { Stop(); throw; } // Timed-out RPCs leave session state uncertain; never reuse it.
        finally { lock (_gate) _pending = null; _calls.Release(); }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (true)
            {
                var frame = await AccountFrame.ReadAsync(_tcp.GetStream(), _lifetime.Token).ConfigureAwait(false);
                if (frame.Command == 1001) throw new AccountQueryException("server_kick", frame.Command, frame.Error);
                if (frame.Error != 0) throw new AccountQueryException("server_rejected", frame.Command, frame.Error);
                if (frame.Version1 > credentials.ProtocolVersion1 || frame.Version2 > credentials.ProtocolVersion2)
                    throw new AccountQueryException("resource_version", frame.Command);
                lock (_gate)
                {
                    if (_pending == null || frame.Upsn != _pendingSequence) continue;
                    if (frame.Command != _expected) throw new InvalidDataException();
                    _pending.TrySetResult(frame);
                }
            }
        }
        catch (Exception error)
        {
            _failure = error is AccountQueryException ? error : new AccountQueryException("transport_closed");
            lock (_gate) _pending?.TrySetException(_failure);
            Stop();
        }
    }

    public void Stop()
    {
        _lifetime.Cancel();
        _tcp.Dispose();
        lock (_gate) _pending?.TrySetCanceled();
    }

    public async ValueTask DisposeAsync() { Stop(); await _reader.ConfigureAwait(false); }
}
