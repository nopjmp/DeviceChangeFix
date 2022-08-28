using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface;

namespace DeviceChangeFix
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            DrawSettingsWindow();
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(10 * ImGui.GetFontSize(), 2 * ImGui.GetFontSize()), ImGuiCond.Always);
            if (ImGui.Begin("DeviceChangeFix Config", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // can't ref a property, so use a local copy
                var configValue = this.configuration.BypassLogic;
                if (ImGui.Checkbox("Bypass plugin logic", ref configValue))
                {
                    this.configuration.BypassLogic = configValue;
                    // save immediately on change instead of using a "Save and Close" button
                    this.configuration.Save();
                }
            }
            ImGui.End();
        }
    }
}
