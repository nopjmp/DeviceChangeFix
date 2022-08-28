using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using SharpDX.DirectInput;

namespace DeviceChangeFix
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "DeviceChangeFix";

        private int controllerCount;
        private readonly DirectInput directInput = new();

        private DalamudPluginInterface PluginInterface { get; init; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }

        public delegate IntPtr DeviceChangeDelegate(IntPtr inputDeviceManager);
        private readonly Hook<DeviceChangeDelegate> deviceChangeDelegateHook;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] SigScanner sigScanner)
        {
            this.PluginInterface = pluginInterface;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            this.PluginUi = new PluginUI(this.Configuration);

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            this.controllerCount = this.GetControllerCount();

            // function that is called from WndProc when a device change happens
            // plugin can't work without this sig so use ScanText instead of TryScanText
            var renderAddress = sigScanner.ScanText("48 83 EC 38 0F B6 81");
            this.deviceChangeDelegateHook = Hook<DeviceChangeDelegate>.FromAddress(renderAddress, this.DeviceChangeDetour);
            this.deviceChangeDelegateHook.Enable();
        }

        private unsafe IntPtr DeviceChangeDetour(IntPtr inputDeviceManager)
        {
            if (this.Configuration.BypassLogic == true)
            {
                PluginLog.Information($"triggered");
                return this.deviceChangeDelegateHook!.Original(inputDeviceManager);
            }

            // Only call the original if the number of connected controllers
            // actually changed since the last time the hook was called
            int newControllerCount = this.GetControllerCount();
            if (newControllerCount != this.controllerCount)
            {
                PluginLog.Information($"{Math.Abs(newControllerCount - this.controllerCount)} devices {(newControllerCount > this.controllerCount ? "added" : "removed")}, polling started");
                this.controllerCount = newControllerCount;

                return this.deviceChangeDelegateHook!.Original(inputDeviceManager);
            }
            PluginLog.Information("No input devices changed, polling skipped");

            return IntPtr.Zero;
        }

        private int GetControllerCount()
        {
            return this.directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices).Count;
        }

        public void Dispose()
        {
            this.deviceChangeDelegateHook.Disable();
            this.deviceChangeDelegateHook.Dispose();
            this.PluginUi.Dispose();
            this.directInput.Dispose();
        }

        private void DrawUI()
        {
            this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            this.PluginUi.SettingsVisible = true;
        }
    }
}
