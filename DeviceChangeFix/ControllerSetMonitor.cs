using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DeviceChangeFix
{
    internal enum RefreshResult
    {
        Unchanged,
        Changed,
        Failed,
    }

    internal sealed class ControllerSetMonitor
    {
        private const uint RIM_TYPEHID = 2;

        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RIDI_DEVICEINFO = 0x2000000B;

        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_JOYSTICK = 0x04;
        private const ushort HID_USAGE_GENERIC_GAMEPAD = 0x05;
        private const ushort HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER = 0x08;

        private static readonly uint DeviceListEntrySize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();

        // Device interface paths of the controllers seen by the last refresh.
        // Case-insensitive because Windows reports interface paths with
        // inconsistent casing between APIs.
        private HashSet<string> controllers = new(StringComparer.OrdinalIgnoreCase);

        public ControllerSetMonitor() => Refresh(out _, out _);

        /// <summary>
        /// Re-enumerates Raw Input devices and reports whether the set of
        /// connected controllers changed since the previous refresh. On
        /// failure the previous snapshot is kept, so a later refresh still
        /// picks up the difference.
        /// </summary>
        public RefreshResult Refresh(out List<string> added, out List<string> removed)
        {
            added = new List<string>();
            removed = new List<string>();

            if (!TryEnumerateControllers(out HashSet<string> current))
            {
                return RefreshResult.Failed;
            }

            foreach (string device in current)
            {
                if (!controllers.Contains(device))
                {
                    added.Add(device);
                }
            }
            foreach (string device in controllers)
            {
                if (!current.Contains(device))
                {
                    removed.Add(device);
                }
            }

            controllers = current;
            return added.Count > 0 || removed.Count > 0
                ? RefreshResult.Changed
                : RefreshResult.Unchanged;
        }

        private bool TryEnumerateControllers(out HashSet<string> snapshot)
        {
            const int MaxAttempts = 3;
            snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // A device can arrive between the sizing call and the fetch,
            // failing the fetch with ERROR_INSUFFICIENT_BUFFER; retry with the
            // new size before deferring to the next device-change broadcast.
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                uint count = 0;
                if (GetRawInputDeviceList(null, ref count, DeviceListEntrySize) != 0)
                {
                    return false;
                }
                if (count == 0)
                {
                    return true;
                }

                var devices = new RAWINPUTDEVICELIST[count];
                uint returned = GetRawInputDeviceList(devices, ref count, DeviceListEntrySize);
                if (returned == unchecked((uint)-1))
                {
                    continue;
                }

                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool incomplete = false;
                for (uint i = 0; i < returned; i++)
                {
                    if (devices[i].dwType != RIM_TYPEHID)
                    {
                        continue;
                    }

                    if (!TryClassifyController(devices[i].hDevice, out bool isController))
                    {
                        // Cannot tell whether this HID was a controller.
                        incomplete = true;
                        continue;
                    }
                    if (!isController)
                    {
                        continue;
                    }

                    if (!TryGetDeviceName(devices[i].hDevice, out string name))
                    {
                        // An identified controller with an unreadable path
                        // cannot be diffed against the snapshot.
                        incomplete = true;
                        continue;
                    }
                    result.Add(name);
                }

                // Per-device query failures usually mean a device went away
                // mid-enumeration, so retry for a consistent pass. If they
                // persist (e.g. a permanently misbehaving virtual HID), commit
                // the partial snapshot on the last attempt: a fabricated
                // add/remove only costs a spurious poll, whereas failing every
                // refresh would leave the plugin unable to ever forward again.
                if (!incomplete || attempt == MaxAttempts - 1)
                {
                    snapshot = result;
                    return true;
                }
            }
            return false;
        }

        private static unsafe bool TryGetDeviceName(nint device, out string name)
        {
            name = string.Empty;

            uint chars = 0;
            if (GetRawInputDeviceInfoW(device, RIDI_DEVICENAME, nint.Zero, ref chars) != 0 || chars == 0)
            {
                return false;
            }

            char[] buffer = new char[chars];
            fixed (char* p = buffer)
            {
                uint written = GetRawInputDeviceInfoW(device, RIDI_DEVICENAME, (nint)p, ref chars);
                if (written == unchecked((uint)-1) || written == 0)
                {
                    return false;
                }
                name = new string(p, 0, (int)written).TrimEnd('\0');
                return true;
            }
        }

        private static unsafe bool TryClassifyController(nint device, out bool isController)
        {
            isController = false;

            RID_DEVICE_INFO info = default;
            info.cbSize = (uint)sizeof(RID_DEVICE_INFO);
            uint size = info.cbSize;
            uint written = GetRawInputDeviceInfoW(device, RIDI_DEVICEINFO, (nint)(&info), ref size);
            if (written == unchecked((uint)-1) || written == 0)
            {
                return false;
            }

            isController = info.hid.usUsagePage == HID_USAGE_PAGE_GENERIC
                && info.hid.usUsage is HID_USAGE_GENERIC_JOYSTICK
                                    or HID_USAGE_GENERIC_GAMEPAD
                                    or HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER;
            return true;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetRawInputDeviceInfoW(nint hDevice, uint uiCommand, nint pData, ref uint pcbSize);
    }
}
