using System.Runtime.InteropServices;

namespace KbdLayoutTool.Core.Native;

/// <summary>
/// Thin P/Invoke wrapper over CfgMgr32 for:
///  - telling whether a device instance is currently present (connected) vs paired/phantom, and
///  - reading the bus-reported device description (the USB iProduct string, e.g. "Keychron J9"),
///    which is the real product name that the generic "DeviceDesc" ("USB Composite Device") hides.
/// The properties registry subtree is ACL-protected, so we read it through the CM API instead.
/// </summary>
internal static class ConfigManager
{
    private const int CR_SUCCESS = 0x00000000;
    private const int CR_BUFFER_SMALL = 0x0000001A;

    // CM_LOCATE_DEVNODE flags
    private const uint CM_LOCATE_DEVNODE_NORMAL = 0x00000000;  // succeeds only for present devices
    private const uint CM_LOCATE_DEVNODE_PHANTOM = 0x00000001; // succeeds for phantom/paired devices too

    // DEVPROP_TYPE_STRING
    private const uint DEVPROP_TYPE_STRING = 0x00000012;

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // DEVPKEY_Device_BusReportedDeviceDesc = {540b947e-8b40-45bc-a8a2-6a0b894cbda2}, 4
    private static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new()
    {
        fmtid = new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"),
        pid = 4,
    };

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Locate_DevNodeW")]
    private static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, uint ulFlags);

    [DllImport("CfgMgr32.dll", EntryPoint = "CM_Get_Parent")]
    private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_DevNode_PropertyW")]
    private static extern int CM_Get_DevNode_Property(
        uint dnDevInst, ref DEVPROPKEY propertyKey, out uint propertyType,
        byte[]? propertyBuffer, ref uint propertyBufferSize, uint ulFlags);

    /// <summary>
    /// Returns true if the device instance is currently connected/present
    /// (false if only paired/registered/phantom or unknown).
    /// </summary>
    public static bool IsDevicePresent(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        return CM_Locate_DevNode(out _, instanceId, CM_LOCATE_DEVNODE_NORMAL) == CR_SUCCESS;
    }

    /// <summary>
    /// Reads the bus-reported device description (USB iProduct) for the instance, walking up to the
    /// parent device nodes if the leaf node does not carry it. Works for phantom devices too, because
    /// the property is read from the persisted property store. Returns null when unavailable.
    /// </summary>
    public static string? TryGetBusReportedDeviceDesc(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;

        // PHANTOM so this still resolves for a currently-disconnected USB keyboard.
        if (CM_Locate_DevNode(out var devInst, instanceId, CM_LOCATE_DEVNODE_PHANTOM) != CR_SUCCESS)
            return null;

        // The iProduct string lives on the USB function/composite node, which may be a parent
        // of the HID collection node. Walk up a few levels and take the first meaningful value.
        for (var level = 0; level < 4; level++)
        {
            var value = ReadStringProperty(devInst, DEVPKEY_Device_BusReportedDeviceDesc);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            if (CM_Get_Parent(out var parent, devInst, 0) != CR_SUCCESS)
                break;
            devInst = parent;
        }

        return null;
    }

    private static string? ReadStringProperty(uint devInst, DEVPROPKEY key)
    {
        uint size = 0;
        var rc = CM_Get_DevNode_Property(devInst, ref key, out var type, null, ref size, 0);
        if (rc != CR_BUFFER_SMALL || size == 0 || type != DEVPROP_TYPE_STRING)
            return null;

        var buffer = new byte[size];
        rc = CM_Get_DevNode_Property(devInst, ref key, out type, buffer, ref size, 0);
        if (rc != CR_SUCCESS)
            return null;

        return System.Text.Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0').Trim();
    }
}
