using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Helpers;
using ActionTimelineReborn.Timeline;
using ActionTimelineReborn.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Commands;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using System.Diagnostics;
using static Dalamud.Interface.Windowing.Window;

namespace ActionTimelineReborn;

public class Plugin : IDalamudPlugin
{
    public static readonly SortedList<uint, byte> IconStack = new()
    {
        { 16203, 0 }, // Medicine.

        { 10155, 1 }, //PLD Fight or Flight.

        { 12556, 1 }, //WAR Inner Strength.

        { 17926, 1 }, //DRK Blood Weapon.

        { 13601, 1 }, //GNB No mercy.

        { 12627, 1 }, //WHM Presence of Mind.

        { 12809, 1 }, //SCH Chain Stratagem.

        { 13245, 1 }, //AST Divination.
        { 13259, 2 }, //AST Harmony of Spirit.

        { 12532, 1 }, //MNK Brotherhood.
        { 12528, 2 }, //MNK Brotherhood.

        { 12578, 1 }, //DRG Battle Litany.
        { 12581, 2 }, //DRG Right Eye.
        { 10304, 3 }, //DRG Lance Charge.

        { 12918, 1 }, //NIN Trick Attack.
        { 15020, 2 }, //NIN Vulnerability Up.

        { 12601, 1 }, //BRD Battle Voice.
        { 12622, 2 }, //BRD Radiant Finale.
        { 10354, 3 }, //BRD Radiant Finale.

        { 13011, 1 }, //MCH Wildfire.

        { 13714, 1 }, //DNC Devilment.
        { 13709, 2 }, //DNC Technical Finish.

        { 12653, 1 }, //BLM Ley Lines.

        { 13409, 1 }, //RDM Embolden.

        { 12699, 1 }, //SMN 2703.
    };
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
        DrawHelper.Init();

        try
        {
            Settings = pluginInterface.GetPluginConfig() as Settings ?? new Settings();
        }
        catch
        {
            Settings = new Settings();
        }

        CreateWindows();
        _settingsWindow.TitleBarButtons.Add(new TitleBarButton()
        {
            Icon = FontAwesomeIcon.Heart,
            ShowTooltip = () =>
            {
                ImGui.BeginTooltip();
                ImGui.Text("Support the developer on Ko-fi");
                ImGui.EndTooltip();
            },
            Priority = 2,
            Click = _ =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "https://ko-fi.com/ltscombatreborn",
                        UseShellExecute = true,
                        Verb = string.Empty
                    });
                }
                catch
                {
                    // ignored
                }
            },
            AvailableClickthrough = true
        });
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
        var sub = arguments.Split(' ').FirstOrDefault();
        if(string.Equals("unlock", sub, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var setting in Settings.TimelineSettings)
            {
                setting.Locked = false;
            }
        }
        else if (string.Equals("lock", sub, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var setting in Settings.TimelineSettings)
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
        if (Settings == null || !Player.Available) return;
        if (Svc.GameGui.GameUiHidden) return;

        _windowSystem?.Draw();

        if (!ShowTimeline()) return;

        int index = 0;
        foreach (var setting in Settings.TimelineSettings)
        {
            TimelineWindow.Draw(setting, index++);
        }
    }

    private bool ShowTimeline()
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
        _settingsWindow.IsOpen = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        Settings.Save();

        TimelineManager.Instance?.Dispose();

        _windowSystem.RemoveAllWindows();

        ECommonsMain.Dispose();

        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
    }
}
