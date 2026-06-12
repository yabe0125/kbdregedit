using KbdLayoutTool.Core.Models;
using KbdLayoutTool.Core.Services;

// Read-only smoke test: enumerate keyboards exactly like the GUI will, and print them.
var scanner = new KeyboardDeviceScanner();
var devices = scanner.Scan();

Console.WriteLine($"検出したキーボードコレクション: {devices.Count} 件\n");
Console.WriteLine($"{"製品名",-20} {"接続",-10} {"状態",-8} {"Col",-6} 現在のレイアウト / 識別子");
Console.WriteLine(new string('-', 100));

foreach (var d in devices)
{
    Console.WriteLine(
        $"{Trim(d.ProductName, 20),-20} {d.BusLabel,-10} {(d.IsConnected ? "接続" : "未接続"),-8} " +
        $"{d.CollectionTag,-6} {d.CurrentLayoutLabel}   [{d.Identifier}]");
}

// Verify the .reg generation format without touching the registry.
Console.WriteLine("\n--- .reg 出力サンプル (JIS, 先頭の接続キーボード) ---");
var sample = devices.Where(d => d.IsConnected).Take(1).ToList();
if (sample.Count > 0)
    Console.WriteLine(new RegFileExporter().BuildContent(sample, KeyboardLayoutPreset.JapaneseJis));

static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
