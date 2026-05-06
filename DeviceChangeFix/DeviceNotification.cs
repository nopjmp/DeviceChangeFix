using System;
using System.Runtime.InteropServices;
using DeviceChangeFix;

namespace DeviceChangeFix
{
    internal static class DeviceNotification
    {
        private static IntPtr notificationHandle;

        /// <summary>
        /// Registers a window to receive notifications when USB devices are plugged or unplugged.
        /// </summary>
        /// <param name="windowHandle">Handle to the window receiving notifications.</param>
        public static void Register(IntPtr windowHandle, uint deviceType, Guid classGuid)
        {
            DEV_BROADCAST_DEVICEINTERFACE dbi = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_devicetype = deviceType,
                dbcc_reserved = 0,
                dbcc_classguid = classGuid,
                dbcc_size = (uint)Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
                dbcc_name = string.Empty
            };

            notificationHandle = RegisterDeviceNotification(windowHandle, dbi, 0);
        }

        /// <summary>
        /// Unregisters the window for USB device notifications
        /// </summary>
        public static void Unregister()
        {
            if (notificationHandle != IntPtr.Zero)
                UnregisterDeviceNotification(notificationHandle);
            notificationHandle = IntPtr.Zero;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, in DEV_BROADCAST_DEVICEINTERFACE notificationFilter, int flags);

        [DllImport("user32.dll")]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);
    }
}