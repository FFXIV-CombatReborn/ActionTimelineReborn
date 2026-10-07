namespace ActionTimelineReborn.Timeline;

public class StatusLineItem : ITimelineItem
{
    public uint Icon { get; set; }
    public string? Name { get; set; }
    public float TimeDuration { get; set; }
    public DateTime StartTime { get; init; }

    public DateTime EndTime => StartTime + TimeSpan.FromSeconds(TimeDuration);

    public byte Stack { get; set; }
}
