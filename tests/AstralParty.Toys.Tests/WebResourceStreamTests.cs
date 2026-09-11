using System.Text;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 交给 WebView2 的响应流包装：内容要原样读出去，读到结尾后要自己释放内层流
/// （WebView2 不会替我们释放，见 WebView2Feedback#2513），之后继续读必须安全返回 0。
/// </summary>
public sealed class WebResourceStreamTests
{
    [Fact]
    public void Read_ForwardsContentAndDisposesInnerAtEnd()
    {
        var inner = new MemoryStream(Encoding.UTF8.GetBytes("hello webview"));
        using var stream = new WebResourceStream(inner);

        var buffer = new byte[64];
        var read = stream.Read(buffer, 0, buffer.Length);
        Assert.Equal("hello webview", Encoding.UTF8.GetString(buffer, 0, read));
        Assert.Equal(13, stream.Length);

        // 读到结尾：包装流自己释放内层流
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Throws<ObjectDisposedException>(() => inner.Read(buffer, 0, buffer.Length));

        // 已释放后再读仍是 0，不抛异常
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.False(stream.CanRead);
        Assert.Equal(13, stream.Length);
    }

    [Fact]
    public void Read_WorksAcrossMultipleBuffers()
    {
        var content = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("0123456789", 100)));
        using var stream = new WebResourceStream(new MemoryStream(content));

        using var copy = new MemoryStream();
        var buffer = new byte[64];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) copy.Write(buffer, 0, read);

        Assert.Equal(content, copy.ToArray());
    }

    [Fact]
    public void WriteAndSetLength_AreRejected()
    {
        using var stream = new WebResourceStream(new MemoryStream([1, 2, 3]));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
    }
}
