using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using System.Numerics;

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

    public void Draw(DateTime time, Vector2 windowPos, Vector2 windowSize, TimelineLayer icon, DrawingSettings setting)
    {
        Vector2 rightCenter = windowPos + (setting.IsHorizonal
            ? new Vector2(windowSize.X, windowSize.Y / 2 + setting.CenterOffset)
            : new Vector2(windowSize.X / 2 + setting.CenterOffset, windowSize.Y));
        rightCenter -= setting.TimeOffset * setting.TimeDirectionPerSecond;
        DrawItemWithCenter(rightCenter - (float)(time - StartTime).TotalSeconds * setting.TimeDirectionPerSecond, icon, setting);
    }

    public void DrawItemWithCenter(Vector2 centerPos, TimelineLayer icon, DrawingSettings setting)
    {
        int GcdSize = setting.GCDIconSize;
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();

        switch (Type)
        {
            case TimelineItemType.GCD:
                DrawItemWithCenter(drawList, centerPos, setting.TimeDirectionPerSecond, GcdSize, icon, setting);
                break;

            case TimelineItemType.OGCD when setting.ShowOGCD:
                float oGcdOffset = setting.OGCDOffset;
                int oGcdSize = setting.OGCDIconSize;
                Vector2 oGcdCenter = centerPos - (oGcdOffset * GcdSize + oGcdSize / 2) * setting.DownDirection;
                DrawItemWithCenter(drawList, oGcdCenter, setting.TimeDirectionPerSecond, oGcdSize, icon, setting);
                break;

            case TimelineItemType.AutoAttack when setting.ShowAutoAttack:
                float autoAttackOffset = setting.AutoAttackOffset;
                int autoAttackSize = setting.AutoAttackIconSize;
                Vector2 autoAttackCenter = centerPos + (autoAttackOffset * GcdSize
                    + (autoAttackSize + GcdSize) / 2) * setting.DownDirection;
                DrawItemWithCenter(drawList, autoAttackCenter, setting.TimeDirectionPerSecond, autoAttackSize, icon, setting);
                break;
        }
    }

    private static (IDalamudTextureWrap texture, string? name)[] GetTextures(HashSet<(uint icon, string? name)> iconIds)
    {
        List<(IDalamudTextureWrap texture, string? name)> result = new List<(IDalamudTextureWrap texture, string? name)>(iconIds.Count);
        foreach ((uint icon, string? name) in iconIds)
        {
            IDalamudTextureWrap? texture = DrawHelper.GetTextureFromIconId(icon);
            if (texture == null)
            {
                continue;
            }

            result.Add((texture, name));
        }
        return [.. result];
    }

    public const float HeightRatio = 4 / 3f;
    private void DrawItemWithCenter(ImDrawListPtr drawList, Vector2 centerPos, Vector2 unitPerSecond, float iconSize, TimelineLayer icon, DrawingSettings setting)
    {
        switch (icon)
        {
            case TimelineLayer.Icon:
                Vector2 pos = centerPos - iconSize / 2 * setting.RealDownDirection;
                drawList.DrawActionIcon(Icon, IsHq, pos, iconSize);
                if (!string.IsNullOrEmpty(Name) && DrawHelper.IsInRect(pos, new Vector2(iconSize)))
                {
                    ImGui.SetTooltip(Name);
                }

                return;

            case TimelineLayer.Status when setting.ShowStatus:
                int statusSize = setting.StatusIconSize;
                Vector2 center = centerPos + setting.TimeDirection * iconSize / 2 - setting.DownDirection
                    * (iconSize / 2 + statusSize * (setting.IsHorizonal ? HeightRatio : 1) * (1 + setting.StatusOffset));
                (IDalamudTextureWrap texture, string? name)[] gains = GetTextures(StatusGainIcon);
                (IDalamudTextureWrap texture, string? name)[] lose = GetTextures(StatusLoseIcon);
                uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, setting.StatusIconAlpha));
                uint gainColor = ImGui.ColorConvertFloat4ToU32(setting.StatusGainColor);
                uint loseColor = ImGui.ColorConvertFloat4ToU32(setting.StatusLoseColor);

                float statusStep = setting.IsHorizonal ? statusSize : statusSize * HeightRatio;
                center -= setting.TimeDirection * statusStep * (gains.Length + lose.Length) / 2;
                if (setting.IsReverse)
                {
                    center += setting.DownDirection * (setting.IsHorizonal ? statusSize * HeightRatio : statusSize);
                }
                for (int i = 0; i < gains.Length; i++)
                {
                    Vector2 size = new Vector2(statusSize, statusSize * HeightRatio);
                    drawList.AddImage(gains[i].texture.Handle, center,
                        center + size, Vector2.Zero, Vector2.One, color);

                    string? name = gains[i].name;
                    if (!string.IsNullOrEmpty(name) && DrawHelper.IsInRect(center, size))
                    {
                        ImGui.SetTooltip(name);
                    }

                    drawList.AddText(UiBuilder.IconFont, statusSize / 2f, center, gainColor, FontAwesomeIcon.Plus.ToIconString());

                    center += setting.TimeDirection * statusStep;
                }
                for (int i = 0; i < lose.Length; i++)
                {
                    Vector2 size = new Vector2(statusSize, statusSize * HeightRatio);
                    drawList.AddImage(lose[i].texture.Handle, center,
                        center + size, Vector2.Zero, Vector2.One, color);

                    string? name = lose[i].name;
                    if (!string.IsNullOrEmpty(name) && DrawHelper.IsInRect(center, size))
                    {
                        ImGui.SetTooltip(name);
                    }

                    drawList.AddText(UiBuilder.IconFont, statusSize / 2f, center, loseColor, FontAwesomeIcon.Ban.ToIconString());

                    center += setting.TimeDirection * statusStep;
                }

                return;

            case TimelineLayer.General:
                //Get Info.
                float highPos = MathF.Min(setting.GCDHeightLow, setting.GCDHeightHigh);
                float lowPos = MathF.Max(setting.GCDHeightLow, setting.GCDHeightHigh);
                float rounding = setting.GCDRound;

                Vector2 leftTop = centerPos + (highPos * iconSize - iconSize / 2) * setting.RealDownDirection;
                Vector2 leftBottom = centerPos + (lowPos * iconSize - iconSize / 2) * setting.RealDownDirection;
                ImDrawFlags flag = ImDrawFlags.RoundCornersAll;

                Vector2 min = centerPos + iconSize / 2 * setting.TimeDirection;

                //Background
                uint GcdBackColor = ImGui.ColorConvertFloat4ToU32(setting.BackgroundColor);
                drawList.AddRectFilled(MinX(leftTop, min), MinX(leftBottom + unitPerSecond * MathF.Max(GCDTime, setting.ShowAnimationLock ? CastingTime + AnimationLockTime : CastingTime), min), GcdBackColor, rounding, flag);

                Vector2 castOffset = unitPerSecond * CastingTime;

                //AnimationLock
                if (setting.ShowAnimationLock)
                {
                    uint animationLockColor = ImGui.ColorConvertFloat4ToU32(setting.AnimationLockColor);
                    drawList.AddRectFilled(MinX(leftTop, min),
                        MinX(leftBottom + castOffset + unitPerSecond * AnimationLockTime, min),
                        animationLockColor, rounding, flag);
                }

                //Casting
                uint castColor = State switch
                {
                    TimelineItemState.Canceled => ImGui.ColorConvertFloat4ToU32(setting.CastCanceledColor),
                    TimelineItemState.Casting => ImGui.ColorConvertFloat4ToU32(setting.CastInProgressColor),
                    _ => ImGui.ColorConvertFloat4ToU32(setting.CastFinishedColor)
                };
                drawList.AddRectFilled(MinX(leftTop, min), MinX(leftBottom + castOffset, min), castColor, rounding, flag);

                //GCD Fore
                uint GcdForeColor = ImGui.ColorConvertFloat4ToU32(setting.GCDBorderColor);
                drawList.AddRect(MinX(leftTop, min),
                     MinX(leftBottom + unitPerSecond * GCDTime, min),
                    GcdForeColor, rounding, flag, setting.GCDThickness);

                //Damage
                if (setting.ShowDamageType)
                {
                    uint lightCol = Damage switch
                    {
                        DamageType.Critical => ImGui.ColorConvertFloat4ToU32(setting.CriticalColor),
                        DamageType.Direct => ImGui.ColorConvertFloat4ToU32(setting.DirectColor),
                        DamageType.CriticalDirect => ImGui.ColorConvertFloat4ToU32(setting.CriticalDirectColor),
                        _ => 0u,
                    };
                    drawList.DrawDamage(centerPos - iconSize / 2 * setting.RealDownDirection, iconSize, lightCol);
                }

                return;
        }
        //Name
    }

    private static Vector2 MinX(Vector2 pos, Vector2 minPos)
    {
        return new Vector2(MathF.Max(pos.X, minPos.X), MathF.Max(pos.Y, minPos.Y));
    }
}
