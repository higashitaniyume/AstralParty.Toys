using System.Windows;
using System.Windows.Threading;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ToysLog.Error("crash", "未处理的异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ToysLog.Error("crash", "未观察的任务异常", args.Exception);
            args.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 必须早于 base.OnStartup：StartupUri 会在这里把主窗口建出来，管理器都在构造函数里定位数据目录。
        InitializeDataLayout();
        base.OnStartup(e);
    }

    private static void InitializeDataLayout()
    {
        try
        {
            var root = ToysPaths.Root;

            // 先用默认级别把日志打开：接下来迁移旧数据的过程也要留得下来。
            ToysLog.Configure(ToysPaths.LogsDirectory, ToysLogLevel.Info);
            ToysLog.Info("app",
                $"启动 AstralParty.Toys {AppInfo.Get().Version}；数据文件夹：{root}");
            // 环境信息单独一条：出问题时不用再去问用户「系统版本 / .NET / 是不是管理员 / WebView2 版本」
            ToysLog.Info("env", ToysEnvironment.Describe());

            var store = ToysConfigStore.ForRoot(root);
            var report = ToysMigration.Run(root, store);
            ToysPaths.EnsureDirectories();

            if (report.Problems.Count > 0)
                ToysLog.Warn("migration", "部分旧数据没能迁移：" + string.Join("；", report.Problems));

            var config = store.Load();
            ToysLog.Apply(config.Logging, ToysPaths.LogsDirectory);
        }
        catch (Exception ex)
        {
            // 初始化失败也不能挡住界面：各服务会自己回退默认值
            ToysLog.Error("app", "启动初始化失败", ex);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ToysLog.Error("crash", "未处理的界面异常", e.Exception);
        // 交给 WPF 默认处理：弹窗退出，避免用户以为只是卡了一下
    }
}
