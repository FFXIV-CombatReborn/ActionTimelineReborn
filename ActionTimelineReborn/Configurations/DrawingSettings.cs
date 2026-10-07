using System.Numerics;

namespace ActionTimelineReborn.Configurations;

public class DrawingSettings
{
    public string Name = "Major";

    public bool Enable = true;
    public bool IsRotation = false;

    public bool Locked = false;
    public Vector4 LockedBackgroundColor = new(0f, 0f, 0f, 0.5f);
    public Vector4 UnlockedBackgroundColor = new(0f, 0f, 0f, 0.75f);
    public float Scale = 1f;

    public bool ShowOGCD = true;
    public bool ShowAutoAttack = true;
    public bool ShowAnimationLock = true;
    public bool ShowStatus = true;
    public bool ShowDamageType = true;
    public bool ShowStatusLine = true;

    public bool ShowGCDClippingSetting = true;
    public bool ShowGCDClipping => !IsRotation && ShowGCDClippingSetting;
    public float GCDClippingThreshold = 0.15f;
    public int GCDClippingMaxTime = 2;
}
