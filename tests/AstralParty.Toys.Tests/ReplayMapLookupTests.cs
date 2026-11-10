using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using AstralParty.Toys.Services.Account;
using AstralParty.Toys.Services.Replay;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class ReplayMapLookupTests
{
    private static byte[] Frame(short command, byte[] payload)
    {
        var bytes = new byte[payload.Length + 6];
        BinaryPrimitives.WriteInt16BigEndian(bytes, command);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(2), payload.Length);
        payload.CopyTo(bytes, 6);
        return bytes;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }

    [Fact]
    public async Task OpeningFrameUsesActualMapIdRatherThanMapMode()
    {
        using var protocol = new AccountProtocol(Path.Combine(AppContext.BaseDirectory, "Protocol"));
        var opening = protocol.Parse("RunningGameS2C", []);
        var roomProperty = opening.GetType().GetProperty("Room")!;
        var room = Activator.CreateInstance(roomProperty.PropertyType)!;
        room.GetType().GetProperty("MapId")!.SetValue(room, 101);
        room.GetType().GetProperty("MapType")!.SetValue(room, 4);
        var bytes = protocol.Create("RunningGameS2C", ("Room", room));
        using var stream = new MemoryStream(Frame(1003, bytes));
        Assert.Equal(101, await ReplayMapLookup.ReadMapIdAsync(stream, payload =>
            (int)AccountProtocol.Number(AccountProtocol.Get(protocol.Parse("RunningGameS2C", payload), "Room"), "MapId"), CancellationToken.None));
    }

    [Fact]
    public async Task RangeLookupStopsAtOpeningEvenWhenServerIgnoresRange()
    {
        using var body = new MemoryStream([.. Frame(1003, [7]), .. new byte[ReplayMapLookup.MaxPrefixBytes * 2]]);
        var handler = new Handler(request =>
        {
            Assert.Equal("https://sereplaycn.feimogames.com/prod/123", request.RequestUri!.AbsoluteUri);
            Assert.Equal(ReplayMapLookup.MaxPrefixBytes - 1, request.Headers.Range!.Ranges.Single().To);
            Assert.Null(request.Headers.Authorization);
            Assert.Null(request.Headers.Referrer);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
        });
        using var client = new HttpClient(handler);
        Assert.Equal(7, await ReplayMapLookup.FindMapIdAsync("123", bytes => { Assert.Single(bytes); return bytes[0]; }, CancellationToken.None, client));
        Assert.False(body.CanRead); // Response stream is disposed even after an early return.
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OpeningCanFollowOtherFramesAndNeverReadsFollowingPayloads()
    {
        using var body = new MemoryStream([.. Frame(1098, [1, 2]), .. Frame(1003, [9]), .. new byte[100]]);
        Assert.Equal(9, await ReplayMapLookup.ReadMapIdAsync(body, bytes => bytes[0], CancellationToken.None));
        Assert.Equal(15, body.Position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ReplayMapLookup.MaxPrefixBytes)]
    [InlineData(int.MaxValue)]
    public async Task InvalidOrOversizePayloadLengthNeverAllocatesOrDecodes(int length)
    {
        var bytes = Frame(1003, []);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(2), length);
        using var body = new MemoryStream(bytes);
        Assert.Equal(0, await ReplayMapLookup.ReadMapIdAsync(body, _ => throw new Exception("must not decode"), CancellationToken.None));
    }

    [Fact]
    public async Task NonzeroContentRangeCannotBeMistakenForReplayBeginning()
    {
        var handler = new Handler(_ =>
        {
            var content = new ByteArrayContent(Frame(1003, [7]));
            content.Headers.ContentRange = new ContentRangeHeaderValue(100, 106, 200);
            return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content };
        });
        using var client = new HttpClient(handler);
        Assert.Equal(0, await ReplayMapLookup.FindMapIdAsync("123", _ => throw new Exception("must not decode"), CancellationToken.None, client));
    }

    [Theory]
    [InlineData(302)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task UnavailableReplayDoesNotPreventRecentListDisplay(int status)
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal(0, await ReplayMapLookup.FindMapIdAsync("123", _ => 99, CancellationToken.None, client));
    }

    [Fact]
    public async Task InvalidReplayIdDoesNotMakeNetworkRequest()
    {
        var handler = new Handler(_ => throw new Exception("must not send"));
        using var client = new HttpClient(handler);
        Assert.Equal(0, await ReplayMapLookup.FindMapIdAsync("../123", _ => 99, CancellationToken.None, client));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CancellationAndTruncatedReplayAreHandledWithoutInventingMaps()
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0, 1]) }));
        Assert.Equal(0, await ReplayMapLookup.FindMapIdAsync("123", _ => 99, CancellationToken.None, client));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReplayMapLookup.FindMapIdAsync("123", _ => 99, cancelled.Token, client));
    }
}
