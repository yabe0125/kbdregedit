using KbdLayoutTool.Core.Models;
using Microsoft.Win32;

namespace KbdLayoutTool.Core.Services;

/// <summary>
/// Applies / clears the per-device keyboard layout override.
/// IMPORTANT: the per-device value names are KeyboardTypeOverride / KeyboardSubtypeOverride
/// (Override at the END). The system-wide names OverrideKeyboardType / OverrideKeyboardSubtype
/// are silently ignored inside a device's "Device Parameters" key.
/// Writing under HKLM requires administrator privileges.
/// </summary>
public sealed class OverrideService
{
    public const string TypeValueName = "KeyboardTypeOverride";
    public const string SubtypeValueName = "KeyboardSubtypeOverride";

    // Legacy/wrong value names we proactively clean up if present.
    private static readonly string[] StaleValueNames =
    {
        "OverrideKeyboardType", "OverrideKeyboardSubtype", "OverrideKeyboardIdentifier",
    };

    /// <summary>
    /// Applies the preset to the device's Device Parameters key and updates the model in place.
    /// Throws UnauthorizedAccessException / SecurityException when not elevated.
    /// </summary>
    public void Apply(KeyboardDevice device, KeyboardLayoutPreset preset)
    {
        using var dp = OpenDeviceParametersWritable(device);

        // Always remove stale system-wide-named values to avoid confusion.
        foreach (var name in StaleValueNames)
            dp.DeleteValue(name, throwOnMissingValue: false);

        if (preset.IsClear)
        {
            dp.DeleteValue(TypeValueName, throwOnMissingValue: false);
            dp.DeleteValue(SubtypeValueName, throwOnMissingValue: false);
            device.KeyboardTypeOverride = null;
            device.KeyboardSubtypeOverride = null;
        }
        else
        {
            dp.SetValue(TypeValueName, preset.Type!.Value, RegistryValueKind.DWord);
            dp.SetValue(SubtypeValueName, preset.Subtype!.Value, RegistryValueKind.DWord);
            device.KeyboardTypeOverride = preset.Type;
            device.KeyboardSubtypeOverride = preset.Subtype;
        }
    }

    private static RegistryKey OpenDeviceParametersWritable(KeyboardDevice device)
    {
        var key = Registry.LocalMachine.OpenSubKey(device.DeviceParametersSubKey, writable: true);
        if (key is not null) return key;

        // Create it if the subkey does not exist yet (rare; usually present).
        var parentPath = device.DeviceParametersSubKey[..device.DeviceParametersSubKey.LastIndexOf('\\')];
        using var parent = Registry.LocalMachine.OpenSubKey(parentPath, writable: true)
            ?? throw new InvalidOperationException(
                $"デバイスキーが見つかりません: {parentPath}");
        return parent.CreateSubKey("Device Parameters", writable: true);
    }
}
