namespace AstralParty.Toys.Tests.Support;

/// <summary>
/// 一次性临时目录：所有会写盘的测试都在里面折腾，绝不碰用户真实目录。
/// </summary>
internal sealed class TestSandbox : IDisposable
{
    public TestSandbox(string name)
    {
        Root = Path.Combine(Path.GetTempPath(), name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>拼一个沙箱内的路径（不创建目录）。</summary>
    public string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>拼一个沙箱内的路径并创建目录。</summary>
    public string EnsureDirectory(params string[] parts)
    {
        var path = Combine(parts);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>写入一个文本文件（父目录自动创建），返回文件路径。</summary>
    public string WriteFile(string relativePath, string content)
    {
        var path = Combine(relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>写入一个二进制文件（父目录自动创建），返回文件路径。</summary>
    public string WriteFile(string relativePath, byte[] content)
    {
        var path = Combine(relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>按游戏的目录约定放一局回放：<c>&lt;root&gt;\&lt;id&gt;\&lt;id&gt;</c>。</summary>
    public string WriteReplay(string replayId, byte[] bytes)
    {
        Directory.CreateDirectory(Combine(replayId));
        return WriteFile(Path.Combine(replayId, replayId), bytes);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // 临时目录清不掉不该让测试失败（可能有文件句柄还没释放）
        }
    }
}
