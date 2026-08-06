using ActionTimelineReborn.Configurations;
using Dalamud.Configuration;
using ECommons.DalamudServices;

namespace ActionTimelineReborn.Configurations;

[Serializable]
public class Settings : IPluginConfiguration
{
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

    /// <summary>
    /// Experimental latency compensation. Additive and off by default; see
    /// <see cref="ExperimentalSettings"/>.
    /// </summary>
    public ExperimentalSettings Experimental { get; set; } = new();

    public int Version { get; set; } = 6;

    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
}
