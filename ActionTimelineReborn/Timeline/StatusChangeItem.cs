namespace ActionTimelineReborn.Timeline;

public class StatusChangeItem : ITimelineItem
{
    public uint Icon { get; init; }
    public string? Name { get; init; }
    public byte Stacks { get; init; }
    public StatusChangeType Type { get; init; }
    public DateTime StartTime { get; init; }

    public DateTime EndTime => StartTime;
}
