using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstralParty.Toys.Services.Skins;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class GameAudioIndexTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), "toys-game-audio-" + Guid.NewGuid().ToString("N"));

    public GameAudioIndexTests() => Directory.CreateDirectory(_gameDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_gameDir)) Directory.Delete(_gameDir, true); } catch { }
    }

    // ---- 手工拼一个最小的 .bnk：BKHD + DIDX + DATA + HIRC ----

    private static byte[] U32(uint v) => BitConverter.GetBytes(v);
    private static byte[] U16(ushort v) => BitConverter.GetBytes(v);
    private static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    private static byte[] Chunk(string tag, byte[] body) => Cat(System.Text.Encoding.ASCII.GetBytes(tag), U32((uint)body.Length), body);
    private static byte[] Obj(byte type, uint id, byte[] payload) => Cat([type], U32((uint)(4 + payload.Length)), U32(id), payload);

    private const uint EventId = 0xC0001234;
    private const uint ActionId = 500;
    private const uint ContainerId = 600;
    private const uint SoundA = 700, SoundB = 701, SoundGone = 702;
    private const uint MusicEventId = 0xC0005678, MusicActionId = 510, MusicTrackId = 800;

    private static byte[] BuildBank(out byte[] wemA, out byte[] wemB)
    {
        wemA = "RIFF-A-0123456789"u8.ToArray();
        wemB = "RIFF-B-xyz"u8.ToArray();
        var data = Cat(wemA, wemB);
        var didx = Cat(
            U32(1001), U32(0), U32((uint)wemA.Length),
            U32(1002), U32((uint)wemA.Length), U32((uint)wemB.Length));

        // 声音：插件 ID + 流类型 + 源 ID + 后面一些无关字段
        byte[] Sound(uint source) => Cat(U32(0x00040001), [0], U32(source), U32(1234), [0, 0, 0]);
        var hirc = Cat(
            U32(8),
            Obj(WwiseBank.EventType, EventId, Cat([1], U32(ActionId))),
            Obj(WwiseBank.ActionType, ActionId, Cat(U16(0x0403), U32(ContainerId), [0, 0])),
            // 随机容器：前面几个字段（这里用 0xFF 填充 + 一个不是节点的父 ID），然后是「数量 + 子节点 ID」
            Obj(5, ContainerId, Cat([0xFF, 0xFF, 0xFF], U32(999999), U32(3), U32(SoundA), U32(SoundB), U32(SoundGone), [0, 0])),
            Obj(WwiseBank.SoundType, SoundA, Sound(1001)),
            Obj(WwiseBank.SoundType, SoundB, Sound(1002)),
            Obj(WwiseBank.SoundType, SoundGone, Sound(4242)), // 引用了库里没有的 wem
            Obj(WwiseBank.EventType, MusicEventId, Cat([1], U32(MusicActionId))),
            Obj(WwiseBank.ActionType, MusicActionId, Cat(U16(0x0403), U32(MusicTrackId), [0, 0])));
        // 音乐轨：音源直接写在自己里面
        hirc = Cat(hirc, Obj(11, MusicTrackId, Cat([0], U32(1), U32(0x00040001), [2], U32(1002), U32(0), [0])));
        hirc = Cat(U32(9), hirc.AsSpan(4).ToArray());

        return Cat(
            Chunk("BKHD", Cat(U32(145), U32(42), new byte[8])),
            Chunk("DIDX", didx),
            Chunk("DATA", data),
            Chunk("HIRC", hirc));
    }

    [Fact]
    public void Parse_ReadsVersionMediaAndObjects()
    {
        var raw = BuildBank(out var wemA, out var wemB);
        var bank = WwiseBank.Parse(raw);

        Assert.Equal(145u, bank.Version);
        Assert.Equal(2, bank.Media.Count);
        var (offA, sizeA) = bank.Media[1001];
        var (offB, sizeB) = bank.Media[1002];
        Assert.Equal(wemA, raw.AsSpan(offA, sizeA).ToArray());
        Assert.Equal(wemB, raw.AsSpan(offB, sizeB).ToArray());
        Assert.Equal(9, bank.Objects.Count);
        Assert.Equal(WwiseBank.EventType, bank.Objects[EventId].Type);
    }

    [Fact]
    public void Resolve_FollowsEventThroughContainer_AndSkipsMissingMedia()
    {
        var bank = WwiseBank.Parse(BuildBank(out _, out _));

        var wems = WwiseEventResolver.Resolve(EventId, bank.Objects, bank.Media.ContainsKey);

        Assert.NotNull(wems);
        Assert.Equal(new uint[] { 1001, 1002 }, wems);
    }

    [Fact]
    public void Resolve_ReadsMusicTrackSources()
    {
        var bank = WwiseBank.Parse(BuildBank(out _, out _));

        var wems = WwiseEventResolver.Resolve(MusicEventId, bank.Objects, bank.Media.ContainsKey);

        Assert.Equal(new uint[] { 1002 }, wems);
    }

    [Fact]
    public void Resolve_UnknownEvent_ReturnsNull()
    {
        var bank = WwiseBank.Parse(BuildBank(out _, out _));
        Assert.Null(WwiseEventResolver.Resolve(12345, bank.Objects, bank.Media.ContainsKey));
        // 动作 ID 不是事件
        Assert.Null(WwiseEventResolver.Resolve(ActionId, bank.Objects, bank.Media.ContainsKey));
    }

    [Fact]
    public void Parse_TruncatedChunk_Throws()
    {
        var raw = BuildBank(out _, out _);
        Assert.Throws<InvalidDataException>(() => WwiseBank.Parse(raw.AsSpan(0, raw.Length - 3)));
    }

    [Theory]
    [InlineData("ogg/Hero_101.Simplified Chinese/554811043.ogg", "Hero_101.Simplified Chinese", 554811043u)]
    [InlineData("ogg/ROLE_COMMON/157338258.ogg", "ROLE_COMMON", 157338258u)]
    [InlineData("ogg\\UISFX\\43494453.ogg", "UISFX", 43494453u)]
    public void ParseRelOgg_SplitsBankAndWem(string relOgg, string bank, uint wem)
    {
        var (b, w) = GameAudioIndex.ParseRelOgg(relOgg);
        Assert.Equal(bank, b);
        Assert.Equal(wem, w);
    }

    [Fact]
    public void BankKey_MatchesRelOggNaming()
    {
        Assert.Equal("ROLE_COMMON", GameAudioIndex.BankKey("ROLE_COMMON", "SFX"));
        Assert.Equal("Hero_101.Japanese", GameAudioIndex.BankKey("Hero_101", "Japanese"));
    }

    [Fact]
    public void Prepare_WithoutGameFiles_ReportsReasonInsteadOfThrowing()
    {
        var manager = new AudioSkinSchemeManager();

        var result = manager.PrepareOriginalAudio(_gameDir);

        Assert.Equal(0, result.Ready);
        Assert.False(string.IsNullOrEmpty(result.Problem));
        Assert.Null(GameAudioIndex.TryLoad(_gameDir));
        Assert.Null(GameAudioIndex.EnsureOriginal(_gameDir, 1010101, out var problem));
        Assert.False(string.IsNullOrEmpty(problem));
    }
}
