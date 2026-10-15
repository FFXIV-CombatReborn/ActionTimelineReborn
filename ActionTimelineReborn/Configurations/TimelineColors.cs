using Dalamud.Interface.Colors;
using RebornMaterial;
using System.Numerics;

namespace ActionTimelineReborn.Configurations;

public static class TimelineColors
{
    // The theme's color for each part, used until a timeline picks its own, so it follows the accent color.
    public static Vector4 Default(TimelineColor color)
    {
        M3Scheme s = M3.Scheme;
        return color switch
        {
            TimelineColor.LaneBackground => M3.Alpha(s.SurfaceContainerHighest, 0.3f),
            TimelineColor.UpcomingShade => M3.Alpha(s.Surface, 0.35f),
            TimelineColor.Hover => M3.Alpha(s.OnSurface, M3.StateHover),

            TimelineColor.GcdBar => M3.Alpha(s.SecondaryContainer, 0.85f),
            TimelineColor.CastingGcdBar => M3.Alpha(s.PrimaryContainer, 0.9f),
            TimelineColor.CanceledGcdBar => M3.Alpha(s.ErrorContainer, 0.75f),
            TimelineColor.CastProgress => s.Primary,
            TimelineColor.CanceledCast => s.Error,

            TimelineColor.IconBorder => M3.Alpha(s.OutlineVariant, 0.8f),
            TimelineColor.AnimationLock => M3.Alpha(s.Tertiary, 0.9f),
            TimelineColor.AutoAttack => M3.Alpha(s.OnSurfaceVariant, 0.8f),

            // ATR's damage colors, turned towards the accent so they sit with the rest of the theme.
            TimelineColor.DirectHit => M3.Harmonize(ImGuiColors.DalamudYellow),
            TimelineColor.CriticalHit => M3.Harmonize(ImGuiColors.DalamudOrange),
            TimelineColor.CriticalDirectHit => M3.Harmonize(ImGuiColors.DPSRed),

            TimelineColor.StatusGained => s.Success,
            TimelineColor.StatusLost => s.Error,

            TimelineColor.Clipping => M3.Alpha(s.Error, 0.18f),
            TimelineColor.AxisLines => M3.Alpha(s.OutlineVariant, 0.45f),
            TimelineColor.NowLine => s.Primary,

            TimelineColor.AxisText => M3.Alpha(s.OnSurfaceVariant, 0.85f),
            TimelineColor.NowText => s.Primary,
            TimelineColor.ClippingText => s.Error,
            TimelineColor.EmptyText => M3.Alpha(s.OnSurfaceVariant, 0.7f),

            _ => s.OnSurface,
        };
    }
}
