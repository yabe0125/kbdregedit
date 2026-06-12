using Microsoft.Win32;

namespace KbdLayoutTool.Core.Services;

/// <summary>
/// Resolves a friendly product name (e.g. "Keychron J9") for a HID keyboard collection,
/// because the HID node itself only reports "HID Keyboard Device".
/// The real name lives on the parent bus device, so we build lookup maps from the
/// Bluetooth (BTHLE) and USB enum trees, keyed by MAC and VID&amp;PID.
/// </summary>
public sealed class ProductNameResolver
{
    private const string EnumRoot = @"SYSTEM\CurrentControlSet\Enum";

    // MAC (lower-case, 12 hex) -> friendly name
    private readonly Dictionary<string, string> _byMac = new(StringComparer.OrdinalIgnoreCase);
    // "vid&pid" (lower-case) -> friendly name
    private readonly Dictionary<string, string> _byVidPid = new(StringComparer.OrdinalIgnoreCase);

    public static ProductNameResolver Build()
    {
        var r = new ProductNameResolver();
        r.IndexBluetooth("BTHLE");
        r.IndexBluetooth("BTHENUM");
        r.IndexUsb();
        return r;
    }

    public string Resolve(string? mac, string? vid, string? pid, string fallback)
    {
        if (!string.IsNullOrEmpty(mac) && _byMac.TryGetValue(mac, out var nameByMac) && IsMeaningfulName(nameByMac))
            return nameByMac;

        if (!string.IsNullOrEmpty(vid) && !string.IsNullOrEmpty(pid))
        {
            var key = $"{vid}&{pid}";
            if (_byVidPid.TryGetValue(key, out var nameByVidPid) && IsMeaningfulName(nameByVidPid))
                return nameByVidPid;
        }

        return CleanDescription(fallback);
    }

    private void IndexBluetooth(string subtree)
    {
        using var root = Registry.LocalMachine.OpenSubKey($@"{EnumRoot}\{subtree}");
        if (root is null) return;

        foreach (var devName in root.GetSubKeyNames())
        {
            // BTHLE\Dev_<MAC>  /  BTHENUM\Dev_<MAC>
            var mac = ExtractMac(devName);
            if (mac is null) continue;

            using var devKey = root.OpenSubKey(devName);
            var name = ReadBestNameFromInstances(devKey);
            if (name is not null && IsMeaningfulName(name))
                _byMac[mac] = name;
        }
    }

    private void IndexUsb()
    {
        using var root = Registry.LocalMachine.OpenSubKey($@"{EnumRoot}\USB");
        if (root is null) return;

        foreach (var devName in root.GetSubKeyNames())
        {
            // USB\VID_xxxx&PID_yyyy
            var (vid, pid) = ExtractVidPid(devName);
            if (vid is null || pid is null) continue;

            using var devKey = root.OpenSubKey(devName);
            var name = ReadBestNameFromInstances(devKey);
            if (name is not null && IsMeaningfulName(name))
                _byVidPid[$"{vid}&{pid}"] = name;
        }
    }

    private static string? ReadBestNameFromInstances(RegistryKey? devKey)
    {
        if (devKey is null) return null;
        foreach (var inst in devKey.GetSubKeyNames())
        {
            using var instKey = devKey.OpenSubKey(inst);
            if (instKey is null) continue;
            var friendly = instKey.GetValue("FriendlyName") as string;
            if (IsMeaningfulName(friendly)) return friendly!;
            var desc = CleanDescription(instKey.GetValue("DeviceDesc") as string ?? "");
            if (IsMeaningfulName(desc)) return desc;
        }
        return null;
    }

    public static bool IsMeaningfulName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        // Skip generic descriptions that carry no product identity.
        string[] generic =
        {
            "HID Keyboard Device", "USB Input Device", "USB Composite Device", "HID-compliant",
            "Bluetooth", "Bluetooth Low Energy", "Bluetooth LE Service",
            "Device Identification Service", "Generic",
        };
        return !generic.Any(g => name.Equals(g, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>"@keyboard.inf,%hid.keyboarddevice%;HID Keyboard Device" -> "HID Keyboard Device".</summary>
    public static string CleanDescription(string desc)
    {
        if (string.IsNullOrEmpty(desc)) return desc;
        var idx = desc.LastIndexOf(';');
        return idx >= 0 ? desc[(idx + 1)..].Trim() : desc.Trim();
    }

    /// <summary>
    /// Extracts the device MAC (12 hex) from a Bluetooth enum key name.
    /// Handles both the BTHLE node form "Dev_D87742B70C6C" and the HID form
    /// "{00001812-...}_Dev_VID&amp;023434_PID&amp;0414_REV&amp;0204_d87742b70c6c&amp;Col01".
    /// Critically, it must NOT match the 12-hex tail of the GATT base UUID
    /// "...8000-00805f9b34fb" embedded in the leading GUID.
    /// </summary>
    public static string? ExtractMac(string deviceKeyName)
    {
        // The MAC is a 12-hex run preceded by '_' and followed by '&' (e.g. &Col01) or end of string.
        // The base-UUID tail is preceded by '-' and followed by '}', so it is excluded.
        var m = System.Text.RegularExpressions.Regex.Match(
            deviceKeyName, @"_([0-9A-Fa-f]{12})(?=&|$)");
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
    }

    /// <summary>Extracts VID/PID from "VID_3434&amp;PID_0414" or "..._Dev_VID&amp;023434_PID&amp;0414...".</summary>
    public static (string? vid, string? pid) ExtractVidPid(string deviceKeyName)
    {
        var vid = System.Text.RegularExpressions.Regex.Match(
            deviceKeyName, @"VID[_&]0?([0-9A-Fa-f]{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var pid = System.Text.RegularExpressions.Regex.Match(
            deviceKeyName, @"PID[_&]([0-9A-Fa-f]{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return (
            vid.Success ? vid.Groups[1].Value.ToLowerInvariant() : null,
            pid.Success ? pid.Groups[1].Value.ToLowerInvariant() : null);
    }
}
