using System.IO;

namespace AstralParty.Toys.Services;

/// <summary>一个账号 profile（游戏自己的目录记为“默认”档案，Name 为空串）。</summary>
public sealed record AccountProfileInfo(
    string Name,
    string Directory,
    bool IsDefault,
    bool Active,
    bool Exists,
    long Bytes,
    int ReplayCount,
    DateTime LastWriteUtc,
    bool CacheSeeded,
    string Note);

/// <summary>
/// 加载器「多 profile」的目录侧实现：每个 profile 就是
/// <c>%USERPROFILE%\AppData\LocalLow\&lt;公司&gt;\&lt;产品&gt;_profiles\&lt;名&gt;</c> 下的一个目录，
/// 游戏读写账号 / 存档 / 回放时由加载器在进程内重定向过去。
///
/// 本服务只做目录与配置无关的纯文件操作（列表 / 新建 / 改名 / 删除），
/// 便于单元测试；窗口层只负责弹窗、消息与写配置。
/// </summary>
public sealed class AccountProfileService
{
    private const string CacheFolderName = "com.unity.addressables";
    private const string SeedMarkerName = ".seeded";
    /// <summary>备注文件（放在档案目录里，改名时跟着目录走、删除时一起清掉）。</summary>
    private const string NoteFileName = ".profile.json";

    /// <summary>备注长度上限（页面 textarea 的 maxlength 与之一致）。</summary>
    public const int MaxNoteLength = 256;

    /// <summary>profile 名的允许规则（唯一的来源：页面从 payload 里拿到同一个正则做实时校验）。
    /// 只允许英文字母、数字与下划线 —— 目录名/配置值都不需要再做编码转换，也不受输入法影响。</summary>
    public const string NamePattern = "^[A-Za-z0-9_]{1,32}$";

    /// <summary>与 <see cref="NamePattern"/> 对应的长度上限。
    /// 页面提示文案里写死了 32：改这里要同步 i18n 的 <c>profile.name_hint</c> / <c>profile.name_too_long</c>。</summary>
    public const int MaxNameLength = 32;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>当前游戏的安装目录（可能为空 = 还没选）。</summary>
    public string? GameDirectory { get; }

    /// <summary>app.info 里的公司名 / 产品名（游戏用它决定 LocalLow 子目录）。</summary>
    public string? Company { get; }
    public string? Product { get; }

    /// <summary>游戏自己写的目录（默认档案）。</summary>
    public string? SourceDirectory { get; }

    /// <summary>profile 根目录（&lt;产品&gt;_profiles）。</summary>
    public string? ProfilesRoot { get; }

    /// <summary>无法定位时的人类可读原因；为空表示可用。</summary>
    public string? Error { get; }

    public AccountProfileService(string? gameDirectory, string? localLowRoot = null)
    {
        GameDirectory = string.IsNullOrWhiteSpace(gameDirectory) ? null : gameDirectory.Trim();
        if (GameDirectory is null)
        {
            Error = "还没有选择游戏目录。";
            return;
        }
        if (!Directory.Exists(GameDirectory))
        {
            Error = "游戏目录已经不存在了。";
            return;
        }
        if (!TryReadAppInfo(GameDirectory, out var company, out var product))
        {
            Error = "没能读取游戏的 app.info（游戏目录不完整？）。";
            return;
        }

        var low = string.IsNullOrWhiteSpace(localLowRoot) ? LocalLowRoot() : localLowRoot!;
        if (string.IsNullOrWhiteSpace(low))
        {
            Error = "无法定位 LocalLow 目录。";
            return;
        }

        Company = company;
        Product = product;
        SourceDirectory = Path.Combine(low, company, product);
        ProfilesRoot = SourceDirectory + "_profiles";
    }

    /// <summary>%USERPROFILE%\AppData\LocalLow（取不到 USERPROFILE 时退回 %LOCALAPPDATA% 的父目录）。</summary>
    public static string? LocalLowRoot()
    {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(userProfile))
            return Path.Combine(userProfile, "AppData", "LocalLow");

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var parent = string.IsNullOrEmpty(local) ? null : Path.GetDirectoryName(local);
        return string.IsNullOrEmpty(parent) ? null : Path.Combine(parent, "LocalLow");
    }

    /// <summary>读 &lt;游戏目录&gt;\&lt;exe 名&gt;_Data\app.info（两行 UTF-8：公司 / 产品）。</summary>
    public static bool TryReadAppInfo(string gameDirectory, out string company, out string product)
    {
        company = "";
        product = "";
        var dataDirectory = FindDataDirectory(gameDirectory);
        if (dataDirectory is null) return false;

        var appInfo = Path.Combine(dataDirectory, "app.info");
        if (!File.Exists(appInfo)) return false;

        try
        {
            var lines = File.ReadAllLines(appInfo);
            if (lines.Length < 2) return false;
            company = lines[0].Trim();
            product = lines[1].Trim();
            return company.Length > 0 && product.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindDataDirectory(string gameDirectory)
    {
        try
        {
            var exes = Directory.GetFiles(gameDirectory, "*.exe", SearchOption.TopDirectoryOnly);
            foreach (var exe in exes)
            {
                var candidate = Path.Combine(gameDirectory, Path.GetFileNameWithoutExtension(exe) + "_Data");
                if (Directory.Exists(candidate)) return candidate;
            }

            // 兜底：扫描 *_Data（exe 被改名/复制过时也能用）
            foreach (var dir in Directory.GetDirectories(gameDirectory, "*_Data", SearchOption.TopDirectoryOnly))
            {
                if (File.Exists(Path.Combine(dir, "app.info"))) return dir;
            }
        }
        catch
        {
            // 网络位置离线等：当作找不到
        }
        return null;
    }

    /// <summary>profile 名（新建 / 改名时用）：只允许英文字母、数字、下划线，最多 32 个字符。
    /// 与页面上的实时校验共用 <see cref="NamePattern"/>，改规则只改这里。</summary>
    public static bool IsValidName(string? name, out string error)
    {
        error = "";
        var value = (name ?? "").Trim();
        if (value.Length == 0)
        {
            error = "请填写 profile 名称（只能用英文、数字和下划线）。";
            return false;
        }
        if (value.Length > MaxNameLength)
        {
            error = $"profile 名称最多 {MaxNameLength} 个字符。";
            return false;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, NamePattern))
        {
            error = "profile 名称只能用英文、数字和下划线（不支持中文、空格和符号）。";
            return false;
        }
        if (ReservedNames.Contains(value))
        {
            error = "这个名字是 Windows 的保留设备名，换一个吧。";
            return false;
        }
        return true;
    }

    /// <summary>能安全地当目录名用（不限制字符集）：用于打开 / 删除**已存在**的档案，
    /// 这样以前手写配置留下的中文名档案也能正常管理。</summary>
    public static bool IsSafeDirectoryName(string? name, out string error)
    {
        error = "";
        var value = (name ?? "").Trim();
        if (value.Length == 0 || value is "." or "..")
        {
            error = "这个 profile 名称不合法。";
            return false;
        }
        foreach (var c in value)
        {
            if (c < 0x20 || c == 0x7F || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
            {
                error = "这个 profile 名称不合法。";
                return false;
            }
        }
        return true;
    }

    /// <summary>列出全部档案（默认档案排最前）。activeName 为空 = 默认档案在用。</summary>
    public IReadOnlyList<AccountProfileInfo> List(string? activeName)
    {
        var result = new List<AccountProfileInfo>();
        if (SourceDirectory is null || ProfilesRoot is null) return result;

        var active = (activeName ?? "").Trim();
        result.Add(new AccountProfileInfo(
            "",
            SourceDirectory,
            IsDefault: true,
            Active: active.Length == 0,
            Exists: Directory.Exists(SourceDirectory),
            Bytes: MeasureBytes(SourceDirectory),
            ReplayCount: CountReplays(SourceDirectory),
            LastWriteUtc: LastPlayed(SourceDirectory),
            CacheSeeded: true,
            Note: ""));

        var found = false;
        if (Directory.Exists(ProfilesRoot))
        {
            IEnumerable<string> directories;
            try { directories = Directory.GetDirectories(ProfilesRoot); }
            catch { directories = Array.Empty<string>(); }

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);
                if (name.Length == 0) continue;
                var isActive = string.Equals(name, active, StringComparison.OrdinalIgnoreCase);
                if (isActive) found = true;
                result.Add(new AccountProfileInfo(
                    name,
                    directory,
                    IsDefault: false,
                    Active: isActive,
                    Exists: true,
                    Bytes: MeasureBytes(directory),
                    ReplayCount: CountReplays(directory),
                    LastWriteUtc: LastPlayed(directory),
                    CacheSeeded: File.Exists(Path.Combine(directory, SeedMarkerName)),
                    Note: ReadNote(directory)));
            }
        }

        // 配置指向的档案已经被删了（或写的是绝对路径）：仍然显示出来，让用户看得见"当前失效"。
        if (active.Length > 0 && !found)
        {
            result.Add(new AccountProfileInfo(
                active,
                Path.IsPathRooted(active) ? active : Path.Combine(ProfilesRoot, active),
                IsDefault: false,
                Active: true,
                Exists: false,
                Bytes: 0,
                ReplayCount: 0,
                LastWriteUtc: default,
                CacheSeeded: false,
                Note: ""));
        }

        return result
            .OrderBy(p => p.IsDefault ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>profile 目录的完整路径；名称会导致路径穿越或服务不可用时返回 null。
    /// 这里只做"能不能当目录名"的检查（不限制字符集），所以中文名等历史档案也能打开/删除。</summary>
    public string? ResolveDirectory(string name)
    {
        if (ProfilesRoot is null || !IsSafeDirectoryName(name, out _)) return null;
        return Path.Combine(ProfilesRoot, name.Trim());
    }

    /// <summary>新建 profile 目录（账号文件由加载器在首次启动时补齐）。返回新目录路径。</summary>
    public string Create(string name)
    {
        if (ProfilesRoot is null) throw new InvalidOperationException(Error ?? "无法定位 profile 目录。");
        if (!IsValidName(name, out var error)) throw new ArgumentException(error, nameof(name));

        var directory = ResolveDirectory(name)!;
        if (Directory.Exists(directory)) throw new InvalidOperationException($"已经有一个叫「{name.Trim()}」的 profile 了。");

        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>改名（只动目录名；加载器配置里的值由调用方同步）。</summary>
    public string Rename(string name, string newName)
    {
        if (ProfilesRoot is null) throw new InvalidOperationException(Error ?? "无法定位 profile 目录。");
        var source = ResolveDirectory(name) ?? throw new ArgumentException("这个 profile 不存在。", nameof(name));
        if (!IsValidName(newName, out var error)) throw new ArgumentException(error, nameof(newName));
        if (!Directory.Exists(source)) throw new InvalidOperationException("这个 profile 目录已经不存在了。");

        var target = ResolveDirectory(newName)!;
        if (Directory.Exists(target)) throw new InvalidOperationException($"已经有一个叫「{newName.Trim()}」的 profile 了。");

        Directory.Move(source, target);
        return target;
    }

    /// <summary>读取备注（没有备注文件 / 文件损坏都返回空串，绝不让列表失败）。</summary>
    public static string ReadNote(string directory)
    {
        try
        {
            var path = Path.Combine(directory, NoteFileName);
            if (!File.Exists(path)) return "";
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("note", out var note) && note.ValueKind == System.Text.Json.JsonValueKind.String
                ? (note.GetString() ?? "").Trim()
                : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>写备注（空串 = 删除备注文件）。备注只保存在这个档案目录里。</summary>
    public void SetNote(string name, string? note)
    {
        if (ProfilesRoot is null) throw new InvalidOperationException(Error ?? "无法定位 profile 目录。");

        var directory = ResolveDirectory(name) ?? throw new ArgumentException("这个 profile 名称不合法。", nameof(name));
        if (!Directory.Exists(directory)) throw new InvalidOperationException("这个 profile 目录已经不存在了。");

        var value = (note ?? "").Trim();
        if (value.Length > MaxNoteLength) value = value[..MaxNoteLength];

        var path = Path.Combine(directory, NoteFileName);
        if (value.Length == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        var json = System.Text.Json.JsonSerializer.Serialize(
            new { note = value },
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                // 备注是给人看的：中文保持原样，不要写成 \uXXXX
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
    }

    /// <summary>删除 profile 目录（优先回收站，回收站不可用时才永久删除）。</summary>
    internal DeleteOutcome Delete(string name, out string error)
    {
        error = "";
        if (ProfilesRoot is null)
        {
            error = Error ?? "无法定位 profile 目录。";
            return DeleteOutcome.Failed;
        }

        var directory = ResolveDirectory(name);
        if (directory is null)
        {
            error = "这个 profile 名称不合法。";
            return DeleteOutcome.Failed;
        }
        if (!Directory.Exists(directory))
        {
            error = "这个 profile 目录已经不存在了。";
            return DeleteOutcome.Failed;
        }
        return RecycleBinHelper.Delete(directory, out error);
    }

    // ============================== 统计 ==============================

    /// <summary>目录占用（**不含** com.unity.addressables：那是与其它 profile 共享的硬链接缓存）。</summary>
    private static long MeasureBytes(string directory)
    {
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(directory);
        var depth = 0;
        while (pending.Count > 0 && depth < 20000)
        {
            var current = pending.Pop();
            depth++;
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
                foreach (var child in Directory.EnumerateDirectories(current))
                {
                    if (string.Equals(Path.GetFileName(child), CacheFolderName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    pending.Push(child);
                }
            }
            catch
            {
                // 权限/离线：忽略这一段
            }
        }
        return total;
    }

    /// <summary>回放条数：游戏的回放目录下每个条目是一局（&lt;id&gt;\&lt;id&gt; 或单文件）。</summary>
    private static int CountReplays(string directory)
    {
        var replayRoot = Path.Combine(directory, "Temp", "Replay");
        try
        {
            return Directory.Exists(replayRoot) ? Directory.EnumerateFileSystemEntries(replayRoot).Count() : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>“最近使用”：只看 SaveFile.es3（本地设置，每次启动游戏都会写）。
    /// 目录时间不能用 —— 新建 profile 时它等于"创建时间"，会让新档显示成"刚用过"。</summary>
    private static DateTime LastPlayed(string directory)
    {
        try
        {
            var save = Path.Combine(directory, "Temp", "SaveFile.es3");
            if (File.Exists(save)) return File.GetLastWriteTimeUtc(save);
        }
        catch
        {
            // 忽略
        }
        return default;
    }
}
