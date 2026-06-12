namespace KbdLayoutTool.Core.Models;

/// <summary>
/// A keyboard layout to apply per-device via the HID override values
/// (KeyboardTypeOverride / KeyboardSubtypeOverride in the device's "Device Parameters" key).
/// Type/Subtype == null means "no override" (clear the values, fall back to the system layout).
/// </summary>
public sealed record KeyboardLayoutPreset(string Id, string DisplayName, int? Type, int? Subtype)
{
    /// <summary>Japanese 106/109 (JIS): Shift+2 = ", etc.</summary>
    public static readonly KeyboardLayoutPreset JapaneseJis = new("jis", "日本語 JIS (106/109)", 7, 2);

    /// <summary>US 101/104 (ANSI): Shift+2 = @, etc.</summary>
    public static readonly KeyboardLayoutPreset UsEnglish = new("us", "英語 US (101/104)", 4, 0);

    /// <summary>Remove the override; the device follows the system-wide layout.</summary>
    public static readonly KeyboardLayoutPreset Clear = new("clear", "override 解除（システム既定）", null, null);

    public bool IsClear => Type is null && Subtype is null;

    public static IReadOnlyList<KeyboardLayoutPreset> BuiltIn { get; } =
        new[] { JapaneseJis, UsEnglish, Clear };

    /// <summary>Best-effort label for an arbitrary Type/Subtype pair read from the registry.</summary>
    public static string Describe(int? type, int? subtype)
    {
        if (type is null && subtype is null) return "（override なし）";
        if (type == 7 && subtype == 2) return JapaneseJis.DisplayName;
        if (type == 4 && subtype == 0) return UsEnglish.DisplayName;
        return $"カスタム (Type={type?.ToString() ?? "-"}, Subtype={subtype?.ToString() ?? "-"})";
    }
}
