using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class MainWindow : Window
{
    private ReplayAnalyzer? _analyzer;
    private ReplayReport? _report;
    private ICollectionView? _eventsView;
    private ICollectionView? _relicsView;
    private ICollectionView? _framesView;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1])) await LoadReplayAsync(args[1]);
    }

    private async void OpenReplay_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择吉星派对回放文件",
            Filter = "回放文件|*|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) await LoadReplayAsync(dialog.FileName);
    }

    private void ToolsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SpeedhackWindow { Owner = this };
        window.Show();
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        await LoadReplayAsync(files[0]);
    }

    private async Task LoadReplayAsync(string path)
    {
        BusyOverlay.Visibility = Visibility.Visible;
        BusyText.Text = "正在准备协议和配置…";
        StatusText.Text = "正在解析";
        try
        {
            var progress = new Progress<string>(message =>
            {
                BusyText.Text = message;
                StatusText.Text = message;
            });

            _report = await Task.Run(() =>
            {
                _analyzer ??= new ReplayAnalyzer(AppContext.BaseDirectory, useEmbeddedAssets: false);
                return _analyzer.Analyze(path, progress);
            });

            DataContext = _report;
            FileNameText.Text = $"{_report.FileName} · {_report.MapName}";
            FilePathText.Text = _report.FilePath;
            WelcomePanel.Visibility = Visibility.Collapsed;
            ContentTabs.Visibility = Visibility.Visible;
            ExportButton.IsEnabled = true;

            _eventsView = CollectionViewSource.GetDefaultView(_report.Events);
            _relicsView = CollectionViewSource.GetDefaultView(_report.Relics);
            _framesView = CollectionViewSource.GetDefaultView(_report.Frames);
            EventsGrid.ItemsSource = _eventsView;
            RelicsGrid.ItemsSource = _relicsView;
            FramesGrid.ItemsSource = _framesView;
            RawDetailText.Text = "选择一条协议帧查看内容。";
            StatusText.Text = $"解析完成 · {_report.FrameCount:N0} 帧 · {_report.Events.Count:N0} 个时间线事件";
        }
        catch (Exception ex)
        {
            StatusText.Text = "解析失败";
            MessageBox.Show(this, ex.Message, "无法读取回放", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BusyOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void EventFilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_eventsView is null) return;
        var query = EventFilterBox.Text.Trim();
        _eventsView.Filter = item => item is TimelineEvent value && Matches(query,
            value.FrameIndex.ToString(), value.Round.ToString(), value.PlayerName, value.Type, value.Title, value.Description);
    }

    private void RelicFilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_relicsView is null) return;
        var query = RelicFilterBox.Text.Trim();
        _relicsView.Filter = item => item is RelicRecord value && Matches(query,
            value.FrameIndex.ToString(), value.Round.ToString(), value.PlayerName, value.HeroName, value.Kind,
            value.RelicId.ToString(), value.RelicName, value.Quality, value.OptionsText);
    }

    private void FrameFilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_framesView is null) return;
        var query = FrameFilterBox.Text.Trim();
        _framesView.Filter = item => item is ProtocolFrame value && Matches(query,
            value.Index.ToString(), value.OffsetText, value.CmdId.ToString(), value.MessageName, value.PayloadLength.ToString());
    }

    private async void FramesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_analyzer is null || FramesGrid.SelectedItem is not ProtocolFrame frame) return;
        RawDetailText.Text = "正在解码…";
        try
        {
            var detail = await Task.Run(() => _analyzer.FormatFrame(frame));
            if (FramesGrid.SelectedItem == frame)
            {
                RawDetailText.Text = $"帧 {frame.Index} · offset {frame.OffsetText}\nCMD {frame.CmdId} · {frame.MessageName}\n载荷 {frame.PayloadLength:N0} 字节\n\n{detail}";
            }
        }
        catch (Exception ex)
        {
            RawDetailText.Text = $"解码失败：{ex.Message}\n\n原始载荷 Base64：\n{Convert.ToBase64String(frame.Payload)}";
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出回放数据",
            Filter = "JSON 文件|*.json",
            FileName = $"{Path.GetFileNameWithoutExtension(_report.FileName)}.report.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var export = new
            {
                schemaVersion = 1,
                source = new { _report.FileName, _report.FileSizeText, _report.Sha256 },
                match = new
                {
                    _report.ReplayId, _report.GameVersion, _report.RoomId, _report.RoomServerId,
                    _report.MapId, _report.MapName, _report.MapType, _report.Difficulty,
                    _report.StartTimeText, _report.FinishTimeText, _report.DurationText,
                    _report.ResultText, _report.WinnerText, _report.RoundCount, _report.TurnCount,
                    _report.PlayerDeaths, _report.GameProgress, _report.GameMaxProgress,
                    _report.BossName, _report.AwardsText
                },
                players = _report.Players,
                timeline = _report.Events,
                relics = _report.Relics,
                statistics = new
                {
                    commands = _report.CommandStats,
                    actions = _report.ActionStats,
                    relicQualities = _report.RelicQualityStats
                },
                frames = _report.Frames.Select(frame => new
                {
                    frame.Index, frame.Offset, frame.CmdId, frame.MessageName, frame.PayloadLength,
                    payloadBase64 = Convert.ToBase64String(frame.Payload)
                }),
                warnings = _report.Warnings
            };
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dialog.FileName, json);
            StatusText.Text = $"已导出：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool Matches(string query, params string[] values)
        => string.IsNullOrWhiteSpace(query) || values.Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase));
}
