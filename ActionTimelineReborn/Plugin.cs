using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Timeline;
using ActionTimelineReborn.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using RebornMaterial;

namespace ActionTimelineReborn;

public class Plugin : IDalamudPlugin
{
    public static string Name => "ActionTimelineReborn";

    public static string Version { get; private set; } = "";

    public static Settings Settings { get; private set; } = null!;

    private static WindowSystem _windowSystem = null!;
    private static SettingsWindow _settingsWindow = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

        TimelineManager.Initialize();

        try
        {
            Settings = pluginInterface.GetPluginConfig() as Settings ?? new Settings();
        }
        catch
        {
            Settings = new Settings();
        }

        M3.Initialize(pluginInterface, Settings);

        CreateWindows();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Cmd("/atl", "Opens the ActionTimelineReborn configuration window.")]
    [SubCmd("lock", "Lock all windows")]
    [SubCmd("unlock", "Unlock all windows")]
    private static void PluginCommand(string command, string arguments)
    {
        string? sub = arguments.Split(' ').FirstOrDefault();
        if (string.Equals("unlock", sub, StringComparison.OrdinalIgnoreCase))
        {
            foreach (DrawingSettings setting in Settings.TimelineSettings)
            {
                setting.Locked = false;
            }
        }
        else if (string.Equals("lock", sub, StringComparison.OrdinalIgnoreCase))
        {
            foreach (DrawingSettings setting in Settings.TimelineSettings)
            {
                setting.Locked = true;
            }
        }
        else
        {
            _settingsWindow.IsOpen = !_settingsWindow.IsOpen;
        }
    }

    private void CreateWindows()
    {
        _settingsWindow = new SettingsWindow();

        _windowSystem = new WindowSystem("ActionTimelineReborn_Windows");
        _windowSystem.AddWindow(_settingsWindow);
    }

    private void Draw()
    {
        if (Settings == null || !Player.Available)
        {
            return;
        }

        if (Svc.GameGui.GameUiHidden)
        {
            return;
        }

        M3.BeginFrame();
        _windowSystem?.Draw();

        if (!ShowTimeline())
        {
            return;
        }

        int index = 0;
        foreach (DrawingSettings setting in Settings.TimelineSettings)
        {
            TimelineWindow.Draw(setting, index++);
        }
    }

    private static bool ShowTimeline()
    {
        if (Settings.ShowTimelineOnlyInCombat && !Svc.Condition[ConditionFlag.InCombat])
        {
            return false;
        }

        if (Settings.ShowTimelineOnlyInDuty && !Svc.Condition[ConditionFlag.BoundByDuty])
        {
            return false;
        }

        if (Settings.HideTimelineInCutscene
            && (Svc.Condition[ConditionFlag.WatchingCutscene]
            || Svc.Condition[ConditionFlag.WatchingCutscene78]
            || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]))
        {
            return false;
        }

        if (Settings.HideTimelineInQuestEvent
            && (Svc.Condition[ConditionFlag.OccupiedInQuestEvent]))
        {
            return false;
        }

        return true;
    }

    public static void OpenConfigUi()
    {
        _settingsWindow.Open();
    }

    public static void OpenTimelineSettings(int index)
    {
        _settingsWindow.Open(index);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Settings.Save();

        TimelineManager.Instance?.Dispose();

        _windowSystem.RemoveAllWindows();

        M3.Dispose();

        ECommonsMain.Dispose();

        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
    }
}
