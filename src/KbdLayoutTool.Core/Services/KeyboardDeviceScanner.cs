using KbdLayoutTool.Core.Models;
using KbdLayoutTool.Core.Native;
using Microsoft.Win32;

namespace KbdLayoutTool.Core.Services;

/// <summary>
/// Enumerates keyboard HID collections from HKLM\SYSTEM\CurrentControlSet\Enum\HID
/// and reads their current per-device layout override values.
/// </summary>
public sealed class KeyboardDeviceScanner
{
    private const string HidEnumSubKey = @"SYSTEM\CurrentControlSet\Enum\HID";
    private const string DeviceParametersLeaf = "Device Parameters";

    // Setup class GUID for "Keyboard".
    private const string KeyboardClassGuid = "{4d36e96b-e325-11ce-bfc1-08002be10318}";
    private const string KeyboardHidService = "kbdhid";

    public IReadOnlyList<KeyboardDevice> Scan()
    {
        var resolver = ProductNameResolver.Build();
        var result = new List<KeyboardDevice>();

        using var hid = Registry.LocalMachine.OpenSubKey(HidEnumSubKey);
        if (hid is null) return result;

        foreach (var deviceKeyName in hid.GetSubKeyNames())
        {
            using var deviceKey = hid.OpenSubKey(deviceKeyName);
            if (deviceKey is null) continue;

            foreach (var instanceName in deviceKey.GetSubKeyNames())
            {
                using var instanceKey = deviceKey.OpenSubKey(instanceName);
                if (instanceKey is null) continue;

                if (!IsKeyboard(instanceKey)) continue;

                var device = BuildDevice(resolver, deviceKeyName, instanceName, instanceKey);
                result.Add(device);
            }
        }

        return result
            .OrderBy(d => d.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.BusLabel)
            .ThenBy(d => d.CollectionTag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsKeyboard(RegistryKey instanceKey)
    {
        var classGuid = instanceKey.GetValue("ClassGUID") as string;
        if (string.Equals(classGuid, KeyboardClassGuid, StringComparison.OrdinalIgnoreCase))
            return true;

        var service = instanceKey.GetValue("Service") as string;
        return string.Equals(service, KeyboardHidService, StringComparison.OrdinalIgnoreCase);
    }

    private static KeyboardDevice BuildDevice(
        ProductNameResolver resolver, string deviceKeyName, string instanceName, RegistryKey instanceKey)
    {
        var instanceId = $"HID\\{deviceKeyName}\\{instanceName}";
        var dpSubKey = $@"{HidEnumSubKey}\{deviceKeyName}\{instanceName}\{DeviceParametersLeaf}";

        var bus = DetectBus(deviceKeyName);
        var mac = ProductNameResolver.ExtractMac(deviceKeyName);
        var (vid, pid) = ProductNameResolver.ExtractVidPid(deviceKeyName);

        var rawDesc = instanceKey.GetValue("DeviceDesc") as string ?? "";
        var cleanDesc = ProductNameResolver.CleanDescription(rawDesc);

        var (type, subtype) = ReadOverride(dpSubKey);

        return new KeyboardDevice
        {
            InstanceId = instanceId,
            DeviceParametersSubKey = dpSubKey,
            ProductName = ResolveProductName(resolver, bus, instanceId, mac, vid, pid, rawDesc),
            DeviceDescription = cleanDesc,
            Bus = bus,
            MacAddress = mac,
            VendorId = vid,
            ProductId = pid,
            IsConnected = ConfigManager.IsDevicePresent(instanceId),
            KeyboardTypeOverride = type,
            KeyboardSubtypeOverride = subtype,
        };
    }

    /// <summary>
    /// Resolves the friendly product name. The bus-reported device description (USB iProduct,
    /// read via CfgMgr) is the most accurate — it turns the generic "USB Composite Device" into
    /// e.g. "Keychron J9". Falls back to the Bluetooth/USB name maps, then the raw HID description.
    /// </summary>
    private static string ResolveProductName(
        ProductNameResolver resolver, BusKind bus, string instanceId,
        string? mac, string? vid, string? pid, string rawDesc)
    {
        // For Bluetooth, the MAC-keyed name map already gives the real product name
        // (BusReportedDeviceDesc returns a generic GATT name like "Bluetooth LE Service"),
        // so only consult the bus-reported description for USB / other buses.
        if (bus != BusKind.Bluetooth)
        {
            var busReported = ConfigManager.TryGetBusReportedDeviceDesc(instanceId);
            if (ProductNameResolver.IsMeaningfulName(busReported))
                return busReported!;
        }

        return resolver.Resolve(mac, vid, pid, rawDesc);
    }

    private static BusKind DetectBus(string deviceKeyName)
    {
        if (deviceKeyName.StartsWith("VID_", StringComparison.OrdinalIgnoreCase))
            return BusKind.Usb;
        if (deviceKeyName.Contains("_Dev_VID&", StringComparison.OrdinalIgnoreCase)
            || deviceKeyName.StartsWith("{", StringComparison.Ordinal))
            return BusKind.Bluetooth;
        return BusKind.Other;
    }

    private static (int? type, int? subtype) ReadOverride(string dpSubKey)
    {
        using var dp = Registry.LocalMachine.OpenSubKey(dpSubKey);
        if (dp is null) return (null, null);
        var type = dp.GetValue(OverrideService.TypeValueName) as int?;
        var subtype = dp.GetValue(OverrideService.SubtypeValueName) as int?;
        return (type, subtype);
    }
}
