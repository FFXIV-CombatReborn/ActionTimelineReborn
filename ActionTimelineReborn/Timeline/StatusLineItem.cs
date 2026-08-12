using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using System.Numerics;

namespace ActionTimelineReborn.Timeline;

public class StatusLineItem : ITimelineItem
{
    public uint Icon { get; set; }
    public string? Name { get; set; }
    public float TimeDuration { get; set; }
    public DateTime StartTime { get; init; }

    public DateTime EndTime => StartTime + TimeSpan.FromSeconds(TimeDuration);

    public byte Stack { get; set; }

    public void Draw(DateTime time, Vector2 windowPos, Vector2 windowSize, DrawingSettings setting)
    {
        Vector2 rightCenter = windowPos + (setting.IsHorizonal
            ? new Vector2(windowSize.X, windowSize.Y / 2 + setting.CenterOffset)
            : new Vector2(windowSize.X / 2 + setting.CenterOffset, windowSize.Y));
        rightCenter -= setting.TimeOffset * setting.TimeDirectionPerSecond;
        DrawItemWithCenter(rightCenter - (float)(time - StartTime).TotalSeconds * setting.TimeDirectionPerSecond, windowPos, setting);
    }

    public void DrawItemWithCenter(Vector2 centerPos, Vector2 windowPos, DrawingSettings setting)
    {
        int GcdSize = setting.GCDIconSize;
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();

        float statusHeight = setting.StatusLineSize;
        ImDrawFlags flag = ImDrawFlags.RoundCornersAll;
        float rounding = setting.GCDRound;

        IDalamudTextureWrap? texture = DrawHelper.GetTextureFromIconId(Icon);
        if (texture == null)
        {
            return;
        }

        uint col = DrawHelper.GetTextureAverageColor(Icon);

        Vector2 leftTop = centerPos + setting.DownDirection * (statusHeight * (setting.IsReverse ? Stack + 1 : Stack) + GcdSize / 2);
        if (setting.ShowAutoAttack)
        {
            float autoAttackOffset = setting.AutoAttackOffset;
            int autoAttackSize = setting.AutoAttackIconSize;
            leftTop += setting.DownDirection * (autoAttackOffset * GcdSize + autoAttackSize);
        }
        float statusWidth = setting.IsHorizonal ? statusHeight : statusHeight / TimelineItem.HeightRatio;
        Vector2 shrink = statusWidth * 0.3f * setting.RealDownDirection;
        Vector2 rightBottom = leftTop + setting.TimeDirectionPerSecond * TimeDuration + setting.RealDownDirection * statusWidth - shrink;

        drawList.AddRectFilled(leftTop + shrink, rightBottom, col, rounding, flag);
        if (!string.IsNullOrEmpty(Name) && DrawHelper.IsInRect(leftTop + shrink, rightBottom - leftTop - shrink))
        {
            ImGui.SetTooltip(Name);
        }

        if (rightBottom.X <= windowPos.X)
        {
            return;
        }

        leftTop.X = Math.Max(leftTop.X, windowPos.X);
        leftTop.Y = Math.Max(leftTop.Y, windowPos.Y);

        Vector2 size = new Vector2(statusHeight / TimelineItem.HeightRatio, statusHeight);
        drawList.AddImage(texture.Handle, leftTop,
            leftTop + size, Vector2.Zero, Vector2.One);
        if (!string.IsNullOrEmpty(Name) && DrawHelper.IsInRect(leftTop, size))
        {
            ImGui.SetTooltip(Name);
        }
    }
}
