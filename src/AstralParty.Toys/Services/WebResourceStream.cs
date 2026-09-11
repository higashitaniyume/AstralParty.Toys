namespace AstralParty.Toys.Services;

/// <summary>
/// 交给 WebView2 的响应流包装：WebView2 读完内容后**不会**释放传入的流
/// （见 WebView2Feedback#2513），所以这里在读到结尾或出错时自行 Dispose 内层流。
/// </summary>
internal sealed class WebResourceStream(Stream inner) : Stream
{
    private readonly long _length = inner.CanSeek ? inner.Length : 0;
    private bool _disposed;

    public override bool CanRead => !_disposed && inner.CanRead;
    public override bool CanSeek => !_disposed && inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _disposed ? _length : inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // 已读完（内层流已释放）后继续返回 0，避免对方再读一次时抛 ObjectDisposedException。
        if (_disposed) return 0;
        try
        {
            var read = inner.Read(buffer, offset, count);
            if (read == 0) Dispose();
            return read;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
