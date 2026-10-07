using ActionTimelineReborn.Configurations;
using ActionTimelineReborn.Helpers;
using ActionTimelineReborn.Timeline;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.Commands;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using RebornMaterial;
using System.Numerics;

namespace ActionTimelineReborn.Windows
{
    public class SettingsWindow : Window
    {
        private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse;

        private static readonly Vector2 DefaultSize = new(720f, 560f);

        private static readonly WindowSizeConstraints DefaultSizeConstraints = new()
        {
            MinimumSize = new Vector2(460f, 360f),
            MaximumSize = new Vector2(5000f, 5000f),
        };

        private const string LogoResource = "ActionTimelineReborn.Resources.ATR_Icon.png";
        private const string KofiUrl = "https://ko-fi.com/ltscombatreborn";

        private static readonly M3WindowAction[] _windowActions =
        [
            new("##window_kofi", FontAwesomeIcon.MugHot, "Support the developer on Ko-fi"),
        ];

        private const string GeneralPage = "general";
        private const string StatusesPage = "statuses";
        private const string AppearancePage = "appearance";
        private const string HelpPage = "help";
        private const string AddTimelineItem = "add_timeline";
        private const string TimelinePagePrefix = "timeline_";

        private static readonly string VersionLabel = $"v{typeof(SettingsWindow).Assembly.GetName().Version}";

        private static readonly DrawingSettings _defaults = new();
        private static readonly Vector4 _koFiColor = new(1f, 0.357f, 0.369f, 1f);

        private static Settings Settings => Plugin.Settings;

        private readonly M3WindowFold _fold = new();
        private int _shownActions = _windowActions.Length;

        private M3Style.Scope _theme;
        private string _page = GeneralPage;
        private int _newStatusId;

        private static M3WindowBrand Brand => new(GetLogo(), "ATR", FontAwesomeIcon.Stream);

        public SettingsWindow() : base("ActionTimelineReborn###atr_settings", BaseFlags)
        {
            SizeCondition = ImGuiCond.FirstUseEver;
            Size = DefaultSize;
            SizeConstraints = DefaultSizeConstraints;
            RespectCloseHotkey = true;

            AllowPinning = false;
            AllowClickthrough = false;
        }

        private static IDalamudTextureWrap? GetLogo()
        {
            return Svc.Texture.GetFromManifestResource(typeof(SettingsWindow).Assembly, LogoResource)
                .TryGetWrap(out IDalamudTextureWrap? logo, out _) ? logo : null;
        }

        internal void Open(int? timeline = null)
        {
            if (timeline is int index)
            {
                _page = TimelinePagePrefix + index;
            }

            IsOpen = true;
            _fold.Restore();
        }

        public override void OnOpen()
        {
            IsPinned = false;
            IsClickthrough = false;
            base.OnOpen();
        }

        public override void OnClose()
        {
            Settings.Save();

            _fold.Reset();
            base.OnClose();
        }

        public override void PreDraw()
        {
            _theme = M3Style.Push();

            Flags = BaseFlags;
            if (_fold.Prepare(this, _shownActions, Brand))
            {
                Position = null;
                Size = DefaultSize;
                SizeCondition = ImGuiCond.FirstUseEver;
                SizeConstraints = DefaultSizeConstraints;
            }
        }

        public override void PostDraw()
        {
            _fold.PopStyle();
            _theme.Dispose();
            _theme = default;
        }

        public override void Draw()
        {
            _fold.BeginDraw();

            if (Settings.TimelineSettings.Count == 0)
            {
                Settings.TimelineSettings.Add(new DrawingSettings());
            }

            float folded = _fold.Amount;
            if (folded < 1f)
            {
                using ImRaii.StyleDisposable alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (1f - MathF.Min(1f, folded * 1.4f)));
                DrawBackdrop(_fold.Rounding);

                (Vector2 openPos, Vector2 openSize) = _fold.OpenRect();
                DrawContent(openPos, openSize);
            }

            DrawWindowBar();
        }

        #region Window
        private void DrawContent(Vector2 openPos, Vector2 openSize)
        {
            Vector2 padding = _fold.OpenPadding;
            ImGui.SetCursorScreenPos(openPos + padding);
            using ImRaii.ChildDisposable content = ImRaii.Child("##atr_window_content", Vector2.Max(Vector2.One, openSize - (padding * 2f)), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground);
            if (!content)
            {
                return;
            }

            float scale = M3.Scale;
            Vector2 available = ImGui.GetContentRegionAvail();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float navWidth = (available.X >= 620f * scale ? 210f : 88f) * scale;

            Vector2 origin = ImGui.GetCursorScreenPos();
            float dividerX = origin.X + navWidth + (spacing * 0.5f);
            ImGui.GetWindowDrawList().AddLine(
                new Vector2(dividerX, origin.Y),
                new Vector2(dividerX, origin.Y + available.Y),
                M3.U32(M3.Scheme.OutlineVariant, 0.45f), 1f * scale);

            DrawNavigation(navWidth);
            ImGui.SameLine(0f, spacing);
            DrawBody();

            // Last, so it draws on top.
            M3Snackbar.Draw(new Vector2(origin.X + navWidth + spacing, origin.Y), origin + available);
        }

        private static void DrawBackdrop(float rounding)
        {
            M3Scheme s = M3.Scheme;
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            Vector2 size = ImGui.GetWindowSize();
            Vector4 top = M3.Alpha(s.SurfaceContainer, 0.65f);

            Vector2 min = pos;
            Vector2 max = new(pos.X + size.X, pos.Y + MathF.Min(200f * M3.Scale, size.Y));
            rounding = MathF.Min(rounding, (max.Y - min.Y) * 0.5f);

            // Multi-color rects can't be rounded, so a solid strip draws the rounded top corners.
            drawList.PushClipRect(pos, pos + size, false);
            if (rounding > 0f)
            {
                drawList.AddRectFilled(min, new Vector2(max.X, min.Y + rounding), M3.U32(top), rounding, ImDrawFlags.RoundCornersTop);
            }

            M3Draw.VerticalGradient(drawList, new Vector2(min.X, min.Y + rounding), max, top, M3.Alpha(s.Surface, 0f));
            drawList.PopClipRect();
        }

        private void DrawWindowBar()
        {
            int first = _windowActions.Length - _shownActions;
            int pressed = _fold.DrawBar("##atr_window_actions", _windowActions.AsSpan(first), Brand, out bool closed,
                M3.Scheme.SurfaceContainerHigh);

            // Indices follow _windowActions.
            if (pressed >= 0 && first + pressed == 0)
            {
                Util.OpenLink(KofiUrl);
            }

            if (closed)
            {
                IsOpen = false;
            }
        }

        private void DrawTopAppBar(string title, string subtitle)
        {
            M3Scheme s = M3.Scheme;
            float scale = M3.Scale;
            float fullWidth = MathF.Max(64f * scale, ImGui.GetContentRegionAvail().X);
            M3WindowBrand brand = Brand;

            int shown = _windowActions.Length;
            while (shown > 0 && M3Widgets.WindowActionsSize(shown, brand, 0f).X + (96f * scale) > fullWidth)
            {
                shown--;
            }

            Vector2 barSize = M3Widgets.WindowActionsSize(shown, brand, 0f);
            float width = MathF.Max(32f * scale, fullWidth - barSize.X - (12f * scale));

            float padTop = 6f * scale;
            float padBottom = 8f * scale;
            float lineGap = 2f * scale;

            string clippedTitle;
            Vector2 titleSize;
            using (ImRaii.PushFont(M3.HeadlineSmall))
            {
                clippedTitle = M3Navigation.Truncate(title, width);
                titleSize = ImGui.CalcTextSize(clippedTitle);
            }

            string clippedSubtitle;
            Vector2 subtitleSize;
            using (ImRaii.PushFont(M3.LabelSmall))
            {
                clippedSubtitle = M3Navigation.Truncate(subtitle, width);
                subtitleSize = ImGui.CalcTextSize(clippedSubtitle);
            }

            float contentHeight = titleSize.Y + lineGap + subtitleSize.Y;
            float height = MathF.Max(52f * scale, padTop + contentHeight + padBottom);
            ImGui.Dummy(new Vector2(fullWidth, height));

            _shownActions = shown;
            _fold.BarTop = (height - barSize.Y) * 0.5f;

            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float textTop = min.Y + ((height - contentHeight) * 0.5f);

            using (ImRaii.PushFont(M3.HeadlineSmall))
            {
                drawList.AddText(new Vector2(min.X, textTop), M3.U32(s.OnSurface, 0.98f), clippedTitle);
            }

            using (ImRaii.PushFont(M3.LabelSmall))
            {
                drawList.AddText(new Vector2(min.X, textTop + titleSize.Y + lineGap), M3.U32(s.OnSurfaceVariant, 0.88f), clippedSubtitle);
            }

            ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
            drawList.AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), M3.U32(s.OutlineVariant, 0.5f), 1f * scale);
            ImGui.Dummy(new Vector2(fullWidth, M3.Space1));
        }
        #endregion

        #region Navigation
        private void DrawNavigation(float width)
        {
            using ImRaii.ChildDisposable nav = ImRaii.Child("##atr_nav", new Vector2(width, -1f), false, ImGuiWindowFlags.NoScrollbar);
            if (!nav)
            {
                return;
            }

            bool expanded = ImGui.GetContentRegionAvail().X >= M3Navigation.DrawerBreakpoint * M3.Scale;

            DrawBrand(expanded);
            ImGui.Dummy(new Vector2(0f, M3.Space2));

            float footer = expanded ? M3Widgets.PillSize(VersionLabel, FontAwesomeIcon.CodeBranch).Y + M3.Space2 : 0f;
            using (ImRaii.ChildDisposable list = ImRaii.Child("##atr_nav_list", new Vector2(-1f, MathF.Max(1f, ImGui.GetContentRegionAvail().Y - footer)), false))
            {
                if (list)
                {
                    DrawNavigationItems(expanded);
                }
            }

            if (expanded)
            {
                DrawVersionFooter();
            }
        }

        private void DrawBrand(bool expanded)
        {
            M3Scheme s = M3.Scheme;
            float scale = M3.Scale;
            float width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
            float badge = 40f * scale;
            float height = badge + (8f * scale);

            bool pressed = ImGui.InvisibleButton("##atr_brand", new Vector2(width, height));
            bool hovered = ImGui.IsItemHovered();
            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

            if (hovered)
            {
                drawList.AddRectFilled(min, max, M3.U32(s.OnSurface, M3.StateHover), M3.ShapeMedium);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            Vector2 center = expanded
                ? new Vector2(min.X + (8f * scale) + (badge * 0.5f), min.Y + (height * 0.5f))
                : new Vector2(min.X + (width * 0.5f), min.Y + (height * 0.5f));
            Vector2 half = new(badge * 0.5f);
            if (!M3ActionIcon.Image(drawList, GetLogo(), center - half, center + half, M3.ShapeSmall))
            {
                drawList.AddRectFilled(center - half, center + half, M3.U32(s.PrimaryContainer), M3.ShapeSmall);
                M3Draw.IconCentered(drawList, FontAwesomeIcon.Stream, center - half, center + half, s.OnPrimaryContainer);
            }

            if (expanded)
            {
                const string tagline = "REBORN";
                float textX = center.X + (badge * 0.5f) + (10f * scale);
                float textWidth = MathF.Max(16f * scale, max.X - textX - (4f * scale));
                float gap = 1f * scale;

                string name;
                Vector2 nameSize;
                using (ImRaii.PushFont(M3.TitleMedium))
                {
                    name = M3Navigation.Truncate("Action Timeline", textWidth);
                    nameSize = ImGui.CalcTextSize(name);
                }

                Vector2 taglineSize;
                using (ImRaii.PushFont(M3.LabelSmall))
                {
                    taglineSize = ImGui.CalcTextSize(tagline);
                }

                float textTop = min.Y + ((height - nameSize.Y - gap - taglineSize.Y) * 0.5f);
                using (ImRaii.PushFont(M3.TitleMedium))
                {
                    drawList.AddText(new Vector2(textX, textTop), M3.U32(s.OnSurface, 0.98f), name);
                }

                using (ImRaii.PushFont(M3.LabelSmall))
                {
                    drawList.AddText(new Vector2(textX, textTop + nameSize.Y + gap), M3.U32(s.Primary), tagline);
                }
            }

            M3Draw.FocusRing(min, max, M3.ShapeMedium);

            if (hovered)
            {
                M3Tooltip.Show("Help and links");
            }

            if (pressed)
            {
                _page = HelpPage;
            }
        }

        private void DrawNavigationItems(bool expanded)
        {
            List<M3NavItem> items =
            [
                new(GeneralPage, "General", FontAwesomeIcon.Cog, _page == GeneralPage, Section: "Plugin"),
                new(StatusesPage, "Statuses", FontAwesomeIcon.Tags, _page == StatusesPage, Section: "Plugin"),
                new(AppearancePage, "Appearance", FontAwesomeIcon.Palette, _page == AppearancePage, Section: "Plugin"),
            ];

            for (int i = 0; i < Settings.TimelineSettings.Count; i++)
            {
                DrawingSettings setting = Settings.TimelineSettings[i];
                string id = TimelinePagePrefix + i;
                items.Add(new(id, TimelineName(setting, i), setting.Enable ? FontAwesomeIcon.Stream : FontAwesomeIcon.EyeSlash,
                    _page == id, setting.Enable ? null : "Turned off", Section: "Timelines"));
            }

            items.Add(new(AddTimelineItem, "Add timeline", FontAwesomeIcon.Plus, false, "Add another timeline window",
                SeparatorAfter: true, Section: "Timelines"));
            items.Add(new(HelpPage, "Help", FontAwesomeIcon.QuestionCircle, _page == HelpPage));

            string? clicked = M3Navigation.Draw("atr_nav", items, expanded);
            if (clicked == AddTimelineItem)
            {
                Settings.TimelineSettings.Add(new DrawingSettings()
                {
                    Name = (Settings.TimelineSettings.Count + 1).ToString(),
                });
                _page = TimelinePagePrefix + (Settings.TimelineSettings.Count - 1);
            }
            else if (clicked != null)
            {
                _page = clicked;
            }
        }

        private static void DrawVersionFooter()
        {
            Vector2 size = M3Widgets.PillSize(VersionLabel, FontAwesomeIcon.CodeBranch);
            float bottom = ImGui.GetCursorPosY() + ImGui.GetContentRegionAvail().Y - size.Y;
            ImGui.SetCursorPosY(MathF.Max(ImGui.GetCursorPosY() + M3.Space2, bottom));
            M3Widgets.Pill("##atr_version", VersionLabel, M3.Scheme.OnSurfaceVariant, FontAwesomeIcon.CodeBranch, "Installed version");
        }
        #endregion

        #region Pages
        private void DrawBody()
        {
            using ImRaii.ChildDisposable body = ImRaii.Child("##atr_body", new Vector2(-1f, -1f), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            if (!body)
            {
                return;
            }

            bool isTimeline = TryGetTimeline(out int index);
            if (!isTimeline && _page.StartsWith(TimelinePagePrefix, StringComparison.Ordinal))
            {
                _page = GeneralPage;
            }

            (string title, string subtitle) = isTimeline ? TimelineHeading(index) : _page switch
            {
                StatusesPage => ("Statuses", "Choose which statuses show up on the timelines"),
                AppearancePage => ("Appearance", "How the plugin's windows look"),
                HelpPage => ("Help", "Commands, tips and links"),
                _ => ("General", "Settings shared by every timeline"),
            };
            DrawTopAppBar(title, subtitle);

            // One child per page, so each page keeps its own scroll position.
            using ImRaii.ChildDisposable page = ImRaii.Child($"##atr_page_{_page}", new Vector2(-1f, -1f), false);
            if (!page)
            {
                return;
            }

            if (isTimeline)
            {
                DrawTimelinePage(Settings.TimelineSettings[index], index);
            }
            else
            {
                switch (_page)
                {
                    case StatusesPage:
                        DrawStatusesPage();
                        break;

                    case AppearancePage:
                        DrawAppearancePage();
                        break;

                    case HelpPage:
                        DrawHelpPage();
                        break;

                    default:
                        DrawGeneralPage();
                        break;
                }
            }

            ImGui.Dummy(new Vector2(0f, M3.Space2));
        }

        private bool TryGetTimeline(out int index)
        {
            index = -1;
            return _page.StartsWith(TimelinePagePrefix, StringComparison.Ordinal)
                && int.TryParse(_page.AsSpan(TimelinePagePrefix.Length), out index)
                && index >= 0 && index < Settings.TimelineSettings.Count;
        }

        private (string Title, string Subtitle) TimelineHeading(int index)
        {
            DrawingSettings setting = Settings.TimelineSettings[index];
            string subtitle = setting.Enable
                ? $"Timeline {index + 1} of {Settings.TimelineSettings.Count}"
                : "This timeline is turned off";
            return (TimelineName(setting, index), subtitle);
        }

        private static string TimelineName(DrawingSettings setting, int index)
        {
            return string.IsNullOrEmpty(setting.Name) ? $"Timeline {index + 1}" : setting.Name;
        }
        #endregion

        #region General
        private void DrawGeneralPage()
        {
            using (M3Card.Begin("general_visibility", "Visibility", FontAwesomeIcon.Eye))
            {
                M3Widgets.RowSwitch("Only show in duties", ref Settings.ShowTimelineOnlyInDuty);
                M3Widgets.RowSwitch("Only show in combat", ref Settings.ShowTimelineOnlyInCombat);
                M3Widgets.RowSwitch("Hide in cutscenes", ref Settings.HideTimelineInCutscene);
                M3Widgets.RowSwitch("Hide during quest events", ref Settings.HideTimelineInQuestEvent);
            }

            using (M3Card.Begin("general_recording", "Recording", FontAwesomeIcon.Database))
            {
                M3Widgets.RowSwitch("Record actions", ref Settings.Record, "Turn off to stop adding new actions to the timelines.");
                M3Widgets.RowSwitch("Record target statuses", ref Settings.RecordTargetStatus,
                    "Also show statuses your actions apply to other targets, not only to you.");

                M3RowInfo row = M3SettingRow.Begin("Clear timeline data", "Removes everything recorded so far and frees the memory it used.",
                    new Vector2(M3Widgets.ButtonWidth(FontAwesomeIcon.TrashAlt, "Clear"), M3Widgets.ButtonHeight));
                if (M3Widgets.HoldButton("##clear_data", "Clear", icon: FontAwesomeIcon.TrashAlt, tooltip: "Hold to clear all timeline data"))
                {
                    TimelineManager.Instance?.ClearAllData();
                    M3Snackbar.Show("All timeline data cleared.");
                }

                M3SettingRow.End(row);
            }

            using (M3Card.Begin("general_chat", "Chat", FontAwesomeIcon.CommentDots))
            {
                M3Widgets.RowSwitch("Print GCD clipping", ref Settings.PrintClipping,
                    "Prints the delay before each late GCD to your chat log.");

                using (M3SubGroup.Begin())
                {
                    M3Widgets.RowNumber("Shortest delay", ref Settings.PrintClippingMin, step: 10, min: 0, max: Settings.PrintClippingMax,
                        unit: "ms", supporting: "Shorter delays aren't printed.", enabled: Settings.PrintClipping);
                    M3Widgets.RowNumber("Longest delay", ref Settings.PrintClippingMax, step: 10, min: Settings.PrintClippingMin, max: 60000,
                        unit: "ms", supporting: "Longer delays aren't printed.", enabled: Settings.PrintClipping);
                }
            }
        }
        #endregion

        #region Statuses
        private static Vector2 StatusIconSize => new Vector2(24f, 32f) * M3.Scale;

        private void DrawStatusesPage()
        {
            using (M3Card.Begin("statuses_recorded", "Recorded statuses", FontAwesomeIcon.History,
                subtitle: "Statuses seen since the data was last cleared. Click one to stop or resume recording it."))
            {
                if (TimelineManager.ShowedStatusId.Count == 0)
                {
                    M3Text.Draw("Nothing yet. Statuses appear here once you gain or lose them.", M3TextStyle.Muted, wrap: true);
                }
                else
                {
                    ushort toggleId = 0;
                    M3Flow flow = new(M3.Space1);
                    foreach (ushort statusId in TimelineManager.ShowedStatusId)
                    {
                        bool hidden = Settings.HideStatusIds.Contains(statusId);
                        flow.Next(StatusIconSize.X);
                        if (StatusIcon($"##recorded_{statusId}", GetStatus(statusId)?.Icon ?? 0, hidden, out bool hovered))
                        {
                            toggleId = statusId;
                        }

                        if (hovered)
                        {
                            M3Tooltip.Show($"{StatusLabel(statusId)}\n{(hidden ? "Hidden. Click to record it again." : "Click to stop recording it.")}");
                        }
                    }

                    if (toggleId != 0)
                    {
                        ToggleHidden(toggleId);
                    }
                }
            }

            using (M3Card.Begin("statuses_hidden", "Hidden statuses", FontAwesomeIcon.EyeSlash, subtitle: "These are never recorded."))
            {
                if (Settings.HideStatusIds.Count == 0)
                {
                    M3Text.Draw("Nothing hidden.", M3TextStyle.Muted);
                }
                else
                {
                    ushort removeId = 0;
                    M3Flow flow = new(M3.Space1);
                    foreach (ushort statusId in Settings.HideStatusIds)
                    {
                        string label = StatusLabel(statusId);
                        flow.Next(M3Widgets.InputChipWidth(label));
                        M3Widgets.InputChip($"##hidden_{statusId}", label, out bool removed);
                        if (removed)
                        {
                            removeId = statusId;
                        }
                    }

                    if (removeId != 0)
                    {
                        ToggleHidden(removeId);
                    }
                }

                ImGui.Dummy(new Vector2(0f, M3.Space1));
                DrawHideByIdRow();
            }
        }

        private void DrawHideByIdRow()
        {
            ushort statusId = (ushort)Math.Clamp(_newStatusId, 0, ushort.MaxValue);
            Status? status = statusId == 0 ? null : GetStatus(statusId);
            bool alreadyHidden = Settings.HideStatusIds.Contains(statusId);

            string supporting = status == null ? "Enter the ID of a status to hide."
                : alreadyHidden ? $"{StatusLabel(statusId)} is already hidden."
                : StatusLabel(statusId);

            float fieldWidth = M3Widgets.NumberFieldWidth();
            float buttonWidth = M3Widgets.ButtonWidth(FontAwesomeIcon.EyeSlash, "Hide");
            M3RowInfo row = M3SettingRow.Begin("Hide by ID", supporting,
                new Vector2(fieldWidth + M3.Space2 + buttonWidth, M3Widgets.ButtonHeight));

            M3Widgets.NumberField("##hide_id", ref _newStatusId, fieldWidth, min: 0, max: ushort.MaxValue);
            ImGui.SameLine(0f, M3.Space2);
            if (M3Widgets.Button("##hide_add", "Hide", M3ButtonStyle.Tonal, FontAwesomeIcon.EyeSlash, enabled: status != null && !alreadyHidden))
            {
                Settings.HideStatusIds.Add(statusId);
                _newStatusId = 0;
            }

            M3SettingRow.End(row);
        }

        private static void ToggleHidden(ushort statusId)
        {
            string label = StatusLabel(statusId);
            if (Settings.HideStatusIds.Remove(statusId))
            {
                M3Snackbar.Show($"Recording {label} again.", "Undo", () => Settings.HideStatusIds.Add(statusId));
            }
            else
            {
                Settings.HideStatusIds.Add(statusId);
                M3Snackbar.Show($"Stopped recording {label}.", "Undo", () => Settings.HideStatusIds.Remove(statusId));
            }
        }

        private static bool StatusIcon(string id, uint iconId, bool dimmed, out bool hovered)
        {
            bool pressed = ImGui.InvisibleButton(id, StatusIconSize);
            hovered = ImGui.IsItemHovered();
            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

            IDalamudTextureWrap? texture = DrawHelper.GetTextureFromIconId(iconId);
            if (texture != null)
            {
                drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, M3.U32(Vector4.One, dimmed ? M3.DisabledContent : 1f));
            }

            if (hovered)
            {
                drawList.AddRectFilled(min, max, M3.U32(M3.Scheme.OnSurface, M3.StateHover), M3.ShapeExtraSmall);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            M3Draw.FocusRing(min, max, M3.ShapeExtraSmall);
            return pressed;
        }

        private static Status? GetStatus(ushort statusId)
        {
            return Svc.Data.GetExcelSheet<Status>()?.GetRowOrDefault(statusId);
        }

        private static string StatusLabel(ushort statusId)
        {
            string? name = GetStatus(statusId)?.Name.ToString();
            return string.IsNullOrEmpty(name) ? $"#{statusId}" : $"{name} ({statusId})";
        }
        #endregion

        #region Appearance
        private void DrawAppearancePage()
        {
            using (M3Card.Begin("appearance_theme", "Theme", FontAwesomeIcon.Palette, subtitle: "Applies to this window and the timelines."))
            {
                Vector4 accent = Settings.UiAccentColor;
                if (M3Widgets.RowColor("Accent color", ref accent, M3.DefaultSeed, "The whole color scheme is built from this one color.", alpha: false))
                {
                    Settings.UiAccentColor = accent;
                }
            }

            using (M3Card.Begin("appearance_size", "Size", FontAwesomeIcon.TextHeight))
            {
                float text = Settings.UiTextScale;
                if (M3Widgets.RowDragFloat("Text size", ref text, 0.75f, 2f, "%.2fx"))
                {
                    Settings.UiTextScale = MathF.Round(text, 2);
                }

                float element = Settings.UiElementScale;
                if (M3Widgets.RowDragFloat("Control size", ref element, 0.75f, 2f, "%.2fx", "Buttons, switches, sliders and other controls."))
                {
                    Settings.UiElementScale = MathF.Round(element, 2);
                }

                float padding = Settings.UiPaddingScale;
                if (M3Widgets.RowDragFloat("Spacing", ref padding, 0.5f, 2f, "%.2fx", "The space around and between things."))
                {
                    Settings.UiPaddingScale = MathF.Round(padding, 2);
                }

                bool isDefault = Settings.UiTextScale == 1f && Settings.UiElementScale == 1f && Settings.UiPaddingScale == 1f;
                if (M3Widgets.Button("##appearance_reset", "Reset sizes", M3ButtonStyle.Text, FontAwesomeIcon.Undo, enabled: !isDefault))
                {
                    Settings.UiTextScale = 1f;
                    Settings.UiElementScale = 1f;
                    Settings.UiPaddingScale = 1f;
                }
            }
        }
        #endregion

        #region Help
        private static void DrawHelpPage()
        {
            using (M3Card.Begin("help_commands", "Commands", FontAwesomeIcon.Terminal))
            {
                CmdManager.DrawHelp();
            }

            using (M3Card.Begin("help_tips", "Tips", FontAwesomeIcon.Lightbulb))
            {
                M3Text.Draw("Unlock a timeline to move or resize it, then lock it again so clicks pass through to the game.", wrap: true);
                M3Text.Draw("Hover an unlocked timeline for buttons to lock it, open its settings or hide it. Right-clicking it opens its settings too.", wrap: true);
                M3Text.Draw("Hover an action on a timeline to see its cast, recast, lock, damage and statuses.", wrap: true);
            }

            using (M3Card.Begin("help_links", "Links", FontAwesomeIcon.Link))
            {
                M3Flow flow = new();

                flow.Next(M3Widgets.ButtonWidth(FontAwesomeIcon.Code, "Source code"));
                if (M3Widgets.Button("##help_github", "Source code", M3ButtonStyle.Outlined, FontAwesomeIcon.Code))
                {
                    Util.OpenLink("https://github.com/FFXIV-CombatReborn/ActionTimelineReborn");
                }

                flow.Next(M3Widgets.ButtonWidth(FontAwesomeIcon.Coffee, "Support on Ko-fi"));
                if (M3Widgets.Button("##help_kofi", "Support on Ko-fi", M3ButtonStyle.Filled, FontAwesomeIcon.Coffee, accent: _koFiColor))
                {
                    Util.OpenLink("https://ko-fi.com/ltscombatreborn");
                }
            }
        }
        #endregion

        #region Timeline
        private void DrawTimelinePage(DrawingSettings settings, int index)
        {
            using ImRaii.IdDisposable id = ImRaii.PushId(index);

            // Card heights are remembered by ID, so keep each timeline's cards apart.
            string key = $"timeline{index}";

            using (M3Card.Begin($"{key}_timeline", "Timeline", FontAwesomeIcon.Stream))
            {
                M3Widgets.RowText("Name", ref settings.Name, "Timeline name", "What this timeline is called in this list.", 32);
                M3Widgets.RowSwitch("Enabled", ref settings.Enable);
                M3Widgets.RowSwitch("Rotation view", ref settings.IsRotation,
                    "Shows your last actions as a fixed rotation instead of scrolling in real time.");
            }

            using (M3Card.Begin($"{key}_window", "Window", FontAwesomeIcon.WindowMaximize))
            {
                M3Widgets.RowSwitch("Locked", ref settings.Locked, "Stops the window from moving or resizing, and lets clicks pass through it.");
                M3Widgets.RowDragFloat("Scale", ref settings.Scale, 0.5f, 2f, "%.2fx", "Sizes everything on this timeline. Drag its edges to show more or less time.");
                M3Widgets.RowColor("Locked background", ref settings.LockedBackgroundColor, _defaults.LockedBackgroundColor);
                M3Widgets.RowColor("Unlocked background", ref settings.UnlockedBackgroundColor, _defaults.UnlockedBackgroundColor);
            }

            using (M3Card.Begin($"{key}_show", "Show", FontAwesomeIcon.Eye))
            {
                M3Widgets.RowSwitch("Off-GCD actions", ref settings.ShowOGCD, "Adds a lane for off-GCD actions under the GCDs.");
                M3Widgets.RowSwitch("Auto attacks", ref settings.ShowAutoAttack, "Marks each auto attack with a dot under the actions.");
                M3Widgets.RowSwitch("Animation lock", ref settings.ShowAnimationLock, "Underlines each action for as long as it locks you out of others.");
                M3Widgets.RowSwitch("Status changes", ref settings.ShowStatus, "Shows the statuses each action gives or removes, above the GCDs.");
                M3Widgets.RowSwitch("Damage type", ref settings.ShowDamageType, "Rings the icons of direct and critical hits.");
                M3Widgets.RowSwitch("Status line", ref settings.ShowStatusLine, "Adds a lane showing how long your main buffs and potions last.");
            }

            using (M3Card.Begin($"{key}_clipping", "GCD clipping", FontAwesomeIcon.Cut, subtitle: "Highlights the gap when a GCD starts late."))
            {
                if (settings.IsRotation)
                {
                    M3Widgets.Banner("##clipping_rotation", "Clipping isn't shown in rotation view.", M3Severity.Info, FontAwesomeIcon.InfoCircle);
                    ImGui.Dummy(new Vector2(0f, M3.Space2));
                }

                M3Widgets.RowSwitch("Show GCD clipping", ref settings.ShowGCDClippingSetting);
                using (M3SubGroup.Begin())
                {
                    bool enabled = settings.ShowGCDClipping;

                    int threshold = (int)(settings.GCDClippingThreshold * 1000f);
                    if (M3Widgets.RowDragInt("Threshold (ms)", ref threshold, 0, 1000,
                        "Shorter gaps are ignored, to filter out latency. Try a few values to find what suits your connection.", enabled: enabled))
                    {
                        settings.GCDClippingThreshold = threshold / 1000f;
                    }

                    M3Widgets.RowDragInt("Longest gap (s)", ref settings.GCDClippingMaxTime, 3, 60, "Longer gaps aren't counted as clipping.",
                        enabled: enabled);
                }
            }

            using (M3Card.Begin($"{key}_remove", null, style: M3CardStyle.Outlined))
            {
                bool canRemove = Settings.TimelineSettings.Count > 1;
                M3RowInfo row = M3SettingRow.Begin("Remove timeline",
                    canRemove ? "Hold the button to remove this timeline." : "You need at least one timeline.",
                    new Vector2(M3Widgets.ButtonWidth(FontAwesomeIcon.TrashAlt, "Remove"), M3Widgets.ButtonHeight));
                if (M3Widgets.HoldButton("##remove_timeline", "Remove", icon: FontAwesomeIcon.TrashAlt, enabled: canRemove,
                    tooltip: canRemove ? null : "You need at least one timeline."))
                {
                    RemoveTimeline(settings, index);
                }

                M3SettingRow.End(row);
            }
        }

        private void RemoveTimeline(DrawingSettings settings, int index)
        {
            Settings.TimelineSettings.RemoveAt(index);
            _page = TimelinePagePrefix + Math.Max(0, index - 1);

            M3Snackbar.Show($"Removed {TimelineName(settings, index)}.", "Undo", () =>
            {
                int restored = Math.Min(index, Settings.TimelineSettings.Count);
                Settings.TimelineSettings.Insert(restored, settings);
                _page = TimelinePagePrefix + restored;
            });
        }
        #endregion
    }
}
