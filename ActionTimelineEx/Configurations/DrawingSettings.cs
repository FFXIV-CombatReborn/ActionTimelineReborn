using Dalamud.Interface.Colors;
using System.Numerics;

namespace ActionTimelineReborn.Configurations;

public class DrawingSettings
{
    public string Name = "Major";

    public bool Enable = true;
    public bool IsRotation = false;
    public bool IsHorizonal = true;
    public bool IsReverse = false;
    public Vector2 TimeDirectionPerSecond => TimeDirection * SizePerSecond;
    public Vector2 TimeDirection => IsHorizonal ? Vector2.UnitX : Vector2.UnitY;
    public Vector2 DownDirection => IsReverse ? -RealDownDirection : RealDownDirection;
    public Vector2 RealDownDirection => IsHorizonal ? Vector2.UnitY : Vector2.UnitX;

    public bool Locked = false;
    public Vector4 LockedBackgroundColor = new (0f, 0f, 0f, 0.5f);
    public Vector4 UnlockedBackgroundColor = new (0f, 0f, 0f, 0.75f);

    public float SizePerSecond = 60;
    public float CenterOffset = 0;

    public int TimeOffsetSetting = 2;
    public int TimeOffset => IsRotation ? -TimeOffsetSetting : TimeOffsetSetting;
    public int GCDIconSize = 40;

    public bool ShowOGCD = true;
    public int OGCDIconSize = 30;
    public float OGCDOffset = 0.1f;

    public bool ShowAutoAttack = true;
    public int AutoAttackIconSize = 15;
    public float AutoAttackOffset = 0.1f;

    public bool ShowStatus = true;
    public int StatusIconSize = 15;
    public float StatusIconAlpha = 0.5f;
    public float StatusOffset = 0.1f;
    public Vector4 StatusGainColor = ImGuiColors.HealerGreen;
    public Vector4 StatusLoseColor = ImGuiColors.DalamudRed;

    public bool ShowDamageType = true;
    public Vector4 DirectColor = ImGuiColors.DalamudYellow;
    public Vector4 CriticalColor = ImGuiColors.DalamudOrange;
    public Vector4 CriticalDirectColor = ImGuiColors.DPSRed;

    public Vector4 BackgroundColor = new (0.5f, 0.5f, 0.5f, 0.5f);
    public Vector4 GCDBorderColor = new (0.9f, 0.9f, 0.9f, 1f);
    public float GCDThickness = 1.5f;
    public float GCDHeightLow = 0.5f;
    public float GCDHeightHigh = 0.8f;
    public float GCDRound = 2;

    public Vector4 CastInProgressColor = new (0.2f, 0.8f, 0.2f, 1f);
    public Vector4 CastFinishedColor = new (0.5f, 0.5f, 0.5f, 1f);
    public Vector4 CastCanceledColor = new (0.8f, 0.2f, 0.2f, 1f);

    public bool ShowAnimationLock = true;
    public Vector4 AnimationLockColor = new (0.8f, 0.7f, 0.6f, 1f);

    public bool ShowStatusLine = true;
    public float StatusLineSize = 18;

    public bool ShowGrid = true;
    public bool ShowGridCenterLine = false;
    public bool GridDivideBySeconds = true;
    public bool GridShowSecondsText = true;
    public bool GridSubdivideSeconds = true;
    public int GridSubdivisionCount = 2;
    public float GridLineWidth = 1;
    public float GridCenterLineWidth = 1f;
    public float GridStartLineWidth = 3;
    public float GridSubdivisionLineWidth = 1;
    public Vector4 GridLineColor = new (0.3f, 0.3f, 0.3f, 1f);
    public Vector4 GridCenterLineColor = new (0.5f, 0.5f, 0.5f, 0.3f);
    public Vector4 GridStartLineColor = new (0.3f, 0.5f, 0.2f, 1f);
    public Vector4 GridSubdivisionLineColor = new (0.3f, 0.3f, 0.3f, 0.2f);

    public bool ShowGCDClippingSetting = true;
    public bool ShowGCDClipping => !IsRotation && ShowGCDClippingSetting;
    public float GCDClippingThreshold = 0.15f;
    public int GCDClippingMaxTime = 2;
    public Vector4 GCDClippingColor = new (1f, 0.2f, 0.2f, 0.3f);
    public Vector4 GCDClippingTextColor = new (0.9f, 0.9f, 0.9f, 1f);
}
