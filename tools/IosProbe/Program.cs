// iOS 容器能力探针（M0 技术验证）。
//
// 只回答一个问题：纯 .NET 能不能通过 house_arrest(VendDocuments) 读 / 写 / 删
// 游戏 app 容器 Documents 里的文件。方法论见 docs/iOS集成到Toys-方案-2026-11-24.md 第 4.1 节。
//
// 安全约束：只在容器根下写自己命名的 cesium-probe-* 文件，除非 --keep 否则跑完自己删掉；
// 绝不改动游戏的任何既有文件（catalog、__data 都只读）。
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Netimobiledevice;
using Netimobiledevice.Afc;
using Netimobiledevice.InstallationProxy;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;

namespace IosProbe;

internal static class Program
{
    /// <summary>国服 iOS 包名与其余候选（见 iOS/cesium_ios/paths.py）。</summary>
    private static readonly string[] GameBundleCandidates =
    [
        "com.feimo.astralpartycn",
        "com.feimo.astralparty",
        "com.feimo.jxpd",
        "com.feimo.astralparty.cesium",
    ];

    private const string ProbePrefix = "cesium-probe-";

    private static readonly List<(string Name, bool Ok, string Detail)> Checks = [];

    private static async Task<int> Main(string[] args)
    {
        var cli = Cli.Parse(args);
        if (cli.Command is null or "help" or "--help" or "-h")
        {
            PrintUsage();
            return cli.Command is null ? 1 : 0;
        }

        var logger = new ConsoleLogger(cli.Verbose ? LogLevel.Debug : LogLevel.Information);

        try
        {
            return cli.Command switch
            {
                "devices" => CmdDevices(),
                "apps" => await CmdAppsAsync(cli, logger),
                "ls" => await CmdLsAsync(cli, logger),
                "stat" => await CmdStatAsync(cli, logger),
                "read" => await CmdReadAsync(cli, logger),
                "write" => await CmdWriteAsync(cli, logger),
                "rm" => await CmdRmAsync(cli, logger),
                "mkdir" => await CmdMkdirAsync(cli, logger),
                "rename" => await CmdRenameAsync(cli, logger),
                "afcprobe" => await CmdAfcProbeAsync(cli, logger),
                "lsdepth" => await CmdLsDepthAsync(cli, logger),
                "selftest" => await CmdSelfTestAsync(cli, logger),
                _ => Unknown(cli.Command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"[fail] {ex.GetType().Name}: {ex.Message}");
            if (cli.Verbose) Console.Error.WriteLine(ex);
            return 2;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"未知子命令：{command}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            iOS 容器能力探针（house_arrest / AFC）

              devices                                 列出 usbmux 上的设备
              apps [--bundle <id>]                     列出已安装 app（默认只列国服候选与匹配项）
              ls [<relative-path>]                     列容器目录（默认根）
              stat <relative-path>                     取文件信息（st_size / st_mtime）
              read <relative-path> [<local-out>]       读文件（不给本地路径就只算哈希）
              write <local-file> <relative-path>       写文件
              rm <relative-path>                       删文件
              mkdir <relative-path>                    建目录（自补 AFC MAKE_DIR，库里没有）
              rename <old-relative-path> <new-relative-path>
                                                       改名（自补 AFC RENAME_PATH，库里没有）
              afcprobe                                 自补 AFC 操作实测：opcode/状态码 + 建目录/改名/删除全流程
              lsdepth [<relative-path>]                实测 LsDirectory 的 depth 语义，并与自补的浅层 READ_DIR 对比
              selftest                                 读 / 写 / 覆盖 / 分块写 / 删 全流程自检（默认走这个）

            选项：
              --udid <serial>      指定设备（默认第一台）
              --bundle <id>        指定 app 包名（默认按国服候选自动识别）
              --keep               自检结束时保留探针文件（默认删掉）
              --verbose            打开 Netimobiledevice 内部日志（Debug）
              --size <bytes>       分块写测试的字节数（默认 1 MiB）
              --makedir-op <n>     改 MAKE_DIR 的 opcode（默认 9；用来实测编号有争议的操作）
              --rename-op <n>      改 RENAME_PATH 的 opcode（默认 24）

            说明：house_arrest 的 AFC 根通常就是该 app 的 Documents，所以路径写相对形式
            （例如 com.unity.addressables/catalog_3.3.0.json）。自检会自动探测这个根。
            """);
    }

    // ────────────────────────────────── 命令

    private static int CmdDevices()
    {
        var devices = Usbmux.GetDeviceList();
        if (devices.Count == 0)
        {
            Console.WriteLine("[fail] usbmux 上没有设备：确认 USB 连接、设备已解锁并「信任此电脑」，");
            Console.WriteLine("       Windows 上还需要 Apple Mobile Device Service 在运行。");
            return 2;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            Console.WriteLine($"[{i}] serial={d.Serial}  deviceId={d.DeviceId}  connection={d.ConnectionType}");
        }
        return 0;
    }

    private static async Task<int> CmdAppsAsync(Cli cli, ILogger logger)
    {
        var (lockdown, device) = OpenLockdown(cli, logger);
        using (lockdown)
        {
            PrintDeviceInfo(lockdown, device);
            var apps = await BrowseAppsAsync(lockdown, logger);
            Console.WriteLine($"[ok] 已安装 app {apps.Count} 个");
            Console.WriteLine();
            foreach (var app in apps)
            {
                var id = app.Text("CFBundleIdentifier");
                var isGame = GameBundleCandidates.Contains(id, StringComparer.OrdinalIgnoreCase);
                var matches = cli.Bundle is null || string.Equals(id, cli.Bundle, StringComparison.OrdinalIgnoreCase);
                if (cli.Bundle is null && !isGame && !app.Flag("IsApplication")) continue;
                if (!matches) continue;
                Console.WriteLine($"  {(isGame ? "*" : " ")} {id,-42} {app.Text("CFBundleShortVersionString"),-10} "
                                  + $"{app.Text("CFBundleDisplayName") ?? app.Text("CFBundleName")}");
            }
            Console.WriteLine();
            Console.WriteLine("  * = 国服/候选包名");
        }
        return 0;
    }

    private static async Task<int> CmdLsAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenContainerAsync(cli, logger);
        var path = cli.Positional.FirstOrDefault() ?? ".";
        var entries = new List<string>();
        await foreach (var entry in session.House.LsDirectory(path, CancellationToken.None, depth: 1))
            entries.Add(entry);

        Console.WriteLine($"[ok] {path} 下 {entries.Count} 项");
        foreach (var e in entries.OrderBy(x => x, StringComparer.Ordinal)) Console.WriteLine("  " + e);
        return 0;
    }

    private static async Task<int> CmdStatAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenContainerAsync(cli, logger);
        var path = Require(cli, 0, "stat <relative-path>");
        await PrintStatAsync(session.House, path);
        return 0;
    }

    private static async Task<int> CmdReadAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenContainerAsync(cli, logger);
        var path = Require(cli, 0, "read <relative-path> [<local-out>]");
        var data = await session.House.GetFileContents(path, CancellationToken.None);
        Console.WriteLine($"[ok] 读到 {data.Length} B  sha256={Sha(data)}");
        var outPath = cli.Positional.ElementAtOrDefault(1);
        if (outPath is not null)
        {
            await File.WriteAllBytesAsync(outPath, data);
            Console.WriteLine($"[ok] 已写本地 {Path.GetFullPath(outPath)}");
        }
        return 0;
    }

    private static async Task<int> CmdWriteAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenContainerAsync(cli, logger);
        var local = Require(cli, 0, "write <local-file> <relative-path>");
        var remote = Require(cli, 1, "write <local-file> <relative-path>");
        var data = await File.ReadAllBytesAsync(local);
        await session.House.SetFileContents(remote, data, CancellationToken.None);
        Console.WriteLine($"[ok] 写入 {remote}  {data.Length} B  sha256={Sha(data)}");
        return 0;
    }

    private static async Task<int> CmdRmAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenContainerAsync(cli, logger);
        var path = Require(cli, 0, "rm <relative-path>");
        var leftovers = await session.House.Rm(path, CancellationToken.None, force: true);
        Console.WriteLine(leftovers.Count == 0 ? $"[ok] 已删除 {path}" : $"[fail] 未能删除：{string.Join(", ", leftovers)}");
        return leftovers.Count == 0 ? 0 : 1;
    }

    // ────────────────────────────────── 自补 AFC 操作（库缺 MakeDir / RenamePath）

    /// <summary>
    /// 自己开一条 house_arrest 连接。`LockdownServiceProvider.StartLockdownService` 与
    /// `ServiceConnection` 都是公开的，握手内容与 `HouseArrestService.SendCommand` 逐字一致
    /// （发 `{Command: VendDocuments, Identifier: &lt;bundleId&gt;}`）。
    /// 这样就不必反射去读基类那个 protected 的 `Service`，生产代码也能照这个形状写。
    /// </summary>
    private static async Task<RawSession> OpenRawSessionAsync(Cli cli, ILogger logger)
    {
        var (lockdown, _) = OpenLockdown(cli, logger);
        var bundleId = cli.Bundle ?? await ResolveBundleQuietAsync(cli, lockdown, logger);
        var connection = lockdown.StartLockdownService("com.apple.mobile.house_arrest");
        var request = new DictionaryNode
        {
            { "Command", new StringNode("VendDocuments") },
            { "Identifier", new StringNode(bundleId) },
        };
        var reply = (await connection.SendReceivePlistAsync(request, CancellationToken.None))?.AsDictionaryNode();
        if (reply is not null && reply.TryGetValue("Error", out var error))
        {
            connection.Dispose();
            lockdown.Dispose();
            throw new InvalidOperationException(
                $"house_arrest VendDocuments 失败：{error.AsStringNode().Value}（包名 {bundleId}）");
        }
        return new RawSession(lockdown, connection, bundleId);
    }

    private static async Task<int> CmdMkdirAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenRawSessionAsync(cli, logger);
        var path = Require(cli, 0, "mkdir <relative-path>");
        var afc = new RawAfcClient(session.Connection) { MakeDirOp = cli.MakeDirOp };
        var reply = await afc.MakeDirectoryAsync(path);
        Console.WriteLine($"[{(reply.Ok ? "ok" : "fail")}] MAKE_DIR(0x{cli.MakeDirOp:X2}) {path} -> {reply.StatusName}");
        return reply.Ok ? 0 : 1;
    }

    private static async Task<int> CmdRenameAsync(Cli cli, ILogger logger)
    {
        using var session = await OpenRawSessionAsync(cli, logger);
        var from = Require(cli, 0, "rename <old-relative-path> <new-relative-path>");
        var to = Require(cli, 1, "rename <old-relative-path> <new-relative-path>");
        var afc = new RawAfcClient(session.Connection) { RenameOp = cli.RenameOp };
        var reply = await afc.RenameAsync(from, to);
        Console.WriteLine($"[{(reply.Ok ? "ok" : "fail")}] RENAME_PATH(0x{cli.RenameOp:X2}) {from} -> {to} : {reply.StatusName}");
        return reply.Ok ? 0 : 1;
    }

    /// <summary>
    /// 自补 AFC 操作的实测：先用只读探针确认 opcode 被认出来、状态码读得对，
    /// 再走一遍「建目录 → 建嵌套目录 → 改名 → 删除」，最后要求容器回到改动前一模一样。
    /// 只碰自己命名的 <c>cesium-probe-m0-*</c> 目录，不动游戏的任何既有文件。
    /// </summary>
    private static async Task<int> CmdAfcProbeAsync(Cli cli, ILogger logger)
    {
        Checks.Clear();
        var stamp = DateTime.Now.ToString("MMdd-HHmmss");
        var probeDir = $"Documents/{ProbePrefix}m0-{stamp}";
        var deepDir = $"{probeDir}/child/grandchild";
        var helloFile = $"{deepDir}/hello.txt";
        var renamedDir = $"Documents/{ProbePrefix}m0-{stamp}-renamed";
        var helloRenamed = $"{renamedDir}/child/grandchild/hello.txt";

        Console.WriteLine("=== 自补 AFC 操作实测（MAKE_DIR / RENAME_PATH）===");
        Console.WriteLine();

        Console.WriteLine("── 0. 改动前快照（走独立的 house_arrest 通道，免得自己看自己）──");
        List<string> before;
        using (var snapshot = await OpenContainerAsync(cli, logger))
            before = await ListAsync(snapshot.House, "Documents");
        Console.WriteLine($"  Documents 下现有 {before.Count} 项");

        using var session = await OpenRawSessionAsync(cli, logger);
        var afc = new RawAfcClient(session.Connection) { MakeDirOp = cli.MakeDirOp, RenameOp = cli.RenameOp };
        Console.WriteLine($"[ok] 裸 AFC 通道就绪（包名 {session.BundleId}）；"
                          + $"MAKE_DIR=0x{cli.MakeDirOp:X2}，RENAME_PATH=0x{cli.RenameOp:X2}");

        Console.WriteLine();
        Console.WriteLine("── 1. 只读探针：opcode 有没有被认出来、状态码读得对不对 ──");
        // 唯一真正只读的 MAKE_DIR 探针：打在一个已存在的目录上。MAKE_DIR 是 mkdir -p 语义，
        // 已存在返回 Success（不是 ObjectExists），所以「Success」在这里恰好同时证明了 opcode 被认出来
        // —— 认不出来的 opcode 会回 UnknownPacketType / OpNotSupported。
        var onExisting = await afc.MakeDirectoryAsync("Documents");
        Console.WriteLine($"  MAKE_DIR Documents（已存在）  -> {onExisting.StatusName}");
        Record("MAKE_DIR opcode 被识别", onExisting.Ok, $"打在已存在目录上实得 {onExisting.StatusName}");

        var renameMissing = await afc.RenameAsync($"{probeDir}-nope", $"{probeDir}-nope2");
        Console.WriteLine($"  RENAME_PATH 源不存在          -> {renameMissing.StatusName}");
        Record("RENAME_PATH 源不存在", renameMissing.Status == (ulong)AfcError.ObjectNotFound,
            $"期望 ObjectNotFound，实得 {renameMissing.StatusName}");

        Console.WriteLine();
        Console.WriteLine("── 2. 建目录：一次建出多级（父目录本来不存在）──");
        var mkDeep = await afc.MakeDirectoryAsync(deepDir);
        Console.WriteLine($"  MAKE_DIR {deepDir} -> {mkDeep.StatusName}");
        Record("MAKE_DIR 一次建多级", mkDeep.Ok, $"{deepDir} → {mkDeep.StatusName}");

        var payload = Encoding.UTF8.GetBytes($"cesium afc probe {stamp}\n");

        using (var verify = await OpenContainerAsync(cli, logger))
        {
            var l1 = await verify.House.Exists(probeDir, CancellationToken.None);
            var l3 = await verify.House.Exists(deepDir, CancellationToken.None);
            var isDir = await verify.House.IsDir(deepDir, CancellationToken.None);
            Console.WriteLine($"  复查：{probeDir} exists={l1}，{deepDir} exists={l3} isDir={isDir}");
            Record("多级目录确实建出来了", l1 && l3 && isDir, $"l1={l1} l3={l3} isDir={isDir}");

            Console.WriteLine();
            Console.WriteLine("── 3. 往嵌套目录写文件（原缺口本体：写 Documents/<dir>/child.txt 曾 ObjectNotFound）──");
            await verify.House.SetFileContents(helloFile, payload, CancellationToken.None);
            var readBack = await verify.House.GetFileContents(helloFile, CancellationToken.None);
            var same = readBack is not null && readBack.AsSpan().SequenceEqual(payload);
            Console.WriteLine($"  写入 {helloFile}  {payload.Length} B，回读一致={same}");
            Record("往嵌套目录写文件", same, $"{helloFile}  {payload.Length} B");
        }

        Console.WriteLine();
        Console.WriteLine("── 4. 改名（整棵目录树）──");
        var renamed = await afc.RenameAsync(probeDir, renamedDir);
        Console.WriteLine($"  RENAME_PATH {probeDir} -> {renamedDir} : {renamed.StatusName}");
        Record("RENAME_PATH 改目录名", renamed.Ok, renamed.StatusName);

        using (var verify = await OpenContainerAsync(cli, logger))
        {
            var oldGone = !await verify.House.Exists(probeDir, CancellationToken.None);
            var newThere = await verify.House.Exists(renamedDir, CancellationToken.None);
            var deepThere = await verify.House.Exists($"{renamedDir}/child/grandchild", CancellationToken.None);
            var fileBack = await verify.House.GetFileContents(helloRenamed, CancellationToken.None);
            var fileSame = fileBack is not null && fileBack.AsSpan().SequenceEqual(payload);
            Console.WriteLine($"  原名存在={!oldGone}  新名存在={newThere}  深层目录存在={deepThere}  深层文件还在={fileSame}");
            Record("改名后原名不存在", oldGone, probeDir);
            Record("改名后新名存在", newThere, renamedDir);
            Record("改名后整棵树跟着走", deepThere && fileSame, $"{renamedDir}/child/grandchild");
        }

        Console.WriteLine();
        Console.WriteLine(cli.Keep ? "── 5. 删除与清理：--keep，跳过 ──" : "── 5. 删除语义与清理 ──");
        if (!cli.Keep)
        {
            var onNonEmpty = await afc.RemovePathAsync(renamedDir);
            Console.WriteLine($"  REMOVE_PATH 非空目录 {renamedDir} -> {onNonEmpty.StatusName}（预期失败）");
            Record("REMOVE_PATH 拒删非空目录", !onNonEmpty.Ok, $"实得 {onNonEmpty.StatusName}");

            foreach (var (path, label) in new[]
                     {
                         (helloRenamed, "深层文件"),
                         ($"{renamedDir}/child/grandchild", "最深目录"),
                         ($"{renamedDir}/child", "中间目录"),
                         (renamedDir, "顶层目录"),
                     })
            {
                var reply = await afc.RemovePathAsync(path);
                Console.WriteLine($"  REMOVE_PATH({label}) {path} -> {reply.StatusName}");
                Record($"REMOVE_PATH {label}", reply.Ok, reply.StatusName);
            }

            // 兜底：万一还有残留，用库里那套递归 Rm 清干净，并把它记进结论
            using var cleanup = await OpenContainerAsync(cli, logger);
            if (await cleanup.House.Exists(renamedDir, CancellationToken.None))
            {
                var leftovers = await cleanup.House.Rm(renamedDir, CancellationToken.None, force: true);
                Console.WriteLine($"  [兜底] 仍有残留，用递归 Rm 清理：剩 {leftovers.Count} 项");
                Record("兜底递归清理", leftovers.Count == 0, string.Join(", ", leftovers));
            }
        }

        Console.WriteLine();
        Console.WriteLine("── 6. 改动后快照：容器必须回到原样 ──");
        List<string> after;
        using (var snapshot = await OpenContainerAsync(cli, logger))
            after = await ListAsync(snapshot.House, "Documents");
        var added = after.Except(before).ToList();
        var removed = before.Except(after).ToList();
        Console.WriteLine($"  Documents 现有 {after.Count} 项（改动前 {before.Count} 项）");
        foreach (var item in added.Take(10)) Console.WriteLine($"      多出：{item}");
        foreach (var item in removed.Take(10)) Console.WriteLine($"      少了：{item}");
        Record(cli.Keep ? "Documents 快照（--keep，允许留有探针目录）" : "Documents 与改动前一致",
            cli.Keep || (added.Count == 0 && removed.Count == 0),
            $"多出 {added.Count} 项、少了 {removed.Count} 项");

        Console.WriteLine();
        Console.WriteLine("=== 结论 ===");
        foreach (var (name, ok, detail) in Checks)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name,-30} {detail}");
        var failed = Checks.Count(c => !c.Ok);
        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? "全部通过：可以在 Netimobiledevice 之上自补 MAKE_DIR / RENAME_PATH，多级目录与改名可用。"
            : $"有 {failed} 项未通过，见上表。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// 实测 <c>LsDirectory</c> 的 depth 语义，并与自补的浅层 READ_DIR 对比。
    /// 结论在库源码里也看得到（<c>Walk</c> 永远是全树递归 + 逐条 <c>GetFileInfo</c>；
    /// 深度过滤数的是 `Path.DirectorySeparatorChar`，Windows 上是 '\'，而路径里是 '/'
    /// → 过滤恒不生效），这里用真机数据把它钉住。
    /// </summary>
    private static async Task<int> CmdLsDepthAsync(Cli cli, ILogger logger)
    {
        var path = cli.Positional.Count > 0 ? cli.Positional[0] : "Documents/com.unity.addressables";
        var baseSlash = path.Count(c => c == '/');
        Console.WriteLine($"=== LsDirectory 深度语义实测：{path} ===");
        Console.WriteLine();

        {
            using var session = await OpenContainerAsync(cli, logger);
            foreach (var depth in new[] { 0, 1, 2, -1 })
            {
                var sw = Stopwatch.StartNew();
                var entries = new List<string>();
                await foreach (var entry in session.House.LsDirectory(path, CancellationToken.None, depth: depth))
                    entries.Add(entry.Replace('\\', '/'));
                sw.Stop();
                var deeper = entries.Count(e => e.TrimEnd('/').Count(c => c == '/') > baseSlash);
                var deepest = entries.Count == 0 ? 0 : entries.Max(e => e.TrimEnd('/').Count(c => c == '/'));
                Console.WriteLine($"  LsDirectory(depth={depth,-2}) 条目 {entries.Count,5}  {sw.ElapsedMilliseconds,6} ms  " +
                                  $"比传入路径更深的条目 {deeper,5} 条，最深层级 {deepest}（传入路径层级 {baseSlash}）");
            }
        }

        Console.WriteLine();
        {
            using var raw = await OpenRawSessionAsync(cli, logger);
            var afc = new RawAfcClient(raw.Connection);
            var sw = Stopwatch.StartNew();
            var names = await afc.ReadDirectoryAsync(path);
            sw.Stop();
            Console.WriteLine($"  自补 READ_DIR（浅层）   条目 {names.Count,5}  {sw.ElapsedMilliseconds,6} ms  不递归、不逐条 GetFileInfo");
            Console.WriteLine($"      {string.Join("  ", names.Take(14))}{(names.Count > 14 ? "  …" : "")}");
        }

        Console.WriteLine();
        Console.WriteLine("说明：depth 只过滤输出、不限制遍历，所以每个 depth 的耗时都是「整棵树」的耗时；");
        Console.WriteLine("      depth=0 只吐传入路径自身那一条；depth>=1 会连深层后代一起吐出来（过滤在 Windows 上失效）。");
        Console.WriteLine("      M1 的设备文件页要懒加载，用自补的 READ_DIR 逐层列，不要用 LsDirectory。");
        return 0;
    }

    // ────────────────────────────────── 自检

    private static async Task<int> CmdSelfTestAsync(Cli cli, ILogger logger)
    {
        Console.WriteLine("=== iOS 容器能力自检（house_arrest / AFC）===");
        Console.WriteLine();

        var (lockdown, device) = OpenLockdown(cli, logger);
        using (lockdown)
        {
            PrintDeviceInfo(lockdown, device);

            Console.WriteLine("── 识别游戏 app ──");
            var bundleId = await ResolveBundleAsync(cli, lockdown, logger);
            Console.WriteLine($"[ok] 包名 {bundleId}");
            Console.WriteLine();

            Console.WriteLine("── 建立容器通道（VendDocuments）──");
            var sw = Stopwatch.StartNew();
            var house = await HouseArrestService.Create(lockdown, bundleId, documentsOnly: true, logger: logger,
                cancellationToken: CancellationToken.None);
            sw.Stop();
            using (house)
            {
                Record("建立 house_arrest 通道", true, $"{sw.ElapsedMilliseconds} ms");
                Console.WriteLine($"[ok] 通道已建立（{sw.ElapsedMilliseconds} ms）");

                // 1) 通道语义 —— AFC 根在哪、可写前缀是什么
                Console.WriteLine();
                Console.WriteLine("── 通道语义（AFC 根 / Documents）──");
                var (baseDir, note) = await ResolveBaseDirAsync(house);
                Record("定位可写目录", baseDir is not null, note);
                if (baseDir is null)
                {
                    Console.WriteLine("[fail] 找不到可访问的目录前缀，后面的读写检查跳过。");
                }
                else
                {
                    Console.WriteLine($"[ok] 可写前缀 = '{baseDir}'");
                    Console.WriteLine($"[ok] 目录内容：{baseDir}");

                    // 2) documentsOnly 标志的实际含义（信息项，不判定成败）
                    Console.WriteLine();
                    Console.WriteLine("── documentsOnly 标志的实际语义 ──");
                    await FlagSemanticsAsync(lockdown, bundleId, logger, house);

                    // 3) 只读游戏既有文件 —— 证明这是真容器且我们能读游戏数据
                    Console.WriteLine();
                    Console.WriteLine("── 读游戏既有文件（只读）──");
                    await ReadGameFileAsync(house, baseDir);

                    // 4) 写 / 回读 / 覆盖 / 分块 / 删
                    Console.WriteLine();
                    Console.WriteLine("── 写 / 回读 / 删 ──");
                    await WriteDeleteChecksAsync(house, cli, baseDir);

                    // 5) 已知缺口：嵌套目录
                    Console.WriteLine();
                    Console.WriteLine("── 嵌套目录（预期的缺口）──");
                    await NestedDirCheckAsync(house, cli, baseDir);
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== 结论 ===");
        foreach (var (name, ok, detail) in Checks)
            Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name,-28} {detail}");
        var failed = Checks.Count(c => !c.Ok);
        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? "全部通过：C# 可以直接读写删该 app 容器 Documents 里的文件。"
            : $"有 {failed} 项未通过，见上表。");
        return failed == 0 ? 0 : 1;
    }

    private static async Task<string> ResolveBundleAsync(Cli cli, LockdownClient lockdown, ILogger logger)
    {
        if (cli.Bundle is not null)
        {
            Console.WriteLine($"[ok] 使用 --bundle 指定：{cli.Bundle}");
            return cli.Bundle;
        }

        var apps = await BrowseAppsAsync(lockdown, logger);
        var installed = apps.Select(a => a.Text("CFBundleIdentifier"))
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in GameBundleCandidates)
        {
            if (installed.Contains(candidate))
            {
                Console.WriteLine($"[ok] 已安装的候选包名：{candidate}");
                return candidate;
            }
        }
        throw new InvalidOperationException(
            "设备上没找到游戏的候选包名（" + string.Join("、", GameBundleCandidates) + "）。"
            + "用 `apps` 看全部已安装 app，再 `--bundle <id>` 指定。");
    }

    private static async Task<List<DictionaryNode>> BrowseAppsAsync(LockdownClient lockdown, ILogger logger)
    {
        using var proxy = new InstallationProxyService(lockdown, logger);
        var node = await proxy.Browse(null, null, CancellationToken.None);
        var list = new List<DictionaryNode>();
        foreach (var item in node)
            if (item is DictionaryNode dict) list.Add(dict);
        return list;
    }

    /// <summary>
    /// 找出「能读能写」的目录前缀。实测形态：house_arrest 的 AFC 根是 app 容器根，
    /// 列根会被拒（PermDenied），游戏数据与可写区都在 Documents 下，所以前缀是 "Documents"。
    /// </summary>
    private static async Task<(string? BaseDir, string Note)> ResolveBaseDirAsync(HouseArrestService house)
    {
        var notes = new List<string>();
        foreach (var candidate in new[] { "Documents", "." })
        {
            try
            {
                var items = await ListAsync(house, candidate);
                notes.Add($"'{candidate}' 可列 {items.Count} 项");
                foreach (var item in items.OrderBy(x => x, StringComparer.Ordinal).Take(15))
                    Console.WriteLine("      " + item);
                if (items.Count > 15) Console.WriteLine($"      …（其余 {items.Count - 15} 项略）");
            }
            catch (Exception ex)
            {
                notes.Add($"'{candidate}' 不可列（{Short(ex)}）");
                continue;
            }

            var gameRoot = Join(candidate, "com.unity.addressables");
            var hasGame = await house.Exists(gameRoot, CancellationToken.None);
            var hasLoader = await house.Exists(Join(candidate, "CesiumLoader"), CancellationToken.None);
            notes.Add($"{gameRoot} exists={hasGame}；{Join(candidate, "CesiumLoader")} exists={hasLoader}");
            Console.WriteLine($"      {gameRoot} exists={hasGame}   CesiumLoader exists={hasLoader}");
            if (hasGame) return (candidate, string.Join("；", notes));
        }

        // 列不出来也还能靠 Exists 判
        foreach (var candidate in new[] { "Documents", "." })
        {
            try
            {
                if (await house.Exists(Join(candidate, "com.unity.addressables"), CancellationToken.None))
                {
                    notes.Add($"'{candidate}' 靠 Exists 命中");
                    return (candidate, string.Join("；", notes));
                }
            }
            catch { /* 换下一个候选 */ }
        }
        return (null, string.Join("；", notes));
    }

    /// <summary>
    /// 把 documentsOnly 两个取值的实际行为记下来（端口时要用对标志）。
    /// 失败只记录，不判定成败。
    /// </summary>
    private static async Task FlagSemanticsAsync(LockdownClient lockdown, string bundleId, ILogger logger,
        HouseArrestService primary)
    {
        Console.WriteLine($"  documentsOnly=true（本进程使用的通道）：Documents/com.unity.addressables exists="
                          + $"{await SafeExistsAsync(primary, "Documents/com.unity.addressables")}"
                          + $"，com.unity.addressables exists={await SafeExistsAsync(primary, "com.unity.addressables")}");
        try
        {
            var other = await HouseArrestService.Create(lockdown, bundleId, documentsOnly: false, logger: logger,
                cancellationToken: CancellationToken.None);
            using (other)
            {
                Console.WriteLine($"  documentsOnly=false：Documents/com.unity.addressables exists="
                                  + $"{await SafeExistsAsync(other, "Documents/com.unity.addressables")}"
                                  + $"，com.unity.addressables exists={await SafeExistsAsync(other, "com.unity.addressables")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  documentsOnly=false：建立通道失败（{Short(ex)}）");
        }
    }

    private static async Task<bool> SafeExistsAsync(HouseArrestService house, string path)
    {
        try { return await house.Exists(path, CancellationToken.None); }
        catch { return false; }
    }

    private static async Task<List<string>> ListAsync(HouseArrestService house, string path)
    {
        var list = new List<string>();
        await foreach (var entry in house.LsDirectory(path, CancellationToken.None, depth: 1))
        {
            var name = entry.Replace('\\', '/');
            var slash = name.TrimEnd('/').LastIndexOf('/');
            list.Add(slash >= 0 ? name.TrimEnd('/')[(slash + 1)..] : name);
        }
        return list;
    }

    private static string Join(string baseDir, string name) =>
        baseDir is "." or "" ? name : $"{baseDir}/{name}";

    private static string Short(Exception ex) => $"{ex.GetType().Name}: {ex.Message.Split('\n')[0]}";

    private static async Task ReadGameFileAsync(HouseArrestService house, string baseDir)
    {
        var catalog = Join(baseDir, "com.unity.addressables/catalog_3.3.0.json");
        try
        {
            var info = await house.GetFileInfo(catalog, CancellationToken.None);
            var size = info.Number("st_size");
            var data = await house.GetFileContents(catalog, CancellationToken.None);
            var head = Encoding.UTF8.GetString(data.Take(48).ToArray());
            Console.WriteLine($"[ok] {catalog}");
            Console.WriteLine($"      st_size={size}  实读 {data.Length} B  sha256={Sha(data)}");
            Console.WriteLine($"      头部：{head.Replace('\n', ' ')}…");
            Record("读游戏既有文件（catalog）", data.Length == size, $"{(size / 1024.0 / 1024.0):N1} MB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[warn] 读 catalog 失败（{Short(ex)}）——不阻塞读写测试");
            Record("读游戏既有文件（catalog）", false, ex.GetType().Name);
        }
    }

    private static async Task WriteDeleteChecksAsync(HouseArrestService house, Cli cli, string baseDir)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var name = Join(baseDir, $"{ProbePrefix}{stamp}.bin");
        var payload = RandomNumberGenerator.GetBytes(256 * 1024);
        // 前置：钉住一个魔数头，便于人眼在小文件里认出来
        Encoding.ASCII.GetBytes("CESIUM-IOS-PROBE").CopyTo(payload, 0);

        try
        {
            // 写
            var sw = Stopwatch.StartNew();
            await house.SetFileContents(name, payload, CancellationToken.None);
            sw.Stop();
            Record("写文件（256 KB）", true, $"{sw.ElapsedMilliseconds} ms");

            // 存在 + 大小
            var exists = await house.Exists(name, CancellationToken.None);
            var info = await house.GetFileInfo(name, CancellationToken.None);
            var size = info.Number("st_size");
            Console.WriteLine($"[ok] 已写 {name}：exists={exists} st_size={size}（期望 {payload.Length}）");
            Record("Exists / GetFileInfo", exists && size == payload.Length, $"st_size={size}");

            // 回读 + 校验
            var readBack = await house.GetFileContents(name, CancellationToken.None);
            var same = readBack.AsSpan().SequenceEqual(payload);
            Console.WriteLine($"[ok] 回读 {readBack.Length} B  sha256={Sha(readBack)}");
            Record("回读逐字节一致", same, same ? "256 KB 一致" : "内容不一致");

            // 覆盖写（相邻大小，验证 truncate 语义）
            var second = RandomNumberGenerator.GetBytes(4096);
            await house.SetFileContents(name, second, CancellationToken.None);
            var readSecond = await house.GetFileContents(name, CancellationToken.None);
            var overwriteOk = readSecond.AsSpan().SequenceEqual(second);
            Console.WriteLine($"[ok] 覆盖写后长度 {readSecond.Length}（期望 4096，没有残留旧尾巴）");
            Record("覆盖写（truncate）", overwriteOk && readSecond.Length == second.Length,
                overwriteOk ? $"新长度 {readSecond.Length}" : "覆盖后内容不对");

            // 分块写（模拟推 8 MB 代码包/载荷）
            var big = RandomNumberGenerator.GetBytes(cli.Size);
            var bigName = Join(baseDir, $"{ProbePrefix}{stamp}-big.bin");
            var sw2 = Stopwatch.StartNew();
            var handle = await house.FileOpen(bigName, CancellationToken.None, AfcFileOpenMode.WriteOnly);
            const int chunk = 64 * 1024;
            for (var offset = 0; offset < big.Length; offset += chunk)
            {
                var slice = big.AsSpan(offset, Math.Min(chunk, big.Length - offset)).ToArray();
                await house.FileWrite(handle, slice, CancellationToken.None, chunkSize: chunk);
            }
            await house.FileClose(handle, CancellationToken.None);
            sw2.Stop();
            var bigBack = await house.GetFileContents(bigName, CancellationToken.None);
            var bigOk = bigBack.AsSpan().SequenceEqual(big);
            var mbPerSec = big.Length / 1024.0 / 1024.0 / Math.Max(sw2.Elapsed.TotalSeconds, 0.001);
            Console.WriteLine($"[ok] 分块写 {big.Length / 1024} KB，用时 {sw2.ElapsedMilliseconds} ms（{mbPerSec:N1} MB/s）"
                              + $"  回读 {bigBack.Length} B  sha256={(bigOk ? "一致" : Sha(bigBack))}");
            Record($"分块写（{big.Length / 1024} KB）", bigOk, $"{mbPerSec:N1} MB/s");

            // 删
            var leftovers = await house.Rm(bigName, CancellationToken.None, force: true);
            var goneBig = !await house.Exists(bigName, CancellationToken.None);
            Record("删除文件（big）", leftovers.Count == 0 && goneBig,
                goneBig ? "已消失" : $"残留 {string.Join(", ", leftovers)}");

            if (cli.Keep)
            {
                Console.WriteLine($"[warn] --keep：保留 {name}（{second.Length} B）");
            }
            else
            {
                var leftovers2 = await house.Rm(name, CancellationToken.None, force: true);
                var gone = !await house.Exists(name, CancellationToken.None);
                Console.WriteLine($"[ok] 删除 {name}：{(gone ? "已消失" : "仍在（" + string.Join(", ", leftovers2) + "）")}");
                Record("删除文件（小）", gone, gone ? "已消失" : "仍有残留");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[fail] 读写删过程中出错：{ex.GetType().Name}: {ex.Message}");
            Record("写/读/删流程", false, ex.Message);
            // 尽力清理
            try { await house.Rm(name, CancellationToken.None, force: true); } catch { /* 清理失败不掩盖原错误 */ }
        }
    }

    /// <summary>
    /// 已知缺口：Netimobiledevice 的 AfcService 没有 MakeDirectory / Rename。
    /// 探针把这一点量出来（写嵌套路径应当失败），部署载荷时必须自己补这两个 AFC 操作。
    /// </summary>
    private static async Task NestedDirCheckAsync(HouseArrestService house, Cli cli, string baseDir)
    {
        var dir = Join(baseDir, $"{ProbePrefix}dir");
        var nested = $"{dir}/child.txt";
        try
        {
            await house.SetFileContents(nested, "x"u8.ToArray(), CancellationToken.None);
            Console.WriteLine($"[note] 意外：直接写 {nested} 成功了（设备/服务会自动建目录？）");
            Record("嵌套目录写入", true, "SetFileContents 自动建了目录");
            await house.Rm(nested, CancellationToken.None, force: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[note] 写 {nested} 按预期失败：{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine("       说明 AfcService 没有 MakeDirectory，部署载荷前必须自己补 AFC 的 MAKE_DIR/");
            Console.WriteLine("       RENAME_PATH（服务连接对象 ServiceConnection.Stream 是公开的，可以自己发 AFC 包）。");
            Record("嵌套目录写入", true, "按预期失败：包内无 Mkdir（已记录为待补项）");
        }

        try
        {
            if (await house.Exists(dir, CancellationToken.None))
            {
                if (!cli.Keep) await house.Rm(dir, CancellationToken.None, force: true);
                Console.WriteLine($"[warn] 容器里残留了 {dir}（用 rm 子命令清理）");
            }
        }
        catch { /* 忽略清理异常 */ }
    }

    // ────────────────────────────────── 基础设施

    private static (LockdownClient Lockdown, UsbmuxdDevice Device) OpenLockdown(Cli cli, ILogger logger)
    {
        var devices = Usbmux.GetDeviceList();
        if (devices.Count == 0)
            throw new InvalidOperationException(
                "usbmux 上没有设备：确认 USB 连接、设备已解锁并「信任此电脑」，"
                + "Windows 上还需要 Apple Mobile Device Service 在运行。");

        var device = cli.Udid is null
            ? devices[0]
            : devices.FirstOrDefault(d => Normalize(d.Serial) == Normalize(cli.Udid))
              ?? throw new InvalidOperationException($"没找到 udid={cli.Udid} 的设备（现有：{string.Join(", ", devices.Select(d => d.Serial))}）");

        var lockdown = MobileDevice.CreateUsingUsbmux(device.Serial, autopair: true, logger: logger);
        return (lockdown, device);
    }

    private static void PrintDeviceInfo(LockdownClient lockdown, UsbmuxdDevice device)
    {
        Console.WriteLine("── 设备 ──");
        Console.WriteLine($"  serial     : {device.Serial}");
        Console.WriteLine($"  connection : {device.ConnectionType}");
        Console.WriteLine($"  udid       : {lockdown.Udid}");
        Console.WriteLine($"  product    : {lockdown.ProductType}");
        Console.WriteLine($"  ios        : {lockdown.OsVersion}");
        try
        {
            var nameNode = lockdown.GetValue("DeviceName");
            Console.WriteLine($"  name       : {(nameNode is StringNode sn ? sn.Value : nameNode?.ToString())}");
        }
        catch { /* 名称拿不到不影响探针 */ }
        Console.WriteLine();
    }

    private static async Task<ContainerSession> OpenContainerAsync(Cli cli, ILogger logger)
    {
        var (lockdown, _) = OpenLockdown(cli, logger);
        var bundleId = cli.Bundle ?? await ResolveBundleQuietAsync(cli, lockdown, logger);
        var house = await HouseArrestService.Create(lockdown, bundleId, documentsOnly: true, logger: logger,
            cancellationToken: CancellationToken.None);
        return new ContainerSession(lockdown, house, bundleId);
    }

    private static async Task<string> ResolveBundleQuietAsync(Cli cli, LockdownClient lockdown, ILogger logger)
    {
        if (cli.Bundle is not null) return cli.Bundle;
        var apps = await BrowseAppsAsync(lockdown, logger);
        var installed = apps.Select(a => a.Text("CFBundleIdentifier"))
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in GameBundleCandidates)
            if (installed.Contains(candidate)) return candidate;
        throw new InvalidOperationException("没找到游戏 app，用 --bundle <id> 指定。");
    }

    private static async Task PrintStatAsync(HouseArrestService house, string path)
    {
        var info = await house.GetFileInfo(path, CancellationToken.None);
        Console.WriteLine($"[ok] {path}");
        foreach (var (key, value) in info)
        {
            var text = value switch
            {
                IntegerNode i => i.Value.ToString(),
                StringNode s => s.Value,
                _ => value.ToString(),
            };
            Console.WriteLine($"  {key,-12} {text}");
        }
    }

    private static string Require(Cli cli, int index, string usage)
    {
        if (cli.Positional.Count <= index)
            throw new InvalidOperationException($"参数不足。用法：{usage}");
        return cli.Positional[index];
    }

    private static void Record(string name, bool ok, string detail) => Checks.Add((name, ok, detail));

    private static string Normalize(string serial) => serial.Replace("-", "").Trim().ToUpperInvariant();

    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data))[..16].ToLowerInvariant() + "…";

    private sealed record ContainerSession(LockdownClient Lockdown, HouseArrestService House, string BundleId)
        : IDisposable
    {
        public void Dispose()
        {
            House.Dispose();
            Lockdown.Dispose();
        }
    }

    /// <summary>裸 house_arrest 通道：只用一个 <see cref="ServiceConnection"/>，不经过 AfcService 的包装。</summary>
    private sealed record RawSession(LockdownClient Lockdown, ServiceConnection Connection, string BundleId)
        : IDisposable
    {
        public void Dispose()
        {
            Connection.Dispose();
            Lockdown.Dispose();
        }
    }

    // ────────────────────────────────── 参数与日志

    private sealed class Cli
    {
        public string? Command { get; private set; }
        public List<string> Positional { get; } = [];
        public string? Udid { get; private set; }
        public string? Bundle { get; private set; }
        public bool Keep { get; private set; }
        public bool Verbose { get; private set; }
        public int Size { get; private set; } = 1024 * 1024;
        public ulong MakeDirOp { get; private set; } = (ulong)AfcOp.MakeDir;
        public ulong RenameOp { get; private set; } = (ulong)AfcOp.RenamePath;

        public static Cli Parse(string[] args)
        {
            var cli = new Cli();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--udid": cli.Udid = Next(args, ref i, arg); break;
                    case "--bundle": cli.Bundle = Next(args, ref i, arg); break;
                    case "--size": cli.Size = int.Parse(Next(args, ref i, arg)); break;
                    case "--makedir-op": cli.MakeDirOp = ParseOp(Next(args, ref i, arg)); break;
                    case "--rename-op": cli.RenameOp = ParseOp(Next(args, ref i, arg)); break;
                    case "--keep": cli.Keep = true; break;
                    case "--verbose": cli.Verbose = true; break;
                    default:
                        if (arg.StartsWith('-')) throw new InvalidOperationException($"未知选项 {arg}");
                        if (cli.Command is null) cli.Command = arg;
                        else cli.Positional.Add(arg);
                        break;
                }
            }
            if (cli.Command is null && args.Length == 0) cli = new Cli { Command = "selftest" };
            return cli;
        }

        private static string Next(string[] args, ref int i, string name)
        {
            if (i + 1 >= args.Length) throw new InvalidOperationException($"{name} 缺少值");
            return args[++i];
        }

        private static ulong ParseOp(string text) =>
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt64(text[2..], 16)
                : ulong.Parse(text);
    }

    private sealed class ConsoleLogger(LogLevel level) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= level && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Console.WriteLine($"    [{logLevel,-11}] {formatter(state, exception)}");
            if (exception is not null) Console.WriteLine("        " + exception.Message);
        }
    }
}
