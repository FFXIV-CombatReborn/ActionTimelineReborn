using ActionTimelineReborn.Timeline;
using ActionTimelineReborn.Configurations;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
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
        if (!setting.Enable || string.IsNullOrEmpty(setting.Name)) return;

        var flag = _baseFlags;
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
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();

        var now = setting.IsRotation ? (TimelineManager.Instance?.EndTime ?? DateTime.Now - TimeSpan.FromSeconds(setting.TimeOffset)) : DateTime.Now;

        var endTime = now - TimeSpan.FromSeconds((setting.IsHorizonal ? size.X : size.Y )/ setting.SizePerSecond - setting.TimeOffset);

        var last = now;
        var list = TimelineManager.Instance?.GetItems(endTime, out last);

        var timeDirWhole = setting.IsHorizonal ? size.X * Vector2.UnitX : size.Y * Vector2.UnitY;
        var downDirWhole = setting.IsHorizonal ? size.Y * Vector2.UnitY : size.X * Vector2.UnitX;

        DrawGrid(pos, timeDirWhole, downDirWhole, setting);

        if (setting.ShowGCDClipping && list != null) //Clipping
        {
            var gcdClippingColor = ImGui.ColorConvertFloat4ToU32(setting.GCDClippingColor);
            var threshold = TimeSpan.FromSeconds(setting.GCDClippingThreshold);
            var max = TimeSpan.FromSeconds(setting.GCDClippingMaxTime);

            foreach (var item in list)
            {
                if (item.Type != TimelineItemType.GCD) continue;

                var start = item.StartTime;
                var span = start - last;

                if (last != DateTime.MinValue && span >= threshold && span < max)
                {
                    var drawingLeftTop = pos + timeDirWhole
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
            foreach (var item in list)
            {
                item.Draw(now, pos, size, TimelineLayer.General, setting);
            }
            foreach (var item in list)
            {
                item.Draw(now, pos, size, TimelineLayer.Status, setting);
            }

            var status = TimelineManager.Instance?.GetStatus(endTime, out _);
            if (status != null && setting.ShowStatusLine)
            {
                foreach (var item in status)
                {
                    item.Draw(now, pos, size, setting);
                }
            }

            foreach (var item in list)
            {
                item.Draw(now, pos, size, TimelineLayer.Icon, setting);
            }
        }

        if (!setting.IsRotation)
        {
            uint lineColor = ImGui.ColorConvertFloat4ToU32(setting.GridStartLineColor);

            var pt = pos + timeDirWhole - setting.TimeOffset * setting.TimeDirectionPerSecond;

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
        if (!setting.ShowGrid) return;

        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        var timeLineLength = timeDirWhole.Length();
        var downLineLength = downDirWhole.Length();

        uint lineColor = ImGui.ColorConvertFloat4ToU32(setting.GridLineColor);
        uint subdivisionLineColor = ImGui.ColorConvertFloat4ToU32(setting.GridSubdivisionLineColor);

        if (setting.GridDivideBySeconds)
        {
            var step = setting.SizePerSecond;
            var startPt = pos + timeDirWhole;

            for (int i = 0; i < timeLineLength / step; i++)
            {
                if (setting.GridSubdivideSeconds && setting.GridSubdivisionCount > 1)
                {
                    float subStep = step * 1f / setting.GridSubdivisionCount;
                    for (int j = 1; j < setting.GridSubdivisionCount; j++)
                    {
                        var pt = startPt + setting.RealDownDirection * subStep * j;
                        drawList.AddLine(pt, pt + downDirWhole, subdivisionLineColor, setting.GridSubdivisionLineWidth);
                    }
                }
                var time = -i + setting.TimeOffset;

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
            var pt = pos + downDirWhole / 2 + setting.RealDownDirection * setting.CenterOffset;
            drawList.AddLine(pt, pt + timeDirWhole, lineColor, setting.GridCenterLineWidth);
        }
    }
}
