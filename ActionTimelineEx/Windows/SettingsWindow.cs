using ActionTimelineReborn.Helpers;
using ActionTimelineReborn.Timeline;
using ActionTimelineReborn.Configurations;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.Commands;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace ActionTimelineReborn.Windows
{
    public class SettingsWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        private Settings Settings => Plugin.Settings;

        public SettingsWindow() : base("ActionTimelineReborn v" + typeof(SettingsWindow).Assembly.GetName().Version?.ToString() ?? string.Empty)
        {
            SizeCondition = ImGuiCond.FirstUseEver;
            Size = new Vector2(300, 490f);
            RespectCloseHotkey = true;
        }

        public override void OnClose()
        {
            Settings.Save();
            base.OnClose();
        }

        public override void Draw()
        {
            if (!ImGui.BeginTabBar("ActionTimelineReborn Bar")) return;

            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralSetting();
                ImGui.EndTabItem();
            }

            int index = 0;
            DrawingSettings? removingSetting = null;

            if (Settings.TimelineSettings.Count == 0) Settings.TimelineSettings.Add(new DrawingSettings());

            foreach (var setting in Settings.TimelineSettings)
            {
                if (ImGui.BeginTabItem($"TL:{index}"))
                {
                    if (DrawTimelineSetting(setting))
                    {
                        removingSetting = setting;
                    }
                    ImGui.EndTabItem();
                }
                index++;
            }

            if(removingSetting != null)
            {
                Settings.TimelineSettings.Remove(removingSetting);
            }

            if (ImGui.BeginTabItem("Help"))
            {
                DrawHelp();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        private void DrawHelp() 
        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.Code.ToIconString()}##Github"))
            {
                Util.OpenLink("https://github.com/ArchiDog1998/ActionTimelineReborn");
            }

            ImGui.SameLine();

            if (ImGui.Button($"{FontAwesomeIcon.History.ToIconString()}##ChangeLog"))
            {
                Util.OpenLink("https://github.com/ArchiDog1998/ActionTimelineReborn/blob/release/CHANGELOG.md");
            }
            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, 0xFF5E5BFF);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD5E5BFF);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA5E5BFF);
            if (ImGui.Button($"{FontAwesomeIcon.Coffee.ToIconString()}##Support"))
            {
                Util.OpenLink("https://ko-fi.com/archited");
            }

            ImGui.PopStyleColor(3);
            ImGui.PopFont();

            if (ImGui.BeginChild("Help Information", new Vector2(0f, -1f), true))
            {
                CmdManager.DrawHelp();
                ImGui.EndChild();
            }
        }

        private ushort _aboutAdd = 0;
        private void DrawGeneralSetting()
        {
            if (ImGui.Button("Add One Timeline"))
            {
                Settings.TimelineSettings.Add(new DrawingSettings()
                {
                    Name = (Settings.TimelineSettings.Count + 1).ToString(),
                });
            }
            ImGui.Checkbox("Record Data", ref Settings.Record);
            ImGui.Checkbox("Show Only In Duty", ref Settings.ShowTimelineOnlyInDuty);
            ImGui.Checkbox("Show Only In Combat", ref Settings.ShowTimelineOnlyInCombat);
            ImGui.Checkbox("Hide In Cutscene", ref Settings.HideTimelineInCutscene);
            ImGui.Checkbox("Hide In Quest Event", ref Settings.HideTimelineInQuestEvent);
            ImGui.Checkbox("Print Clipping Time On Chat", ref Settings.PrintClipping);
            if (Settings.PrintClipping)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100 * _scale);
                ImGui.DragIntRange2("Clipping Range", ref Settings.PrintClippingMin, ref Settings.PrintClippingMax);
            }

            ImGui.NewLine();
            
            // Clear All Data button with warning color
            ImGui.PushStyleColor(ImGuiCol.Button, 0xFF4444AA);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF3333DD);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF5555BB);
            if (ImGui.Button("Clear All Timeline Data"))
            {
                TimelineManager.Instance?.ClearAllData();
                Svc.Chat.Print("[ActionTimelineReborn] All timeline data cleared.");
            }
            ImGui.PopStyleColor(3);
            DrawHelper.SetTooltip("Clears all recorded timeline data and caches. This will free memory and reset the timeline.");
            
            ImGui.NewLine();
            ImGui.Checkbox("Record Target Status", ref Settings.RecordTargetStatus);

            var index = 0;

            if(ImGui.CollapsingHeader("Showed Statuses"))
            {
                foreach (var statusId in TimelineManager.ShowedStatusId)
                {
                    var status = Svc.Data.GetExcelSheet<Status>()?.GetRow(statusId);
                    var texture = DrawHelper.GetTextureFromIconId(status?.Icon ?? 0);
                    if (texture != null)
                    {
                        ImGui.Image(texture.Handle, new Vector2(18, 24));
                        var tips = $"{status?.Name ?? string.Empty} [{status?.RowId ?? 0}]";
                        DrawHelper.SetTooltip(tips);
                        if (++index % 10 != 0) ImGui.SameLine();
                    }
                }
            }

            ImGui.SameLine();
            ImGui.NewLine();

            ImGui.Text("Don't record these statuses.");

            if (ImGui.BeginChild("ExceptStatus", new Vector2(0f, -1f), true))
            {
                ushort removeId = 0, addId = 0;
                index = 0;
                foreach (var statusId in Plugin.Settings.HideStatusIds)
                {
                    var status = Svc.Data.GetExcelSheet<Status>()?.GetRow(statusId);
                    var texture = DrawHelper.GetTextureFromIconId(status?.Icon ?? 0);
                    if (texture != null)
                    {
                        ImGui.Image(texture.Handle, new Vector2(24, 30));
                        DrawHelper.SetTooltip(status?.Name.ToString() ?? string.Empty);
                        ImGui.SameLine();
                    }

                    var id = statusId.ToString();
                    ImGui.SetNextItemWidth(100 * _scale);
                    if (ImGui.InputText($"##Status{index++}", ref id, 8) && ushort.TryParse(id, out var newId))
                    {
                        removeId = statusId;
                        addId = newId;
                    }

                    ImGui.SameLine();

                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button($"{FontAwesomeIcon.Ban.ToIconString()}##Remove{statusId}"))
                    {
                        removeId = statusId;
                    }
                    ImGui.PopFont();
                }
                var oneId = string.Empty;
                ImGui.SetNextItemWidth(100 * _scale);
                if (ImGui.InputText($"##AddOne", ref oneId, 8) && ushort.TryParse(oneId, out var newOneId))
                {
                    _aboutAdd = newOneId;
                }
                ImGui.SameLine();

                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##AddNew"))
                {
                    addId = _aboutAdd;
                }
                ImGui.PopFont();

                if (removeId != 0)
                {
                    Plugin.Settings.HideStatusIds.Remove(removeId);
                }
                if (addId != 0)
                {
                    Plugin.Settings.HideStatusIds.Add(addId);
                }
                ImGui.EndChild();
            }
        }

        #region Timeline
        private bool DrawTimelineSetting(DrawingSettings settings)
        {
            var result = false;
            if (!ImGui.BeginTabBar("##Timeline_Settings_TabBar"))
            {
                return result;
            }

            ImGui.PushItemWidth(80 * _scale);

            // general
            if (ImGui.BeginTabItem("General##Timeline_General"))
            {
                result = DrawGeneralTab(settings);
                ImGui.EndTabItem();
            }

            // icons
            if (ImGui.BeginTabItem("Icons##Timeline_Icons"))
            {
                DrawIconsTab(settings);
                ImGui.EndTabItem();
            }

            // casts
            if (ImGui.BeginTabItem("Bar##Timeline_Bar"))
            {
                DrawBarTab(settings);
                ImGui.EndTabItem();
            }

            // grid
            if (ImGui.BeginTabItem("Grid##Timeline_Grid"))
            {
                DrawGridTab(settings);
                ImGui.EndTabItem();
            }

            // gcd clipping
            if (ImGui.BeginTabItem("GCD Clipping##Timeline_GCD"))
            {
                DrawGCDClippingTab(settings);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();

            return result;
        }

        private string _undoName = string.Empty;
        private DateTime _lastTime = DateTime.MinValue;
        private bool RemoveValue(string name)
        {
            ImGui.SameLine();

            bool isLast = name == _undoName && DateTime.Now - _lastTime < TimeSpan.FromSeconds(2);
            bool isTime = DateTime.Now - _lastTime > TimeSpan.FromSeconds(0.5);

            bool result = false;

            if (isLast) ImGui.PushStyleColor(ImGuiCol.Text, isTime ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed);

            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{(isLast ? FontAwesomeIcon.Check : FontAwesomeIcon.Ban).ToIconString()}##Remove{name}"))
            {
                if (isLast && isTime)
                {
                    result = true;
                    _lastTime = DateTime.MinValue;
                }
                else
                {
                    _lastTime = DateTime.Now;
                    _undoName = name;
                }
            }

            ImGui.PopFont();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(!isTime ? "Please wait for a second." :
                isLast ? "Are you sure to remove this timeline?"
                : "Click to remove this timeline.");
            }

            if (isLast) ImGui.PopStyleColor();
            return result;
        }

        private bool DrawGeneralTab(DrawingSettings settings)
        {
            ImGui.InputText("Name", ref settings.Name, 32);
            var result = Plugin.Settings.TimelineSettings.Count > 1 && RemoveValue(settings.Name);

            ImGui.Checkbox("Enable", ref settings.Enable);
            ImGui.Checkbox("Is Rotation", ref settings.IsRotation);
            ImGui.Checkbox("Is Horizonal", ref settings.IsHorizonal);
            ImGui.Checkbox("Is Reverse", ref settings.IsReverse);

            ImGui.NewLine();

            ImGui.Checkbox("Locked", ref settings.Locked);
            ImGui.ColorEdit4("Locked Color", ref settings.LockedBackgroundColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Unlocked Color", ref settings.UnlockedBackgroundColor, ImGuiColorEditFlags.NoInputs);

            ImGui.NewLine();

            ImGui.DragFloat("Size per second", ref settings.SizePerSecond, 0.3f, 20, 150);
            DrawHelper.SetTooltip("This is the width of every second drawn on the window.");

            ImGui.DragInt("Offset Time (seconds)", ref settings.TimeOffsetSetting, 0.1f, 0, 1000);
            DrawHelper.SetTooltip(settings.IsRotation ? "The Offset time of rotation."
                : "This is the advanced time about action using");

            ImGui.DragFloat("Drawing Center offset", ref settings.CenterOffset, 0.3f, -500, 500);

            return result;
        }

        private static void DrawIconsTab(DrawingSettings settings)
        {
            ImGui.DragInt("Icon Size", ref settings.GCDIconSize);

            ImGui.NewLine();
            ImGui.Checkbox("Show Off GCD", ref settings.ShowOGCD);

            if (settings.ShowOGCD)
            {
                ImGui.Indent();
                ImGui.DragInt("Off GCD Icon Size", ref settings.OGCDIconSize, 0.2f, 1, 100);
                ImGui.DragFloat("Iff GCD Vertical Offset", ref settings.OGCDOffset, 0.01f, 0, 1);
                ImGui.Unindent();
            }

            ImGui.NewLine();
            ImGui.Checkbox("Show Auto Attacks", ref settings.ShowAutoAttack);

            if (settings.ShowAutoAttack)
            {
                ImGui.Indent();
                ImGui.DragInt("Auto Attack Icon Size", ref settings.AutoAttackIconSize, 0.2f, 1, 100);
                ImGui.DragFloat("Auto Attack Vertical Offset", ref settings.AutoAttackOffset, 0.01f, 0, 1);
                ImGui.Unindent();
            }

            ImGui.NewLine();
            ImGui.Checkbox("Show Status Gain Lose", ref settings.ShowStatus);

            if (settings.ShowStatus)
            {
                ImGui.Indent();
                ImGui.DragInt("Status Icon Size", ref settings.StatusIconSize, 0.2f, 1, 100);
                ImGui.DragFloat("Status Icon Alpha", ref settings.StatusIconAlpha, 0.01f, 0, 1);
                ImGui.ColorEdit4("Status Gain Color", ref settings.StatusGainColor, ImGuiColorEditFlags.NoInputs);
                ImGui.ColorEdit4("Status Lose Color", ref settings.StatusLoseColor, ImGuiColorEditFlags.NoInputs);
                ImGui.DragFloat("Status Offset", ref settings.StatusOffset, 0.01f, 0, 1);
                ImGui.Unindent();
            }

            ImGui.NewLine();
            ImGui.Checkbox("Show Damage Type", ref settings.ShowDamageType);
            if (settings.ShowDamageType)
            {
                ImGui.Indent();
                ImGui.ColorEdit4("Direct Color", ref settings.DirectColor, ImGuiColorEditFlags.NoInputs);
                ImGui.ColorEdit4("Critical Color", ref settings.CriticalColor, ImGuiColorEditFlags.NoInputs);
                ImGui.ColorEdit4("Critical Direct Color", ref settings.CriticalDirectColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Unindent();
            }
        }

        private static void DrawBarTab(DrawingSettings settings)
        {
            ImGui.ColorEdit4("Bar Background Color", ref settings.BackgroundColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("GCD Border Color", ref settings.GCDBorderColor, ImGuiColorEditFlags.NoInputs);
            ImGui.DragFloat("GCD Border Thickness", ref settings.GCDThickness, 0.01f, 0, 10);
            ImGui.DragFloat("GCD Border Round", ref settings.GCDRound, 0.01f, 0, 10);
            ImGui.DragFloatRange2("GCD Bar Height", ref settings.GCDHeightLow, ref settings.GCDHeightHigh, 0.01f, 0, 1);
            ImGui.NewLine();

            ImGui.ColorEdit4("Cast In Progress Color", ref settings.CastInProgressColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Cast Finished Color", ref settings.CastFinishedColor, ImGuiColorEditFlags.NoInputs);
            ImGui.ColorEdit4("Cast Canceled Color", ref settings.CastCanceledColor, ImGuiColorEditFlags.NoInputs);

            ImGui.NewLine();

            ImGui.Checkbox("Show Animation Lock Time", ref settings.ShowAnimationLock);

            if (settings.ShowAutoAttack)
            {
                ImGui.Indent();
                ImGui.ColorEdit4("Animation Lock Color", ref settings.AnimationLockColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Unindent();
            }

            ImGui.NewLine();

            ImGui.Checkbox("Show Status Line", ref settings.ShowStatusLine);

            if (settings.ShowAutoAttack)
            {
                ImGui.Indent();
                ImGui.DragFloat("Status Line Height", ref settings.StatusLineSize, 0.2f, 1, 100);
                ImGui.Unindent();
            }
        }

        private static void DrawGridTab(DrawingSettings settings)
        {
            ImGui.Checkbox("Enabled", ref settings.ShowGrid);

            ImGui.DragFloat("Start Line Width", ref settings.GridStartLineWidth, 0.1f, 0.1f, 10);
            ImGui.ColorEdit4("Start Line Color", ref settings.GridStartLineColor, ImGuiColorEditFlags.NoInputs);

            if (!settings.ShowGrid) { return; }
            ImGui.NewLine();

            ImGui.Checkbox("Show Center Line", ref settings.ShowGridCenterLine);
            if (settings.ShowGridCenterLine)
            {
                ImGui.Indent();
                ImGui.DragFloat("Center Line Width", ref settings.GridCenterLineWidth, 0.1f, 0.1f, 10);
                ImGui.ColorEdit4("Center Line Color", ref settings.GridCenterLineColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Unindent();
            }

            ImGui.NewLine();

            ImGui.DragFloat("Line Width", ref settings.GridLineWidth, 0.1f, 0.1f, 10);
            ImGui.ColorEdit4("Line Color", ref settings.GridLineColor, ImGuiColorEditFlags.NoInputs);

            ImGui.NewLine();
            ImGui.Checkbox("Divide By Seconds", ref settings.GridDivideBySeconds);

            if (!settings.GridDivideBySeconds) { return; }

            ImGui.Checkbox("Show Text", ref settings.GridShowSecondsText);

            ImGui.NewLine();
            ImGui.Checkbox("Sub-Divide By Seconds", ref settings.GridSubdivideSeconds);

            if (!settings.GridSubdivideSeconds) { return; }

            ImGui.DragInt("Sub-Division Count", ref settings.GridSubdivisionCount, 0.2f, 2, 8);
            ImGui.DragFloat("Sub-Division Line Width", ref settings.GridSubdivisionLineWidth, 0.5f, 1, 5);
            ImGui.ColorEdit4("Sub-Division Line Color", ref settings.GridSubdivisionLineColor, ImGuiColorEditFlags.NoInputs);
        }

        private static void DrawGCDClippingTab(DrawingSettings settings)
        {
            ImGui.Checkbox("Enabled", ref settings.ShowGCDClippingSetting);
            DrawHelper.SetTooltip("This only shown when timeline is not rotation.");

            if (!settings.ShowGCDClipping) return;

            int clippingThreshold = (int)(settings.GCDClippingThreshold * 1000f);
            if (ImGui.DragInt("Threshold (ms)", ref clippingThreshold, 0.1f, 0, 1000))
            {
                settings.GCDClippingThreshold = clippingThreshold / 1000f;
            }
            DrawHelper.SetTooltip("This can be used filter out \"false positives\" due to latency or other factors. Any GCD clipping detected that is shorter than this value will be ignored.\nIt is strongly recommended that you test out different values and find out what works best for your setup.");

            ImGui.DragInt("Max Time (seconds)", ref settings.GCDClippingMaxTime, 0.1f, 3, 60);
            DrawHelper.SetTooltip("Any GCD clip longer than this will be capped");

            ImGui.ColorEdit4("Color", ref settings.GCDClippingColor, ImGuiColorEditFlags.NoInputs);

            ImGui.ColorEdit4("Text Color", ref settings.GCDClippingTextColor, ImGuiColorEditFlags.NoInputs);
        }
        #endregion
    }
}
