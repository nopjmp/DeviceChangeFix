using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using System;
using System.Runtime.InteropServices;

namespace DeviceChangeFix
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "DeviceChangeFix";

        private const uint WM_DEVICECHANGE = 0x0219;
        private const uint DBT_DEVNODES_CHANGED = 0x0007;
        private const uint DBT_DEVICEARRIVAL = 0x8000;
        private const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private const uint DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;

        private delegate nint WndProcDelegate(nint hWnd, uint msg, nuint wParam, nint lParam);

        private static readonly Guid GUID_DEVINTERFACE_HID = new("4D1E55B2-F16F-11CF-88CB-001111000030");

        private IPluginLog pluginLog { get; init; }

        private bool deviceNotificationRegistered = false;

        // WinProc function that processes Windows messages
        [Signature("40 55 53 56 57 41 54 41 56 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 E0", DetourName = nameof(WndProcDetour))]
        private readonly Hook<WndProcDelegate> wndProcHook = null!;

        public Plugin(IPluginLog pluginLog, IGameInteropProvider gameInteropProvider)
        {
            this.pluginLog = pluginLog;
            gameInteropProvider.InitializeFromAttributes(this);

            this.wndProcHook.Enable();
        }

        private unsafe nint WndProcDetour(nint hWnd, uint msg, nuint wParam, nint lParam)
        {
            if (msg == WM_DEVICECHANGE)
            {
                this.pluginLog.Debug("WM_DEVICECHANGE received. wParam: 0x{0:X}, lParam: 0x{1:X}", wParam, lParam);
                switch ((uint)wParam)
                {
                    case DBT_DEVNODES_CHANGED:
                        return nint.Zero; // Skip polling on generic device change notifications
                    case DBT_DEVICEARRIVAL:
                    case DBT_DEVICEREMOVECOMPLETE:
                        bool ignore = true;
                        DEV_BROADCAST_HDR* pDevHdr = (DEV_BROADCAST_HDR*)lParam;
                        if (pDevHdr != null && pDevHdr->dbch_devicetype == DBT_DEVTYP_DEVICEINTERFACE)
                        {
                            Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
                            DEV_BROADCAST_DEVICEINTERFACE devW = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(lParam);
                            Guid interfaceGuid = devW.dbcc_classguid;
                            string deviceName = devW.dbcc_name ?? "Unknown";

                            // Check if the device interface GUID matches HID, MSDN states we can get port devices by default
                            if (interfaceGuid == GUID_DEVINTERFACE_HID
                                && (
                                       deviceName.Contains("IG_", StringComparison.Ordinal) // XInput
                                    || deviceName.Contains("054c", StringComparison.Ordinal) // Sony
                                )
                                && !deviceName.Contains(@"\kbd", StringComparison.Ordinal)) // Special K ignores these
                            {
                                ignore = false; // Relevant device change detected
                                pluginLog.Information($"XInput Device change detected. Interface: {deviceName}");
                            }
                            else
                            {
                                // Log the device interface GUID for debugging purposes
                                pluginLog.Debug($"Unknown device change detected. Interface: {deviceName}");
                            }
                        }

                        if (!ignore)
                        {
                            // force a poll on device arrival/removal in original handler
                            pluginLog.Information($"Processing relevant device change notification.");
                            return wndProcHook.Original(hWnd, WM_DEVICECHANGE, DBT_DEVNODES_CHANGED, nint.Zero);
                        }
                        return nint.Zero;
                }
            }
            else
            {
                // HACK: can't get hWnd at initialization, so register on first non-devicechange message
                if (!this.deviceNotificationRegistered)
                {
                    this.deviceNotificationRegistered = true;
                    this.pluginLog.Debug("Registering for device notifications.");
                    DeviceNotification.Register(hWnd, DBT_DEVTYP_DEVICEINTERFACE, GUID_DEVINTERFACE_HID);
                }
            }
            return this.wndProcHook.Original(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            this.pluginLog.Debug("Unregistering for device notifications.");
            DeviceNotification.Unregister();
            this.wndProcHook.Dispose();
        }
    }
}
