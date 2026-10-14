using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Helpers;
using ActionTimelineReborn.Timeline;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RebornMaterial;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ActionTimelineReborn.Windows;

internal static class TimelineWindow
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar
                                        | ImGuiWindowFlags.NoCollapse
                                        | ImGuiWindowFlags.NoScrollbar
                                        | ImGuiWindowFlags.NoScrollWithMouse
                                        | ImGuiWindowFlags.NoNav
                                        | ImGuiWindowFlags.NoFocusOnAppearing;

    private const float SecondWidth = 80f;
    private const float StatusIconHeight = 26f;
    private const float MarkerUnderline = 3f;
    private const float GcdLaneHeight = 48f;
    private const float OgcdLaneHeight = 36f;
    private const float LaneGap = 6f;
    private const float IconInset = 3f;
    private const float LockLineHeight = 2f;
    private const float DamageRingWidth = 2f;
    private const float AutoAttackRadius = 2.5f;
    private const float NowCapRadius = 3.5f;
    private const float ClipLabelPadding = 3f;

    private const float FutureSeconds = 2f;

    // Status changes are moments rather than spans, so look back far enough to keep the ones still poking in from the left.
    private const float StatusLookbackSeconds = 2f;

    private const float StatusIconAspect = 0.75f;

    private const string EmptyText = "Actions show up here as you use them.";

    private static readonly int[] AxisSteps = [1, 2, 5, 10, 15, 30, 60];

    private static readonly string[] _axisLabels = new string[256];

    private static readonly M3WindowAction[] Actions =
    [
        new("##timeline_lock", FontAwesomeIcon.LockOpen, "Lock the timeline in place. Clicks pass through it until it's unlocked in the settings."),
        new("##timeline_settings", FontAwesomeIcon.Cog, "Open the settings for this timeline"),
    ];

    private sealed class Layout
    {
        public float Height;
        public float MinimumWidth;
    }

    private static readonly ConditionalWeakTable<DrawingSettings, Layout> _layouts = [];
    private static readonly Dictionary<TimelineItem, float> _gcdEnds = [];

    private readonly record struct Axis(DateTime Now, float NowX, float SecondWidth, float Left, float Right)
    {
        public float X(DateTime time)
        {
            return NowX + ((float)(time - Now).TotalSeconds * SecondWidth);
        }

        public float Width(float seconds)
        {
            return seconds * SecondWidth;
        }
    }

    private struct Hover(Vector2 mouse, bool enabled)
    {
        public TimelineItem? Item { get; private set; }
        public string? Text { get; set; }
        public Vector2 Min { get; private set; }
        public Vector2 Max { get; private set; }

        public readonly bool Found => Item != null || Text != null;

        public bool Check(Vector2 min, Vector2 max, TimelineItem? item = null)
        {
            if (!enabled || !Contains(min, max, mouse))
            {
                return false;
            }

            Item = item;
            Text = null;
            Min = min;
            Max = max;
            return true;
        }
    }

    public static void Draw(DrawingSettings setting, int index)
    {
        if (!setting.Enable)
        {
            return;
        }

        Layout layout = _layouts.GetOrCreateValue(setting);

        using M3.WindowScaleScope scale = M3.PushWindowScale(setting.Scale);
        using M3Style.Scope theme = M3Style.Push(M3Density.Compact);
        using ImRaii.ColorDisposable background = ImRaii.PushColor(ImGuiCol.WindowBg,
            setting.Locked ? setting.LockedBackgroundColor : setting.UnlockedBackgroundColor);

        if (layout.Height > 0f)
        {
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(layout.MinimumWidth, layout.Height),
                new Vector2(float.MaxValue, layout.Height));
        }

        ImGui.SetNextWindowSize(new Vector2(560, 110) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(200, 200) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);

        ImGuiWindowFlags flags = BaseFlags;
        if (setting.Locked)
        {
            flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs;
        }

        if (ImGui.Begin($"Timeline: {index}", flags))
        {
            DrawContent(setting, index, layout);
        }

        ImGui.End();
    }

    private static void DrawContent(DrawingSettings setting, int index, Layout layout)
    {
        M3Scheme s = M3.Scheme;
        float scale = M3.Scale;
        ImGuiStylePtr style = ImGui.GetStyle();
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        bool live = !setting.IsRotation;

        Vector2 origin = ImGui.GetCursorScreenPos();
        float width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        float laneGap = LaneGap * scale;

        float labelHeight;
        float labelWidth;
        using (ImRaii.PushFont(M3.LabelSmall))
        {
            labelHeight = ImGui.GetTextLineHeight();
            labelWidth = ImGui.CalcTextSize("-00s").X;
        }

        float gcdTop = origin.Y;
        float gcdBottom = gcdTop + (GcdLaneHeight * scale);
        float ogcdTop = gcdBottom + laneGap;
        float ogcdBottom = ogcdTop + (OgcdLaneHeight * scale);
        float actionsBottom = setting.ShowOGCD ? ogcdBottom : gcdBottom;
        float statusTop = actionsBottom + laneGap;
        float statusHeight = ((StatusIconHeight * setting.StatusIconScale) + MarkerUnderline + (IconInset * 2f)) * scale;
        float lanesBottom = setting.ShowStatus ? statusTop + statusHeight : actionsBottom;
        float axisTop = lanesBottom + laneGap;
        float height = axisTop + labelHeight - origin.Y;

        ImGui.Dummy(new Vector2(width, height));

        Vector2 pillSize = M3Widgets.WindowActionsSize(Actions.Length);
        layout.Height = height + (style.WindowPadding.Y * 2f);
        layout.MinimumWidth = MathF.Max(pillSize.X + (M3.Space1 * 2f), 4f * SecondWidth * scale) + (style.WindowPadding.X * 2f);

        float right = origin.X + width;
        float secondWidth = SecondWidth * scale;
        DateTime now = live ? DateTime.Now : TimelineManager.Instance?.EndTime ?? DateTime.Now;
        float future = live ? MathF.Min(FutureSeconds, width / secondWidth * 0.3f) : 0f;
        Axis axis = new(now, right - (future * secondWidth), secondWidth, origin.X, right);

        DateTime since = axis.Now.AddSeconds(-(axis.NowX - axis.Left) / secondWidth);
        DateTime lastGcdEnd = DateTime.MinValue;
        List<TimelineItem>? items = TimelineManager.Instance?.GetItems(since, out lastGcdEnd);
        List<StatusChangeItem>? statuses = setting.ShowStatus
            ? TimelineManager.Instance?.GetStatusChanges(since.AddSeconds(-StatusLookbackSeconds))
            : null;

        Vector2 windowPos = ImGui.GetWindowPos();
        Vector2 windowSize = ImGui.GetWindowSize();
        Vector2 pillTopRight = new(windowPos.X + windowSize.X - M3.Space1, windowPos.Y + M3.Space1);
        Vector2 pillMin = pillTopRight - new Vector2(pillSize.X, 0f);
        bool showPill = !setting.Locked && ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        Vector2 mouse = ImGui.GetMousePos();
        bool overWindow = setting.Locked ? ImGui.IsMouseHoveringRect(windowPos, windowPos + windowSize, false) : ImGui.IsWindowHovered();
        bool overPill = showPill && Contains(pillMin, pillMin + pillSize, mouse);
        Hover hover = new(mouse, overWindow && !ImGui.IsAnyItemActive() && !overPill);
        int drawn = 0;

        drawList.PushClipRect(origin, new Vector2(right, origin.Y + height), true);

        DrawLaneTrack(drawList, axis, gcdTop, gcdBottom);
        if (setting.ShowOGCD)
        {
            DrawLaneTrack(drawList, axis, ogcdTop, ogcdBottom);
        }

        if (setting.ShowStatus)
        {
            DrawLaneTrack(drawList, axis, statusTop, lanesBottom);
        }

        DrawAxis(drawList, axis, gcdTop, lanesBottom, axisTop, labelWidth);

        _gcdEnds.Clear();
        if (setting.ShowGCDClipping && items != null)
        {
            DrawClipping(drawList, setting, items, lastGcdEnd, axis, gcdTop, gcdBottom, ref hover);
        }

        if (statuses != null)
        {
            DrawStatusChanges(drawList, statuses, setting, axis, statusTop + (IconInset * scale), ref hover);
        }

        if (items != null)
        {
            foreach (TimelineItem item in items)
            {
                Vector2 min, max;
                bool visible = item.Type switch
                {
                    TimelineItemType.GCD => DrawGcd(drawList, item, setting, axis, gcdTop, gcdBottom, out min, out max),
                    TimelineItemType.OGCD when setting.ShowOGCD => DrawOgcd(drawList, item, setting, axis, ogcdTop, ogcdBottom, out min, out max),
                    TimelineItemType.AutoAttack when setting.ShowAutoAttack => DrawAutoAttack(drawList, item, axis, actionsBottom + (laneGap * 0.5f), out min, out max),
                    _ => Skip(out min, out max),
                };

                if (!visible)
                {
                    continue;
                }

                drawn++;
                hover.Check(min, max, item);
            }
        }

        if (live)
        {
            DrawFuture(drawList, axis, gcdTop, gcdBottom);
            if (setting.ShowOGCD)
            {
                DrawFuture(drawList, axis, ogcdTop, ogcdBottom);
            }

            if (setting.ShowStatus)
            {
                DrawFuture(drawList, axis, statusTop, lanesBottom);
            }

            DrawNowLine(drawList, axis, gcdTop, lanesBottom, axisTop);
        }

        if (drawn == 0)
        {
            DrawEmpty(drawList, axis, gcdTop, gcdBottom);
        }

        if (hover.Found)
        {
            drawList.AddRectFilled(hover.Min, hover.Max, M3.U32(s.OnSurface, M3.StateHover), M3.ShapeSmall);
        }

        drawList.PopClipRect();

        if (hover.Item != null)
        {
            DrawTooltip(hover.Item, setting, axis.Now, live);
        }
        else if (hover.Text != null)
        {
            M3Tooltip.Show(hover.Text);
        }

        if (showPill)
        {
            DrawPill(setting, index, pillTopRight);
        }

        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            Plugin.OpenTimelineSettings(index);
        }
    }

    private static bool Contains(Vector2 min, Vector2 max, Vector2 point)
    {
        return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
    }

    private static bool Skip(out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.Zero;
        return false;
    }

    private static void DrawPill(DrawingSettings setting, int index, Vector2 topRight)
    {
        int pressed = M3Widgets.WindowActions("##timeline_actions", topRight, Actions, out bool closed,
            M3.Scheme.SurfaceContainerHigh, "Hide this timeline. Turn it back on in the settings.");

        if (pressed == 0)
        {
            setting.Locked = true;
            Plugin.Settings.Save();
        }
        else if (pressed == 1)
        {
            Plugin.OpenTimelineSettings(index);
        }

        if (closed)
        {
            setting.Enable = false;
            Plugin.Settings.Save();
        }
    }

    #region Lanes
    private static void DrawLaneTrack(ImDrawListPtr drawList, in Axis axis, float top, float bottom)
    {
        drawList.AddRectFilled(new Vector2(axis.Left, top), new Vector2(axis.Right, bottom),
            M3.U32(M3.Scheme.SurfaceContainerHighest, 0.3f), M3.ShapeSmall);
    }

    private static void DrawFuture(ImDrawListPtr drawList, in Axis axis, float top, float bottom)
    {
        if (axis.NowX >= axis.Right)
        {
            return;
        }

        drawList.AddRectFilled(new Vector2(axis.NowX, top), new Vector2(axis.Right, bottom),
            M3.U32(M3.Scheme.Surface, 0.35f), M3.ShapeSmall, ImDrawFlags.RoundCornersRight);
    }

    private static bool DrawGcd(ImDrawListPtr drawList, TimelineItem item, DrawingSettings setting, in Axis axis, float top, float bottom, out Vector2 min, out Vector2 max)
    {
        M3Scheme s = M3.Scheme;
        float laneHeight = bottom - top;
        float inset = IconInset * M3.Scale;

        float x0 = axis.X(item.StartTime);
        float x1 = axis.X(item.EndTime);
        if (_gcdEnds.TryGetValue(item, out float end))
        {
            x1 = MathF.Min(x1, end);
        }

        x1 = MathF.Max(x1, x0 + laneHeight);
        min = new Vector2(x0, top);
        max = new Vector2(x1, bottom);
        if (x1 < axis.Left || x0 > axis.Right)
        {
            return false;
        }

        float rounding = M3.ShapeSmall;
        bool canceled = item.State == TimelineItemState.Canceled;
        Vector4 container = item.State switch
        {
            TimelineItemState.Casting => M3.Alpha(s.PrimaryContainer, 0.9f),
            TimelineItemState.Canceled => M3.Alpha(s.ErrorContainer, 0.75f),
            _ => M3.Alpha(s.SecondaryContainer, 0.85f),
        };
        drawList.AddRectFilled(min, max, M3.U32(container), rounding);

        if (item.CastingTime > 0f)
        {
            Vector4 accent = canceled ? s.Error : s.Primary;
            float castEnd = MathF.Min(x1, x0 + axis.Width(item.CastingTime));
            if (item.State == TimelineItemState.Casting)
            {
                FillFrom(drawList, min, max, castEnd, M3.Alpha(accent, 0.2f), rounding);
                FillFrom(drawList, min, max, Math.Clamp(axis.NowX, x0, castEnd), M3.Alpha(accent, 0.6f), rounding);
            }
            else
            {
                FillFrom(drawList, min, max, castEnd, M3.Alpha(accent, 0.4f), rounding);
            }
        }

        if (canceled)
        {
            drawList.AddRect(min, max, M3.U32(s.Error, 0.8f), rounding, ImDrawFlags.None, 1f * M3.Scale);
        }

        if (setting.ShowAnimationLock)
        {
            DrawLock(drawList, item, axis, x0, bottom - (inset * 0.5f));
        }

        float iconX = MathF.Max(x0, MathF.Min(axis.Left, x1 - laneHeight)) + inset;
        DrawIcon(drawList, item, new Vector2(iconX, top + inset), laneHeight - (inset * 2f), canceled ? M3.DisabledContent : 1f,
            DamageColor(setting, item));
        return true;
    }

    private static bool DrawOgcd(ImDrawListPtr drawList, TimelineItem item, DrawingSettings setting, in Axis axis, float top, float bottom, out Vector2 min, out Vector2 max)
    {
        float inset = IconInset * M3.Scale;
        float size = bottom - top - (inset * 2f);

        float x0 = axis.X(item.StartTime);
        float lockEnd = x0 + axis.Width(item.CastingTime + item.AnimationLockTime);
        min = new Vector2(x0, top);
        max = new Vector2(MathF.Max(x0 + size, lockEnd), bottom);
        if (max.X < axis.Left || x0 > axis.Right)
        {
            return false;
        }

        if (setting.ShowAnimationLock)
        {
            DrawLock(drawList, item, axis, x0, bottom - (inset * 0.5f));
        }

        DrawIcon(drawList, item, new Vector2(x0, top + inset), size, 1f, DamageColor(setting, item));
        return true;
    }

    private static bool DrawAutoAttack(ImDrawListPtr drawList, TimelineItem item, in Axis axis, float y, out Vector2 min, out Vector2 max)
    {
        float radius = AutoAttackRadius * M3.Scale;
        Vector2 center = new(axis.X(item.StartTime), y);

        Vector2 reach = new(radius * 2f, radius * 2f);
        min = center - reach;
        max = center + reach;
        if (center.X + radius < axis.Left || center.X - radius > axis.Right)
        {
            return false;
        }

        drawList.AddCircleFilled(center, radius, M3.U32(M3.Scheme.OnSurfaceVariant, 0.8f), 12);
        return true;
    }

    private static void DrawLock(ImDrawListPtr drawList, TimelineItem item, in Axis axis, float x0, float centerY)
    {
        if (item.AnimationLockTime <= 0f || item.State == TimelineItemState.Canceled)
        {
            return;
        }

        float half = LockLineHeight * M3.Scale * 0.5f;
        float start = x0 + axis.Width(item.CastingTime);
        float end = start + axis.Width(item.AnimationLockTime);
        drawList.AddRectFilled(new Vector2(start, centerY - half), new Vector2(end, centerY + half),
            M3.U32(M3.Scheme.Tertiary, 0.9f), half);
    }

    private static void FillFrom(ImDrawListPtr drawList, Vector2 min, Vector2 max, float end, Vector4 color, float rounding)
    {
        if (end <= min.X)
        {
            return;
        }

        ImDrawFlags flags = end >= max.X - rounding ? ImDrawFlags.RoundCornersAll : ImDrawFlags.RoundCornersLeft;
        drawList.AddRectFilled(min, new Vector2(end, max.Y), M3.U32(color), rounding, flags);
    }

    private static void DrawIcon(ImDrawListPtr drawList, TimelineItem item, Vector2 min, float size, float alpha, Vector4? ring)
    {
        Vector2 max = min + new Vector2(size, size);
        float rounding = M3ActionIcon.Rounding(size);

        IDalamudTextureWrap? texture = item.Icon == 0 ? null : DrawHelper.GetTextureFromIconId(item.Icon, item.IsHq);
        if (!M3ActionIcon.Image(drawList, texture, min, max, rounding, alpha))
        {
            M3ActionIcon.EmptySlot(drawList, min, max, rounding);
            return;
        }

        if (ring is Vector4 color)
        {
            float thickness = DamageRingWidth * M3.Scale;
            Vector2 grow = new(thickness * 0.5f);
            drawList.AddRect(min - grow, max + grow, M3.U32(color), rounding + grow.X, ImDrawFlags.None, thickness);
            return;
        }

        drawList.AddRect(min, max, M3.U32(M3.Scheme.OutlineVariant, 0.8f), rounding, ImDrawFlags.None, 1f * M3.Scale);
    }

    // ATR's damage colors, turned towards the accent so they sit with the rest of the theme.
    private static Vector4? DamageColor(DrawingSettings setting, TimelineItem item)
    {
        if (!setting.ShowDamageType)
        {
            return null;
        }

        return item.Damage switch
        {
            DamageType.Direct => M3.Harmonize(ImGuiColors.DalamudYellow),
            DamageType.Critical => M3.Harmonize(ImGuiColors.DalamudOrange),
            DamageType.CriticalDirect => M3.Harmonize(ImGuiColors.DPSRed),
            _ => null,
        };
    }
    #endregion

    #region Statuses
    // Each change to a status you applied, at the moment it happened. Changes that land together sit side by side.
    private static void DrawStatusChanges(ImDrawListPtr drawList, List<StatusChangeItem> changes, DrawingSettings setting, in Axis axis, float top, ref Hover hover)
    {
        float scale = M3.Scale;
        float iconHeight = StatusIconHeight * setting.StatusIconScale * scale;
        Vector2 size = new(iconHeight * StatusIconAspect, iconHeight);
        float gap = 1f * scale;
        float next = float.MinValue;

        foreach (StatusChangeItem change in changes)
        {
            float x = MathF.Max(axis.X(change.StartTime), next);
            next = x + size.X + gap;
            if (next < axis.Left || x > axis.Right)
            {
                continue;
            }

            DrawStatusChange(drawList, change, new Vector2(x, top), size, ref hover);
        }
    }

    // Underlined to say whether the status was gained or lost.
    private static void DrawStatusChange(ImDrawListPtr drawList, StatusChangeItem change, Vector2 min, Vector2 size, ref Hover hover)
    {
        M3Scheme s = M3.Scheme;
        float scale = M3.Scale;
        Vector2 max = min + size;
        bool gained = change.Type is StatusChangeType.Gained or StatusChangeType.StacksUp;

        IDalamudTextureWrap? texture = DrawHelper.GetTextureFromIconId(change.Icon);
        if (texture != null)
        {
            drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, M3.U32(Vector4.One, gained ? 1f : 0.6f));
        }

        float thickness = 2f * scale;
        float lineY = max.Y + (1f * scale) + (thickness * 0.5f);
        drawList.AddLine(new Vector2(min.X, lineY), new Vector2(max.X, lineY), M3.U32(gained ? s.Success : s.Error), thickness);

        if (hover.Check(min, new Vector2(max.X, lineY + (thickness * 0.5f))))
        {
            hover.Text = Describe(change);
        }
    }

    private static string Describe(StatusChangeItem change)
    {
        string name = string.IsNullOrEmpty(change.Name) ? "an unknown status" : change.Name;
        string stacks = change.Stacks == 1 ? "1 stack" : $"{change.Stacks} stacks";
        return change.Type switch
        {
            StatusChangeType.Gained => $"Gained {name}",
            StatusChangeType.Lost => $"Lost {name}",
            StatusChangeType.StacksUp => $"{name} up to {stacks}",
            _ => $"{name} down to {stacks}",
        };
    }
    #endregion

    #region GCD clipping
    private static void DrawClipping(ImDrawListPtr drawList, DrawingSettings setting, List<TimelineItem> items, DateTime last, in Axis axis, float top, float bottom, ref Hover hover)
    {
        M3Scheme s = M3.Scheme;
        TimeSpan threshold = TimeSpan.FromSeconds(MathF.Max(setting.GCDClippingThreshold, 0.001f));
        TimeSpan longest = TimeSpan.FromSeconds(setting.GCDClippingMaxTime);
        float laneHeight = bottom - top;
        float padding = ClipLabelPadding * M3.Scale;
        TimelineItem? previous = null;

        using ImRaii.FontDisposable font = ImRaii.PushFont(M3.LabelSmall);
        foreach (TimelineItem item in items)
        {
            if (item.Type != TimelineItemType.GCD)
            {
                continue;
            }

            TimeSpan span = item.StartTime - last;
            if (last != DateTime.MinValue && span >= threshold && span < longest)
            {
                int milliseconds = (int)span.TotalMilliseconds;
                string label = $"{milliseconds}ms";
                Vector2 labelSize = ImGui.CalcTextSize(label);
                float labelRoom = labelSize.X + (padding * 2f);

                Vector2 min = new(axis.X(last), top);
                Vector2 max = new(axis.X(item.StartTime), bottom);

                // Too short for its label, so take the room from the end of the GCD before it, short of that GCD's icon.
                float start = max.X - labelRoom;
                float floor = previous == null ? float.MinValue : axis.X(previous.StartTime) + laneHeight;
                bool fits = start >= MathF.Min(min.X, floor);
                float edge = MathF.Max(start, floor);
                if (edge < min.X)
                {
                    min.X = edge;
                    if (previous != null)
                    {
                        _gcdEnds[previous] = edge;
                    }
                }

                drawList.AddRectFilled(min, max, M3.U32(s.Error, 0.18f), M3.ShapeSmall);

                if (fits)
                {
                    drawList.AddText(new Vector2((min.X + max.X - labelSize.X) * 0.5f, (min.Y + max.Y - labelSize.Y) * 0.5f),
                        M3.U32(s.Error), label);
                }

                if (hover.Check(min, max))
                {
                    hover.Text = $"GCD clipped by {milliseconds}ms";
                }
            }

            last = item.EndTime;
            previous = item;
        }
    }
    #endregion

    #region Axis
    private static void DrawAxis(ImDrawListPtr drawList, in Axis axis, float top, float linesBottom, float labelTop, float labelWidth)
    {
        if (axis.SecondWidth <= 0f)
        {
            return;
        }

        M3Scheme s = M3.Scheme;
        int step = AxisSteps[^1];
        foreach (int candidate in AxisSteps)
        {
            if (candidate * axis.SecondWidth >= labelWidth + M3.Space2)
            {
                step = candidate;
                break;
            }
        }

        uint lineColor = M3.U32(s.OutlineVariant, 0.45f);
        uint labelColor = M3.U32(s.OnSurfaceVariant, 0.85f);
        float thickness = 1f * M3.Scale;

        using ImRaii.FontDisposable font = ImRaii.PushFont(M3.LabelSmall);
        for (int seconds = step; ; seconds += step)
        {
            float x = axis.NowX - (seconds * axis.SecondWidth);
            if (x < axis.Left)
            {
                break;
            }

            drawList.AddLine(new Vector2(x, top), new Vector2(x, linesBottom), lineColor, thickness);

            string label = AxisLabel(seconds);
            float labelX = x - (ImGui.CalcTextSize(label).X * 0.5f);
            if (labelX >= axis.Left)
            {
                drawList.AddText(new Vector2(labelX, labelTop), labelColor, label);
            }
        }
    }

    private static string AxisLabel(int seconds)
    {
        return seconds < _axisLabels.Length
            ? _axisLabels[seconds] ??= $"-{seconds}s"
            : $"-{seconds}s";
    }

    private static void DrawNowLine(ImDrawListPtr drawList, in Axis axis, float top, float bottom, float labelTop)
    {
        uint color = M3.U32(M3.Scheme.Primary);
        float scale = M3.Scale;
        float cap = NowCapRadius * scale;

        drawList.AddLine(new Vector2(axis.NowX, top + cap), new Vector2(axis.NowX, bottom), color, 2f * scale);
        drawList.AddCircleFilled(new Vector2(axis.NowX, top + cap), cap, color, 12);

        using ImRaii.FontDisposable font = ImRaii.PushFont(M3.LabelSmall);
        const string label = "now";
        float labelWidth = ImGui.CalcTextSize(label).X;
        float labelX = MathF.Min(axis.NowX - (labelWidth * 0.5f), axis.Right - labelWidth);
        drawList.AddText(new Vector2(labelX, labelTop), color, label);
    }

    private static void DrawEmpty(ImDrawListPtr drawList, in Axis axis, float top, float bottom)
    {
        Vector2 size = ImGui.CalcTextSize(EmptyText);
        float room = axis.NowX - axis.Left - (M3.Space3 * 2f);
        if (size.X > room)
        {
            return;
        }

        Vector2 position = new(((axis.Left + axis.NowX) - size.X) * 0.5f, ((top + bottom) - size.Y) * 0.5f);
        drawList.AddText(position, M3.U32(M3.Scheme.OnSurfaceVariant, 0.7f), EmptyText);
    }
    #endregion

    #region Tooltip
    private static void DrawTooltip(TimelineItem item, DrawingSettings setting, DateTime now, bool live)
    {
        using ImRaii.TooltipDisposable tooltip = ImRaii.Tooltip();
        ImGui.TextUnformatted(string.IsNullOrEmpty(item.Name) ? "Unknown action" : item.Name);

        using ImRaii.ColorDisposable color = ImRaii.PushColor(ImGuiCol.Text, M3.Scheme.OnSurfaceVariant);
        ImGui.TextUnformatted(Describe(item));

        if (setting.ShowDamageType && item.Damage != DamageType.None)
        {
            ImGui.TextUnformatted(item.Damage switch
            {
                DamageType.Direct => "Direct hit",
                DamageType.Critical => "Critical hit",
                _ => "Critical direct hit",
            });
        }

        if (item.State == TimelineItemState.Casting)
        {
            ImGui.TextUnformatted("Casting now");
        }
        else if (live)
        {
            ImGui.TextUnformatted($"{(now - item.StartTime).TotalSeconds:F1}s ago");
        }
    }

    private static string Describe(TimelineItem item)
    {
        string text = item.Type switch
        {
            TimelineItemType.GCD => "GCD",
            TimelineItemType.OGCD => "oGCD",
            _ => "Auto attack",
        };

        if (item.State == TimelineItemState.Canceled)
        {
            return $"{text} - Canceled after {item.CastingTime:F2}s";
        }

        if (item.CastingTime > 0f)
        {
            text += $" - Cast {item.CastingTime:F2}s";
        }

        if (item.GCDTime > 0f)
        {
            text += $" - Recast {item.GCDTime:F2}s";
        }

        if (item.AnimationLockTime > 0f)
        {
            text += $" - Lock {item.AnimationLockTime:F2}s";
        }

        return text;
    }
    #endregion
}
