using System.Globalization;
using System.Windows;
using AstralParty.ReplayTool.Services;
using Microsoft.Win32;

namespace AstralParty.ReplayTool;

/// <summary>经典 WPF 界面的变速器管理窗口（与 WebUI「游戏工具」页共用 SpeedhackManager）。</summary>
public partial class SpeedhackWindow : Window
{
    private readonly SpeedhackManager _manager;
    private readonly List<SpeedhackSpeedState> _states = [];
    private SpeedhackConfig _config = new();
    private bool _suppressSelection;

    public SpeedhackWindow()
    {
        InitializeComponent();
        _manager = new SpeedhackManager(AppContext.BaseDirectory);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => RefreshAll();

    // ============================== 状态刷新 ==============================

    private void RefreshAll()
    {
        RefreshStatusUi();
        RefreshConfigUi();
    }

    private void RefreshStatusUi()
    {
        var status = _manager.GetStatus();

        GameDirBox.Text = status.GameDirectory;
        ConfigPathText.Text = status.ProfileConfigPresent
            ? $"主配置：{status.ProfileConfigPath}"
            : $"主配置（将自动生成）：{status.ProfileConfigPath}";

        var installedOk = status.Installed && status.DllMatchesBundle;
        if (status.GameRunning)
        {
            SetBadge("游戏运行中", "#38BDF8", "#0C2A3F");
            StatusText.Text = "检测到游戏正在运行——安装 / 卸载前请先完全退出游戏。";
        }
        else if (installedOk)
        {
            SetBadge("已安装", "#A3E635", "#1C3310");
            StatusText.Text = $"变速器已就位：{status.DllPath}\n配置修改后重启游戏生效，或在游戏内按重载热键（默认 Ctrl+Shift+R）。";
        }
        else if (status.Installed)
        {
            SetBadge("文件不一致", "#FACC15", "#33271B");
            StatusText.Text = $"游戏目录里的 version.dll 与内置文件不同（{status.DllPath}），可能是其它工具的——覆盖或卸载前请先确认来源。";
        }
        else if (string.IsNullOrEmpty(status.GameDirectory))
        {
            SetBadge("未安装", "#E4B86C", "#33271B");
            StatusText.Text = status.Message + "\n可以点「自动检测」从 Steam 定位游戏目录，或「选择…」手动指定（游戏 exe 所在文件夹）。";
        }
        else
        {
            SetBadge("未安装", "#E4B86C", "#33271B");
            StatusText.Text = status.Message;
        }
        if (!status.GameRunning)
        {
            StatusText.Text += status.GameExeFound
                ? "\n已检测到目录里的 AstralParty 游戏程序，可以安装。"
                : "\n未在该目录找到 AstralParty 游戏程序（仍可强行安装，但请确认目录正确）。";
        }

        var ready = !status.GameRunning && status.BundleDllPresent;
        InstallButton.IsEnabled = ready && !string.IsNullOrWhiteSpace(GameDirBox.Text);
        UninstallButton.IsEnabled = ready && status.Installed;
        PushConfigButton.IsEnabled = ready && status.Installed;
    }

    private void SetBadge(string text, string foreground, string background)
    {
        StateBadgeText.Text = text;
        StateBadgeText.Foreground = new System.Windows.Media.SolidColorBrush(ColorFromHex(foreground));
        StateBadge.Background = new System.Windows.Media.SolidColorBrush(ColorFromHex(background));
    }

    private static System.Windows.Media.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return System.Windows.Media.Color.FromRgb(
            byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
            byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
            byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
    }

    // ============================== 配置界面 ==============================

    private void RefreshConfigUi()
    {
        try
        {
            _config = _manager.LoadProfileConfig();
        }
        catch (Exception ex)
        {
            _config = new SpeedhackConfig();
            MessageBox.Show(this, ex.Message, "配置读取失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _states.Clear();
        foreach (var state in _config.SpeedStates)
            _states.Add(new SpeedhackSpeedState { Keys = [.. state.Keys], Speed = state.Speed, IsToggle = state.IsToggle });

        AutoSpeedEnableCheck.IsChecked = _config.BaseSpeed != 1.0;
        BaseSpeedBox.Text = FormatSpeed(_config.BaseSpeed == 1.0 ? 2.0 : _config.BaseSpeed);
        SyncAutoSpeedEnabled();
        ConsoleCheck.IsChecked = _config.Console;
        WaitMsBox.Text = Math.Round(_config.WaitWithHook.Secs * 1000d + _config.WaitWithHook.Nanos / 1_000_000d).ToString(CultureInfo.InvariantCulture);

        var startup = _config.StartupState;
        StartupEnableCheck.IsChecked = startup is not null;
        StartupSpeedBox.Text = FormatSpeed(startup?.Speed ?? 10);
        StartupDurationBox.Text = (startup?.Duration.Secs ?? 5).ToString(CultureInfo.InvariantCulture);
        SyncStartupEnabled();

        ReloadKeysBox.Text = string.Join(",", _config.ReloadConfigKeys);
        SaveHintText.Text = "配置已加载（未保存的修改不会生效）";
        RebindStates();
    }

    private static string FormatSpeed(double speed) => speed.ToString("0.#", CultureInfo.InvariantCulture);

    private void SyncAutoSpeedEnabled()
    {
        BaseSpeedBox.IsEnabled = AutoSpeedEnableCheck.IsChecked == true;
    }

    private void SyncStartupEnabled()
    {
        var enabled = StartupEnableCheck.IsChecked == true;
        StartupSpeedBox.IsEnabled = enabled;
        StartupDurationBox.IsEnabled = enabled;
    }

    private void RebindStates()
    {
        _suppressSelection = true;
        var index = StatesList.SelectedIndex;
        StatesList.ItemsSource = null;
        StatesList.ItemsSource = _states.Select((state, i) =>
            $"档位 {i + 1}: {string.Join(" + ", state.Keys)}   ×{FormatSpeed(state.Speed)}   {(state.IsToggle ? "点按切换" : "按住生效")}").ToList();
        if (index >= 0 && index < _states.Count) StatesList.SelectedIndex = index;
        _suppressSelection = false;
    }

    private void LoadSelectedStateIntoBoxes()
    {
        if (StatesList.SelectedIndex < 0 || StatesList.SelectedIndex >= _states.Count) return;
        var state = _states[StatesList.SelectedIndex];
        StateKeysBox.Text = string.Join(",", state.Keys);
        StateSpeedBox.Text = FormatSpeed(state.Speed);
        StateToggleCheck.IsChecked = state.IsToggle;
        ApplyStateButton.IsEnabled = true;
    }

    private void MarkDirty() => SaveHintText.Text = "⚠ 配置已修改，点「保存配置」写入文件";

    // ============================== 目录与安装 ==============================

    private string? DirectoryFromBox()
    {
        var value = GameDirBox.Text.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('\\', '/');
    }

    private void DetectDirButton_Click(object sender, RoutedEventArgs e)
    {
        var directory = SpeedhackManager.DetectGameDirectory();
        if (directory is null)
        {
            MessageBox.Show(this, "自动检测未找到游戏目录。\n可手动选择 Steam 库里的 Astral Party 安装目录（游戏 exe 所在文件夹）。",
                "未找到", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _manager.SaveStoredGameDirectory(directory);
        RefreshStatusUi();
        StatusText.Text = $"已自动定位游戏目录：{directory}";
    }

    private void BrowseDirButton_Click(object sender, RoutedEventArgs e)
    {
        var current = _manager.ResolveGameDirectory();
        var dialog = new OpenFolderDialog
        {
            Title = "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
            InitialDirectory = current is { Length: > 0 } && Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(this) != true || dialog.FolderName is not { Length: > 0 }) return;
        _manager.SaveStoredGameDirectory(dialog.FolderName);
        RefreshStatusUi();
    }

    private void OpenDirButton_Click(object sender, RoutedEventArgs e)
    {
        var directory = DirectoryFromBox();
        if (directory is null || !Directory.Exists(directory))
        {
            MessageBox.Show(this, "请先选择有效的游戏目录。", "无法打开", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SpeedhackManager.OpenInExplorer(directory);
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var directory = DirectoryFromBox();
        try
        {
            var config = BuildConfigFromUi();
            _manager.SaveProfileConfig(config);
            _manager.Install(directory ?? "", OverwriteCheck.IsChecked == true);
            _manager.SaveStoredGameDirectory(directory);
            RefreshAll();
            var autoText = config.BaseSpeed == 1.0
                ? "自动倍速已关闭，可使用配置的快捷键切换。"
                : $"每次进入游戏会自动使用 {FormatSpeed(config.BaseSpeed)} 倍速，无需按快捷键。";
            MessageBox.Show(this, $"变速器与当前配置已安装到游戏目录。\n\n{autoText}\n需在游戏设置里关闭垂直同步。",
                "安装完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "安装失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshStatusUi();
        }
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _manager.Uninstall(DirectoryFromBox() ?? "", ForceUninstallCheck.IsChecked == true);
            RefreshAll();
            MessageBox.Show(this, result.Message, "卸载", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "卸载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshStatusUi();
        }
    }

    private void PushConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var directory = DirectoryFromBox();
        try
        {
            if (directory is null || !File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
                throw new InvalidOperationException("游戏目录尚未安装变速器，请先安装。");
            _manager.PushConfigToGame(directory);
            MessageBox.Show(this, "配置已同步到游戏目录，重启游戏后生效。", "已同步", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "同步失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ============================== 配置操作 ==============================

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = BuildConfigFromUi();
            _manager.SaveProfileConfig(config);
            var directory = DirectoryFromBox();
            if (directory is not null && File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
                _manager.PushConfigToGame(directory);

            _config = config;
            var autoText = config.BaseSpeed == 1.0 ? "自动倍速已关闭" : $"进入游戏自动 ×{FormatSpeed(config.BaseSpeed)}";
            SaveHintText.Text = directory is not null && File.Exists(Path.Combine(directory, SpeedhackManager.DllName))
                ? $"✔ 已保存并同步 · {autoText}（{DateTime.Now:HH:mm:ss}）"
                : $"✔ 已保存到主配置 · {autoText}（{DateTime.Now:HH:mm:ss}）";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private SpeedhackConfig BuildConfigFromUi()
    {
        var baseSpeed = 1.0;
        if (AutoSpeedEnableCheck.IsChecked == true &&
            (!double.TryParse(BaseSpeedBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out baseSpeed) || baseSpeed <= 0))
            throw new FormatException("自动倍速必须是大于 0 的数字。");
        if (!double.TryParse(WaitMsBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var waitMs) || waitMs < 0)
            throw new FormatException("挂接延迟必须是 ≥ 0 的数字（毫秒）。");

        SpeedhackStartupState? startup = null;
        if (StartupEnableCheck.IsChecked == true)
        {
            if (!double.TryParse(StartupSpeedBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var startupSpeed) || startupSpeed <= 0)
                throw new FormatException("启动加速倍速必须是大于 0 的数字。");
            if (!int.TryParse(StartupDurationBox.Text, out var durationSecs) || durationSecs < 1)
                throw new FormatException("启动加速时长必须是 ≥ 1 的整数秒。");
            startup = new SpeedhackStartupState
            {
                Speed = startupSpeed,
                Duration = new SpeedhackDuration { Secs = durationSecs, Nanos = 0 }
            };
        }

        var reloadKeys = ParseKeyList(ReloadKeysBox.Text, "重载配置热键");
        var config = new SpeedhackConfig
        {
            Console = ConsoleCheck.IsChecked == true,
            BaseSpeed = baseSpeed,
            WaitWithHook = new SpeedhackDuration
            {
                Secs = (int)(waitMs / 1000),
                Nanos = (int)(waitMs % 1000 * 1_000_000)
            },
            StartupState = startup,
            ReloadConfigKeys = reloadKeys,
            SpeedStates = _states.Select(state => new SpeedhackSpeedState
            {
                Keys = [.. state.Keys],
                Speed = state.Speed,
                IsToggle = state.IsToggle
            }).ToList()
        };
        return config;
    }

    private static List<string> ParseKeyList(string text, string label)
    {
        var keys = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(key => key.Length > 0)
            .ToList();
        if (keys.Count == 0) throw new FormatException($"{label}不能为空，需填入 VK 键名（如 VK_CONTROL）。");
        if (keys.Any(key => !key.StartsWith("VK_", StringComparison.OrdinalIgnoreCase)))
            throw new FormatException($"{label}里的键名需要以 VK_ 开头（如 VK_CONTROL, VK_SHIFT）。");
        return keys;
    }

    private void RestoreConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _manager.ResetProfileToTemplate();
            var directory = DirectoryFromBox();
            if (directory is not null && File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
                _manager.PushConfigToGame(directory);
            RefreshConfigUi();
            MessageBox.Show(this, "已恢复默认模板（Ctrl 切换 2 倍速）。", "已恢复", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "恢复失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EditJsonButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _manager.EnsureProfileConfig();
            SpeedhackManager.OpenWithDefaultEditor(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ============================== 档位编辑 ==============================

    private void StatesList_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSelection) return;
        LoadSelectedStateIntoBoxes();
    }

    private void AddStateButton_Click(object sender, RoutedEventArgs e)
    {
        _states.Add(new SpeedhackSpeedState { Keys = ["VK_CONTROL"], Speed = 2.0, IsToggle = true });
        RebindStates();
        StatesList.SelectedIndex = _states.Count - 1;
        LoadSelectedStateIntoBoxes();
        MarkDirty();
    }

    private void RemoveStateButton_Click(object sender, RoutedEventArgs e)
    {
        if (StatesList.SelectedIndex < 0 || StatesList.SelectedIndex >= _states.Count) return;
        var index = StatesList.SelectedIndex;
        _states.RemoveAt(index);
        RebindStates();
        if (_states.Count == 0) ApplyStateButton.IsEnabled = false;
        MarkDirty();
    }

    private void ApplyStateButton_Click(object sender, RoutedEventArgs e)
    {
        var index = StatesList.SelectedIndex;
        if (index < 0 || index >= _states.Count) return;
        try
        {
            if (!double.TryParse(StateSpeedBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) || speed <= 0)
                throw new FormatException("倍速必须是大于 0 的数字。");
            var state = _states[index];
            state.Keys = ParseKeyList(StateKeysBox.Text, $"档位 {index + 1}");
            state.Speed = speed;
            state.IsToggle = StateToggleCheck.IsChecked == true;
            RebindStates();
            StatesList.SelectedIndex = index;
            MarkDirty();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "档位无效", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AutoSpeedEnable_Changed(object sender, RoutedEventArgs e)
    {
        SyncAutoSpeedEnabled();
        if (IsLoaded) MarkDirty();
    }

    private void StartupEnable_Changed(object sender, RoutedEventArgs e) => SyncStartupEnabled();
}
