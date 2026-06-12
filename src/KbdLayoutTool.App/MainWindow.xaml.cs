using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KbdLayoutTool.Core.Models;
using KbdLayoutTool.Core.Services;
using Microsoft.Win32;

namespace KbdLayoutTool.App;

public partial class MainWindow : Window
{
    private readonly KeyboardDeviceScanner _scanner = new();
    private readonly OverrideService _overrideService = new();
    private readonly RegFileExporter _exporter = new();
    private readonly ObservableCollection<KeyboardDevice> _devices = new();

    public MainWindow()
    {
        InitializeComponent();
        DeviceGrid.ItemsSource = _devices;
        Loaded += (_, _) => Rescan();
    }

    private void Rescan()
    {
        var selectedIds = DeviceGrid.SelectedItems
            .OfType<KeyboardDevice>()
            .Select(d => d.InstanceId)
            .ToHashSet();

        _devices.Clear();
        try
        {
            foreach (var d in _scanner.Scan())
                _devices.Add(d);

            // Restore selection by instance id.
            foreach (var d in _devices.Where(d => selectedIds.Contains(d.InstanceId)))
                DeviceGrid.SelectedItems.Add(d);

            SetStatus($"{_devices.Count} 件のキーボードコレクションを検出しました。");
        }
        catch (Exception ex)
        {
            SetStatus($"スキャンに失敗しました: {ex.Message}", isError: true);
        }
    }

    private IReadOnlyList<KeyboardDevice> GetSelection()
        => DeviceGrid.SelectedItems.OfType<KeyboardDevice>().ToList();

    private void ApplyPreset(KeyboardLayoutPreset preset)
    {
        var targets = GetSelection();
        if (targets.Count == 0)
        {
            SetStatus("対象のキーボードを選択してください。", isError: true);
            return;
        }

        var ok = 0;
        foreach (var device in targets)
        {
            try
            {
                _overrideService.Apply(device, preset);
                ok++;
            }
            catch (UnauthorizedAccessException)
            {
                SetStatus("管理者権限が必要です。アプリを管理者として再起動してください。", isError: true);
                return;
            }
            catch (Exception ex)
            {
                SetStatus($"'{device.ProductName}' への適用に失敗: {ex.Message}", isError: true);
                return;
            }
        }

        Rescan();
        SetStatus($"{ok} 件に「{preset.DisplayName}」を適用しました。反映には再接続/再起動が必要です。");
    }

    private void OnRescan(object sender, RoutedEventArgs e) => Rescan();

    private void OnApplyJis(object sender, RoutedEventArgs e) => ApplyPreset(KeyboardLayoutPreset.JapaneseJis);

    private void OnApplyUs(object sender, RoutedEventArgs e) => ApplyPreset(KeyboardLayoutPreset.UsEnglish);

    private void OnClear(object sender, RoutedEventArgs e) => ApplyPreset(KeyboardLayoutPreset.Clear);

    private void OnExportReg(object sender, RoutedEventArgs e)
    {
        var targets = GetSelection();
        if (targets.Count == 0)
        {
            SetStatus("出力対象のキーボードを選択してください。", isError: true);
            return;
        }

        var choice = MessageBox.Show(
            "「はい」= 選択行を現在の各レイアウトで再適用する .reg を出力\n" +
            "「いいえ」= 選択行の override を解除する .reg を出力",
            "出力する .reg の種類", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (choice == MessageBoxResult.Cancel) return;

        var dialog = new SaveFileDialog
        {
            Title = ".reg の保存先",
            Filter = "Registry ファイル (*.reg)|*.reg",
            FileName = "keyboard-layout.reg",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            if (choice == MessageBoxResult.Yes)
            {
                // Group by the preset each device currently has, so mixed selections export correctly.
                WriteCurrentLayouts(dialog.FileName, targets);
            }
            else
            {
                _exporter.Write(dialog.FileName, targets, KeyboardLayoutPreset.Clear);
            }
            SetStatus($".reg を出力しました: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($".reg 出力に失敗: {ex.Message}", isError: true);
        }
    }

    private void WriteCurrentLayouts(string path, IReadOnlyList<KeyboardDevice> devices)
    {
        // Each device may currently hold a different override; emit a combined file.
        var sb = new System.Text.StringBuilder();
        sb.Append("Windows Registry Editor Version 5.00\r\n\r\n");
        foreach (var d in devices)
        {
            var preset = d.KeyboardTypeOverride is null && d.KeyboardSubtypeOverride is null
                ? KeyboardLayoutPreset.Clear
                : new KeyboardLayoutPreset("current", KeyboardLayoutPreset.Describe(d.KeyboardTypeOverride, d.KeyboardSubtypeOverride),
                    d.KeyboardTypeOverride, d.KeyboardSubtypeOverride);
            // Reuse exporter body per device for consistent formatting.
            sb.Append(_exporter.BuildContent(new[] { d }, preset)
                .Replace("Windows Registry Editor Version 5.00\r\n\r\n", ""));
        }
        var encoding = new System.Text.UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        File.WriteAllText(path, sb.ToString(), encoding);
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? System.Windows.Media.Brushes.Firebrick
            : System.Windows.Media.Brushes.Black;
    }
}
