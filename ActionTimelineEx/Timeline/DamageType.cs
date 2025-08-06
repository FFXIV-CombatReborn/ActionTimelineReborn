namespace ActionTimelineReborn.Timeline;

[Flags]
public enum DamageType: byte
{
    None = 0,
    Direct = 1 << 0,
    Critical = 1 << 1,
    CriticalDirect = Direct | Critical,
}
