using Dalamud.Configuration;
using ECommons.DalamudServices;
using RebornMaterial;
using System.Numerics;

namespace ActionTimelineReborn.Configurations;

[Serializable]
public class Settings : IPluginConfiguration, IM3Settings
{
    public static readonly Vector4 DefaultAccentColor = M3ColorMath.FromRgb(0x6E4297);

    private const int CurrentVersion = 7;

    public bool ShowTimelineOnlyInDuty = false;
    public bool ShowTimelineOnlyInCombat = false;
    public bool HideTimelineInCutscene = true;
    public bool HideTimelineInQuestEvent = true;
    public bool Record = true;
    public bool RecordTargetStatus = true;
    public List<DrawingSettings> TimelineSettings = [];
    public HashSet<ushort> HideStatusIds = [];
    public bool PrintClipping = false;
    public int PrintClippingMin = 150;
    public int PrintClippingMax = 2000;
    public int Version { get; set; } = CurrentVersion;

    public Vector4 UiAccentColor { get; set; } = DefaultAccentColor;
    public float UiTextScale { get; set; } = 1f;
    public float UiElementScale { get; set; } = 1f;
    public float UiPaddingScale { get; set; } = 1f;
    public float UiMotionScale { get; set; } = 1f;
    public bool UiCompactLayout { get; set; } = false;
    public bool UiInlineDescriptions { get; set; } = true;
    public bool UiIconSidebar { get; set; } = false;

    bool IM3Settings.UiDescriptionTooltips => !UiInlineDescriptions;

    public void Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return;
        }

        if (UiAccentColor == M3.DefaultSeed)
        {
            UiAccentColor = DefaultAccentColor;
        }

        Version = CurrentVersion;
        Save();
    }

    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
}
