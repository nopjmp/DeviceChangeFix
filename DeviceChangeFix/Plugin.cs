using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using SharpDX.DirectInput;
using System;

namespace DeviceChangeFix
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "DeviceChangeFix";

        private int controllerCount;
        private readonly DirectInput directInput = new();

        public Plugin()
        {
            this.controllerCount = this.GetControllerCount();

            SignatureHelper.Initialise(this);
            this.deviceChangeDelegateHook.Enable();
        }

        public delegate IntPtr DeviceChangeDelegate(IntPtr inputDeviceManager);
        // non-nullable, plugin can't work without this signature
        [Signature("48 83 EC 38 0F B6 81", DetourName = nameof(DeviceChangeDetour))]
        private readonly Hook<DeviceChangeDelegate> deviceChangeDelegateHook = null!;
        private unsafe IntPtr DeviceChangeDetour(IntPtr inputDeviceManager)
        {
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
            this.directInput.Dispose();
        }
    }
}
