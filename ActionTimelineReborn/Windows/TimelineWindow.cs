using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Timeline;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace ActionTimelineReborn.Windows;

internal static class TimelineWindow
{
    private const ImGuiWindowFlags _baseFlags = ImGuiWindowFlags.NoScrollbar
                                        | ImGuiWindowFlags.NoCollapse
                                        | ImGuiWindowFlags.NoTitleBar
                                        | ImGuiWindowFlags.NoNav
                                        | ImGuiWindowFlags.NoScrollWithMouse;

    public static void Draw(DrawingSettings setting, int index)
    {
        if (!setting.Enable || string.IsNullOrEmpty(setting.Name))
        {
            return;
        }

        ImGuiWindowFlags flag = _baseFlags;
        if (setting.Locked)
        {
            flag |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;
        }

        Vector4 bgColor = setting.Locked ? setting.LockedBackgroundColor : setting.UnlockedBackgroundColor;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);

        ImGui.SetNextWindowSize(new Vector2(560, 100) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(200, 200) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);

        if (ImGui.Begin($"Timeline: {index}", flag))
        {
            DrawContent(setting);
            ImGui.End();
        }

        ImGui.PopStyleVar(2);

        ImGui.PopStyleColor();
    }

    private static void DrawContent(DrawingSettings setting)
    {
        if (ImGui.IsWindowHovered())
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                Plugin.OpenConfigUi();
            }
        }
        Vector2 pos = ImGui.GetWindowPos();
        Vector2 size = ImGui.GetWindowSize();

        DateTime now = setting.IsRotation ? (TimelineManager.Instance?.EndTime ?? DateTime.Now - TimeSpan.FromSeconds(setting.TimeOffset)) : DateTime.Now;

        DateTime endTime = now - TimeSpan.FromSeconds((setting.IsHorizonal ? size.X : size.Y) / setting.SizePerSecond - setting.TimeOffset);

        DateTime last = now;
        List<TimelineItem>? list = TimelineManager.Instance?.GetItems(endTime, out last);

        Vector2 timeDirWhole = setting.IsHorizonal ? size.X * Vector2.UnitX : size.Y * Vector2.UnitY;
        Vector2 downDirWhole = setting.IsHorizonal ? size.Y * Vector2.UnitY : size.X * Vector2.UnitX;

        DrawGrid(pos, timeDirWhole, downDirWhole, setting);

        if (setting.ShowGCDClipping && list != null) //Clipping
        {
            uint gcdClippingColor = ImGui.ColorConvertFloat4ToU32(setting.GCDClippingColor);
            TimeSpan threshold = TimeSpan.FromSeconds(setting.GCDClippingThreshold);
            TimeSpan max = TimeSpan.FromSeconds(setting.GCDClippingMaxTime);

            foreach (TimelineItem item in list)
            {
                if (item.Type != TimelineItemType.GCD)
                {
                    continue;
                }

                DateTime start = item.StartTime;
                TimeSpan span = start - last;

                if (last != DateTime.MinValue && span >= threshold && span < max)
                {
                    Vector2 drawingLeftTop = pos + timeDirWhole
                        - (setting.TimeOffset + (float)(now - last).TotalSeconds) * setting.TimeDirectionPerSecond;


                    ImGui.GetWindowDrawList().AddRectFilled(drawingLeftTop, drawingLeftTop
                        + downDirWhole + (float)span.TotalSeconds * setting.TimeDirectionPerSecond
                       , gcdClippingColor);
                    ImGui.GetWindowDrawList().AddText(drawingLeftTop,
                        ImGui.ColorConvertFloat4ToU32(setting.GCDClippingTextColor),
                        $"{(int)span.TotalMilliseconds}ms");
                }

                last = item.EndTime;
            }
        }

        if (list != null)
        {
            foreach (TimelineItem item in list)
            {
                item.Draw(now, pos, size, TimelineLayer.General, setting);
            }
            foreach (TimelineItem item in list)
            {
                item.Draw(now, pos, size, TimelineLayer.Status, setting);
            }

            List<StatusLineItem>? status = TimelineManager.Instance?.GetStatus(endTime, out _);
            if (status != null && setting.ShowStatusLine)
            {
                foreach (StatusLineItem item in status)
                {
                    item.Draw(now, pos, size, setting);
                }
            }

            foreach (TimelineItem item in list)
            {
                item.Draw(now, pos, size, TimelineLayer.Icon, setting);
            }
        }

        if (!setting.IsRotation)
        {
            uint lineColor = ImGui.ColorConvertFloat4ToU32(setting.GridStartLineColor);

            Vector2 pt = pos + timeDirWhole - setting.TimeOffset * setting.TimeDirectionPerSecond;

            ImGui.GetWindowDrawList().AddLine(pt, pt + downDirWhole, lineColor, setting.GridStartLineWidth);
        }

        if (!setting.Locked)
        {
            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.Text(setting.Name);
        }
    }

    private static void DrawGrid(Vector2 pos, Vector2 timeDirWhole, Vector2 downDirWhole, DrawingSettings setting)
    {
        if (!setting.ShowGrid)
        {
            return;
        }

        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        float timeLineLength = timeDirWhole.Length();
        float downLineLength = downDirWhole.Length();

        uint lineColor = ImGui.ColorConvertFloat4ToU32(setting.GridLineColor);
        uint subdivisionLineColor = ImGui.ColorConvertFloat4ToU32(setting.GridSubdivisionLineColor);

        if (setting.GridDivideBySeconds)
        {
            float step = setting.SizePerSecond;
            Vector2 startPt = pos + timeDirWhole;

            for (int i = 0; i < timeLineLength / step; i++)
            {
                if (setting.GridSubdivideSeconds && setting.GridSubdivisionCount > 1)
                {
                    float subStep = step * 1f / setting.GridSubdivisionCount;
                    for (int j = 1; j < setting.GridSubdivisionCount; j++)
                    {
                        Vector2 pt = startPt + setting.RealDownDirection * subStep * j;
                        drawList.AddLine(pt, pt + downDirWhole, subdivisionLineColor, setting.GridSubdivisionLineWidth);
                    }
                }
                int time = -i + setting.TimeOffset;

                if (time != 0 || setting.IsRotation)
                {
                    drawList.AddLine(startPt, startPt + downDirWhole, lineColor, setting.GridLineWidth);
                }

                if (setting.GridShowSecondsText)
                {
                    drawList.AddText(startPt, lineColor, $" {time}s");
                }

                startPt -= setting.TimeDirectionPerSecond;
            }
        }

        lineColor = ImGui.ColorConvertFloat4ToU32(setting.GridCenterLineColor);
        if (setting.ShowGridCenterLine)
        {
            Vector2 pt = pos + downDirWhole / 2 + setting.RealDownDirection * setting.CenterOffset;
            drawList.AddLine(pt, pt + timeDirWhole, lineColor, setting.GridCenterLineWidth);
        }
    }
}
