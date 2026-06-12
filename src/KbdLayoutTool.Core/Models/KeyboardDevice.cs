namespace KbdLayoutTool.Core.Models;

public enum BusKind
{
    Usb,
    Bluetooth,
    Other,
}

/// <summary>
/// A single keyboard HID collection found under HKLM\SYSTEM\CurrentControlSet\Enum\HID.
/// One physical keyboard can expose several collections (e.g. Keychron J9 over BT = Col01 + Col04);
/// each is represented as its own KeyboardDevice so the override can be applied to all of them.
/// </summary>
public sealed class KeyboardDevice
{
    /// <summary>Full PnP instance id, e.g. "HID\VID_3434&PID_0414&MI_00\7&25a1dfc5&0&0000".</summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Registry path of the "Device Parameters" subkey, relative to HKEY_LOCAL_MACHINE,
    /// e.g. "SYSTEM\CurrentControlSet\Enum\HID\...\Device Parameters".
    /// </summary>
    public required string DeviceParametersSubKey { get; init; }

    /// <summary>Human-friendly product name resolved from the parent bus device (e.g. "Keychron J9").</summary>
    public required string ProductName { get; init; }

    /// <summary>Raw HID device description (e.g. "HID Keyboard Device").</summary>
    public required string DeviceDescription { get; init; }

    public required BusKind Bus { get; init; }

    public string? MacAddress { get; init; }
    public string? VendorId { get; init; }
    public string? ProductId { get; init; }

    /// <summary>True if the devnode is currently present/connected (not just paired/phantom).</summary>
    public bool IsConnected { get; init; }

    public int? KeyboardTypeOverride { get; set; }
    public int? KeyboardSubtypeOverride { get; set; }

    /// <summary>Short tail of the instance id, handy to distinguish multiple collections of one device.</summary>
    public string CollectionTag
    {
        get
        {
            var parts = InstanceId.Split('\\');
            var devKey = parts.Length >= 2 ? parts[^2] : InstanceId;
            var col = devKey.Contains("&Col", StringComparison.OrdinalIgnoreCase)
                ? "Col" + devKey[(devKey.LastIndexOf("&Col", StringComparison.OrdinalIgnoreCase) + 4)..]
                : devKey.Contains("&MI_", StringComparison.OrdinalIgnoreCase)
                    ? devKey[devKey.LastIndexOf("&MI_", StringComparison.OrdinalIgnoreCase)..].TrimStart('&')
                    : "";
            return col;
        }
    }

    public string BusLabel => Bus switch
    {
        BusKind.Usb => "USB",
        BusKind.Bluetooth => "Bluetooth",
        _ => "その他",
    };

    public string CurrentLayoutLabel =>
        Models.KeyboardLayoutPreset.Describe(KeyboardTypeOverride, KeyboardSubtypeOverride);

    public string ConnectedLabel => IsConnected ? "● 接続中" : "○ 未接続";

    public string Identifier => Bus == BusKind.Bluetooth
        ? $"MAC {MacAddress}"
        : VendorId is not null ? $"VID_{VendorId} PID_{ProductId}" : InstanceId;
}
