namespace ActionTimelineReborn.Timeline;

public enum TimelineItemType : byte
{
    GCD,
    OGCD,
    AutoAttack,
}

public enum TimelineItemState : byte
{
    Casting,
    Canceled,
    Finished,
}

public enum StatusChangeType : byte
{
    Gained,
    Lost,
    StacksUp,
    StacksDown,
}
