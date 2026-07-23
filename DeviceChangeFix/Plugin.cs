using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using System.Runtime.InteropServices;

namespace DeviceChangeFix
{
    public sealed class Plugin : IDalamudPlugin
    {
        private const uint WM_DEVICECHANGE = 0x0219;
        private const uint DBT_DEVNODES_CHANGED = 0x0007;

        private delegate nint WndProcDelegate(nint hWnd, uint msg, nuint wParam, nint lParam);

        private readonly IPluginLog pluginLog;
        private readonly ControllerSetMonitor monitor;

        // WinProc function that processes Windows messages
        [Signature("40 55 53 56 57 41 54 41 56 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 E0", DetourName = nameof(WndProcDetour))]
        private readonly Hook<WndProcDelegate> wndProcHook = null!;

        public Plugin(IPluginLog pluginLog, IGameInteropProvider gameInteropProvider)
        {
            this.pluginLog = pluginLog;
            monitor = new ControllerSetMonitor();
            gameInteropProvider.InitializeFromAttributes(this);

            wndProcHook.Enable();
        }

        private nint WndProcDetour(nint hWnd, uint msg, nuint wParam, nint lParam)
        {
            if (msg != WM_DEVICECHANGE)
            {
                return wndProcHook.Original(hWnd, msg, wParam, lParam);
            }

            pluginLog.Debug("WM_DEVICECHANGE received. wParam: 0x{0:X}, lParam: 0x{1:X}", wParam, lParam);

            if ((uint)wParam == DBT_DEVNODES_CHANGED && RefreshControllerSet() == RefreshResult.Changed)
            {
                // Let the original handler set the hotplug flag so the game
                // re-enumerates controllers.
                return wndProcHook.Original(hWnd, WM_DEVICECHANGE, DBT_DEVNODES_CHANGED, nint.Zero);
            }

            return nint.Zero;
        }

        private RefreshResult RefreshControllerSet()
        {
            RefreshResult result = monitor.Refresh(out var added, out var removed);
            if (result == RefreshResult.Failed)
            {
                pluginLog.Warning($"Raw Input device enumeration failed. Win32 error: {Marshal.GetLastWin32Error()}");
                return result;
            }
            foreach (string device in added)
            {
                pluginLog.Information($"Controller connected: {device}");
            }
            foreach (string device in removed)
            {
                pluginLog.Information($"Controller disconnected: {device}");
            }
            return result;
        }

        public void Dispose()
        {
            wndProcHook.Dispose();
        }
    }
}
