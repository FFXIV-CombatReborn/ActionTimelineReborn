namespace ActionTimelineReborn.Timeline;

public class TimelineItem : ITimelineItem
{
    public string? Name { get; set; }
    public ushort Icon { get; set; }
    public bool IsHq { get; set; }
    public DateTime StartTime { get; init; }

    public float AnimationLockTime { get; set; }

    public float CastingTime { get; set; }

    public float GCDTime { get; set; }

    public TimelineItemType Type { get; set; }

    public TimelineItemState State { get; set; }

    public DamageType Damage { get; set; } = DamageType.None;

    public float TimeDuration => MathF.Max(GCDTime, CastingTime + AnimationLockTime);

    public DateTime EndTime => StartTime + TimeSpan.FromSeconds(TimeDuration);

    public HashSet<(uint icon, string? name)> StatusGainIcon { get; } = new(4);
    public HashSet<(uint icon, string? name)> StatusLoseIcon { get; } = new(4);
}
