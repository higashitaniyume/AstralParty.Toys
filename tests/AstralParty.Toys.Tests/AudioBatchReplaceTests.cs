using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstralParty.Toys.Services.Skins;
using AstralParty.Toys.Tests.Support;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class AudioBatchReplaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "toys-audio-batch-" + Guid.NewGuid().ToString("N"));
    private readonly AudioSkinSchemeManager _manager = new();

    public AudioBatchReplaceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    private static int? MatchOne(AudioSkinSchemeManager m, string fileName)
    {
        var (matches, _) = m.MatchBatchFiles([Path.Combine("C:\\x", fileName)]);
        return matches.Count == 1 ? matches[0].Id : null;
    }

    [Theory]
    [InlineData("Voice_101_achieve.wav", 1010101)]          // 音效名
    [InlineData("voice_101_ACHIEVE.ogg", 1010101)]          // 大小写不敏感
    [InlineData("1010101.ogg", 1010101)]                    // 编号
    [InlineData("#1.mp3", 1)]                               // 带 # 的编号
    [InlineData("1010101_Voice_101_achieve.ogg", 1010101)]  // 「导出原版音频包」的命名
    [InlineData("1_UI_Common_Click01.wav", 1)]
    public void Match_AcceptsNameIdAndExportNaming(string fileName, int expected)
    {
        Assert.Equal(expected, MatchOne(_manager, fileName));
    }

    [Theory]
    [InlineData("01_achieve.ogg")]          // 第三方语音包的「序号_槽位」：编号 1 是点击音效，不能对上
    [InlineData("1_Voice_101_achieve.wav")] // 编号和音效名不是同一条
    [InlineData("999999999.wav")]           // 没有这个编号
    [InlineData("我的配音.wav")]              // 既不是编号也不是音效名
    [InlineData("Voice_101_achieve.txt")]   // 不是音频
    public void Match_RejectsAmbiguousOrWrongNames(string fileName)
    {
        var (matches, skipped) = _manager.MatchBatchFiles([Path.Combine("C:\\x", fileName)]);
        Assert.Empty(matches);
        var skip = Assert.Single(skipped);
        Assert.False(string.IsNullOrEmpty(skip.Reason));
    }

    /// <summary>
    /// 宿主分发器用白名单把消息交给 HandleAudioSkinMessage：新加的 case 忘了登记，页面上的按钮就没有反应（批量替换踩过）。
    /// </summary>
    [Fact]
    public void EveryAudioMessageHandled_IsRoutedByDispatcher()
    {
        var handler = File.ReadAllText(Path.Combine(TestPaths.AppSourceDirectory, "HybridWindow.AudioSkins.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(TestPaths.AppSourceDirectory, "HybridWindow.Dispatcher.cs"));
        var handled = System.Text.RegularExpressions.Regex.Matches(handler, "case \"(audioSkins\\w+)\":")
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.Contains("audioSkinsBatchPick", handled);
        var missing = handled.Where(t => !dispatcher.Contains($"case \"{t}\":")).ToList();
        Assert.True(missing.Count == 0, "分发器没有登记：" + string.Join(", ", missing));
    }

    [Fact]
    public void Match_SameEntryTwice_KeepsFirstOnly()
    {
        var (matches, skipped) = _manager.MatchBatchFiles(["C:\\x\\1.wav", "C:\\x\\UI_Common_Click01.ogg"]);
        Assert.Equal(1, Assert.Single(matches).Id);
        Assert.Contains("1.wav", Assert.Single(skipped).Reason);
    }

    [Fact]
    public void Apply_WritesRulesAndReportsUnreadableFiles()
    {
        var game = Path.Combine(_root, "game");
        Directory.CreateDirectory(game);
        _manager.CreateScheme(game, "mine", "我的", "", "");
        var a = Path.Combine(_root, "Voice_101_achieve.wav");
        var b = Path.Combine(_root, "1.ogg");
        File.WriteAllBytes(a, [1, 2, 3]);
        File.WriteAllBytes(b, [4, 5]);
        var (matches, _) = _manager.MatchBatchFiles([a, b]);
        File.Delete(b); // 选完文件后被删掉了

        var failed = new List<string>();
        var done = _manager.ApplyBatchReplace(game, "mine", matches, failed);

        Assert.Equal(1, done);
        Assert.Single(failed);
        var items = _manager.GetCatalogItems(game, "mine");
        var replaced = items.Single(i => i.Id == 1010101);
        Assert.Equal("replace", replaced.Action);
        Assert.True(File.Exists(Path.Combine(AudioSkinSchemeManager.SchemeDirectory(game, "mine"), "1010101_Voice_101_achieve.wav")));
        Assert.Equal("none", items.Single(i => i.Id == 1).Action);
    }
}
