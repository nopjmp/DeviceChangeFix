using System.Runtime.InteropServices;

namespace DeviceChangeFix
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RAWINPUTDEVICELIST
    {
        public nint hDevice;
        public uint dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RID_DEVICE_INFO_HID
    {
        public uint dwVendorId;
        public uint dwProductId;
        public uint dwVersionNumber;
        public ushort usUsagePage;
        public ushort usUsage;
    }

    // Union: the mouse/keyboard variants are not needed, but the keyboard variant is
    // the largest (24 bytes), so pad the total to the native 32-byte size.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct RID_DEVICE_INFO
    {
        [FieldOffset(0)] public uint cbSize;
        [FieldOffset(4)] public uint dwType;
        [FieldOffset(8)] public RID_DEVICE_INFO_HID hid;
    }
}
