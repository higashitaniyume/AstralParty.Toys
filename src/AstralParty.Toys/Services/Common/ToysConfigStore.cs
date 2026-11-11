using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AstralParty.Toys.Services;

/// <summary>
/// <c>AstralParty.Toys.Config.json</c> 的读写。
///
/// 所有写入都是"锁 → 重新读盘 → 改字段 → 原子替换"，所以多个服务各持一个实例也不会互相覆盖：
/// 主题只改 <c>app.theme</c>，回放设置只改 <c>replay.*</c>。配置被手改坏时保留 <c>.bak</c> 并回退默认值。
/// </summary>
public sealed class ToysConfigStore
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>进程内所有实例共用同一把锁，避免两个服务同时写同一份文件。</summary>
    private static readonly object FileGate = new();

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _path;

    public ToysConfigStore(string path) => _path = path;

    /// <summary>数据根对应的统一配置。</summary>
    public static ToysConfigStore ForRoot(string root) =>
        new(System.IO.Path.Combine(root, ToysPaths.ConfigFileName));

    /// <summary>这份配置的文件路径。</summary>
    public string FilePath => _path;

    /// <summary>读一份配置。文件缺失或损坏时返回默认值，绝不抛异常。</summary>
    public ToysConfig Load()
    {
        lock (FileGate)
        {
            return LoadCore();
        }
    }

    private ToysConfig LoadCore()
    {
        var config = TryRead(_path);
        if (config is not null) return config.Normalize();

        var backup = TryRead(_path + ".bak");
        if (backup is not null)
        {
            ToysLog.Warn("config", $"配置文件无法解析，已回退到备份：{_path}.bak");
            return backup.Normalize();
        }

        if (File.Exists(_path))
            ToysLog.Error("config", $"配置文件无法解析，已改用默认设置：{_path}");

        return new ToysConfig().Normalize();
    }

    private static ToysConfig? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonSerializer.Deserialize<ToysConfig>(text, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>读盘 → 修改 → 原子写回。</summary>
    public ToysConfig Update(Action<ToysConfig> mutate)
    {
        lock (FileGate)
        {
            var config = LoadCore();
            mutate(config);
            config.Normalize();
            Write(config);
            return config;
        }
    }

    /// <summary>整份覆盖写入（调用方自己保证字段完整）。</summary>
    public void Save(ToysConfig config)
    {
        lock (FileGate)
        {
            config.Normalize();
            Write(config);
        }
    }

    private void Write(ToysConfig config)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            WriteAtomic(_path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ToysLog.Error("config", $"写入配置文件失败：{_path}（{ex.Message}）");
        }
    }

    /// <summary>写临时文件再替换，避免中途崩溃留下半份 JSON。</summary>
    public static void WriteAtomic(string path, string contents)
    {
        var directory = System.IO.Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        var backup = path + ".bak";

        File.WriteAllText(temp, contents, new UTF8Encoding(false));

        if (File.Exists(path))
        {
            try
            {
                File.Replace(temp, path, backup, ignoreMetadataErrors: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                // 某些网络盘不支持 Replace：退回到覆盖复制
            }
        }

        File.Move(temp, path, overwrite: true);
    }
}
