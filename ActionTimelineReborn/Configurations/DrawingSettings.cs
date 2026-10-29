using Newtonsoft.Json;
using System.Numerics;

namespace ActionTimelineReborn.Configurations;

public class DrawingSettings
{
    public string Name = "Major";

    public bool Enable = true;
    public TimelineView View = TimelineView.Live;

    [JsonProperty]
    private bool IsRotation
    {
        set
        {
            if (value)
            {
                View = TimelineView.Rotation;
            }
        }
    }

    public bool Locked = false;
    public Vector4 LockedBackgroundColor = new(0f, 0f, 0f, 0.5f);
    public Vector4 UnlockedBackgroundColor = new(0f, 0f, 0f, 0.75f);
    public float Scale = 1f;

    public bool ShowOGCD = true;
    public bool ShowAutoAttack = true;
    public bool ShowAnimationLock = true;
    public bool ShowStatus = true;
    public bool ShowDamageType = true;
    public float StatusIconScale = 1f;

    public bool ShowGCDClippingSetting = true;
    public bool ShowGCDClipping => View == TimelineView.Live && ShowGCDClippingSetting;
    public float GCDClippingThreshold = 0.15f;
    public int GCDClippingMaxTime = 2;

    // Only the colors picked for this timeline. Everything else follows the theme.
    public Dictionary<TimelineColor, Vector4> Colors = [];

    public Vector4 GetColor(TimelineColor color)
    {
        return Colors.TryGetValue(color, out Vector4 value) ? value : TimelineColors.Default(color);
    }

    public void CopyColorsFrom(DrawingSettings other)
    {
        Colors = new Dictionary<TimelineColor, Vector4>(other.Colors);
        LockedBackgroundColor = other.LockedBackgroundColor;
        UnlockedBackgroundColor = other.UnlockedBackgroundColor;
    }
}
