using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using Action = Lumina.Excel.Sheets.Action;
using Status = Lumina.Excel.Sheets.Status;

namespace ActionTimelineReborn.Timeline;

public class TimelineManager : IDisposable
{
    internal const byte GCDCooldownGroup = 58;

    #region singleton
    public static void Initialize() { Instance = new TimelineManager(); }

    public static TimelineManager Instance { get; private set; } = null!;

    public TimelineManager()
    {
        try
        {
            Svc.Hook.InitializeFromAttributes(this);
            _onActorControlHook?.Enable();
            _onCastHook?.Enable();
            ActionEffect.ActionEffectEvent += ActionFromSelf;
        }
        catch (Exception e)
        {
            Svc.Log.Error("Error initiating hooks: " + e.Message);
        }
    }

    ~TimelineManager()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _items.Clear();
        _statusItems.Clear();
        _actionCache.Clear();
        _statusCache.Clear();
        _itemCache.Clear();
        ShowedStatusId.Clear();

        ActionEffect.ActionEffectEvent -= ActionFromSelf;

        _onActorControlHook?.Disable();
        _onActorControlHook?.Dispose();

        _onCastHook?.Disable();
        _onCastHook?.Dispose();
    }
    
    /// <summary>
    /// Clears all timeline data and caches. Useful for resetting the timeline or freeing memory.
    /// </summary>
    public void ClearAllData()
    {
        _items.Clear();
        _statusItems.Clear();
        _actionCache.Clear();
        _statusCache.Clear();
        _itemCache.Clear();
        ShowedStatusId.Clear();
        _lastItem = null;
        _lastTime = DateTime.MinValue;
        EndTime = DateTime.Now;
    }
    #endregion

    private delegate void OnActorControlDelegate(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg7, uint arg8, uint arg9, uint arg10, ulong targetId, byte arg12);
    [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(OnActorControl))]
#pragma warning disable CS0649
    private readonly Hook<OnActorControlDelegate>? _onActorControlHook;
#pragma warning restore CS0649

    private delegate void OnCastDelegate(uint sourceId, IntPtr sourceCharacter);
    [Signature("40 53 57 48 81 EC ?? ?? ?? ?? 48 8B FA 8B D1", DetourName = nameof(OnCast))]
#pragma warning disable CS0649
    private readonly Hook<OnCastDelegate>? _onCastHook;
#pragma warning restore CS0649

    public static SortedSet<ushort> ShowedStatusId { get; } = [];
    
    // Cache for action data to reduce Excel sheet lookups
    private static readonly Dictionary<uint, (string name, ushort icon, ActionCate category, byte cooldownGroup, byte additionalCooldownGroup)> _actionCache = [];
    private static readonly Dictionary<uint, (string name, ushort icon, uint maxStacks)> _statusCache = [];
    private static readonly Dictionary<uint, (string name, ushort icon, float castTime)> _itemCache = [];

    public DateTime EndTime { get; private set; } = DateTime.Now;
    private static readonly int kMaxItemCount = 2048;
    private readonly Queue<TimelineItem> _items = new(kMaxItemCount);
    private TimelineItem? _lastItem = null;

    private DateTime _lastTime = DateTime.MinValue;
    private void AddItem(TimelineItem item)
    {
        if (item == null) return;
        if (_items.Count >= 2048)
        {
            _items.Dequeue();
        }
        _items.Enqueue(item);
        if (item.Type != TimelineItemType.AutoAttack)
        {
            _lastItem = item;
            _lastTime = DateTime.Now;
            UpdateEndTime(item.EndTime);
        }
    }

    private void UpdateEndTime(DateTime endTime)
    {
        if(endTime > EndTime) EndTime = endTime;
    }

    public List<TimelineItem> GetItems(DateTime time, out DateTime lastEndTime)
    {
        var result = new List<TimelineItem>();
        lastEndTime = DateTime.Now;
        foreach (var item in _items)
        {
            if (item.EndTime > time)
            {
                result.Add(item);
            }
            else if (item.Type == TimelineItemType.GCD)
            {
                lastEndTime = item.EndTime;
            }
        }
        return result;
    }

    private static readonly int kMaxStatusCount = 256;
    private readonly Queue<StatusLineItem> _statusItems = new(kMaxStatusCount);
    private void AddItem(StatusLineItem item)
    {
        if (item == null) return;
        if (_statusItems.Count >= kMaxStatusCount)
        {
            _statusItems.Dequeue();
        }
        _statusItems.Enqueue(item);
    }

    public List<StatusLineItem> GetStatus(DateTime time, out DateTime lastEndTime)
    {
        return GetItems(_statusItems, time, out lastEndTime);
    }

    private static List<T> GetItems<T>(IEnumerable<T> items, DateTime time, out DateTime lastEndTime) where T : ITimelineItem 
    {
        var result = new List<T>();
        lastEndTime = DateTime.Now;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (item.EndTime > time)
            {
                result.Add(item);
            }
            else if(item is TimelineItem tItem && tItem.Type == TimelineItemType.GCD)
            {
                lastEndTime = item.EndTime;
            }
        }
        return result;
    }

    public unsafe float GCD
    {
        get
        {
            var cdGrp = ActionManager.Instance()->GetRecastGroupDetail(GCDCooldownGroup - 1);
            return cdGrp->Total;
        }
    }

    private static TimelineItemType GetActionType(uint actionId, ActionType type)
    {
        if (Svc.Data.GetExcelSheet<Action>()?.TryGetRow(actionId, out var action) != true)
            return TimelineItemType.OGCD; // Default or fallback type

        if (actionId == 3) return TimelineItemType.OGCD; // Sprint

        var isRealGcd = action.CooldownGroup == GCDCooldownGroup || action.AdditionalCooldownGroup == GCDCooldownGroup;
        return action.ActionCategory.Value.RowId == 1 // AutoAttack
            ? TimelineItemType.AutoAttack
            : !isRealGcd && action.ActionCategory.Value.RowId == 4 ? TimelineItemType.OGCD // Ability
            : TimelineItemType.GCD;
    }

    private void CancelCasting()
    {
        if (_lastItem == null || _lastItem.CastingTime == 0) return;

        _lastItem.State = TimelineItemState.Canceled;
        var maxTime = (float)(DateTime.Now - _lastItem.StartTime).TotalSeconds;
        _lastItem.GCDTime = 0;
        _lastItem.CastingTime = MathF.Min(maxTime, _lastItem.CastingTime);
    }

    private static uint GetStatusIcon(ushort id, bool isGain, out string? name, byte stack = byte.MaxValue)
    {
        name = null;
        if (Plugin.Settings.HideStatusIds.Contains(id)) return 0;
        var status = Svc.Data.GetExcelSheet<Status>()?.GetRow(id);
        if (status == null) return 0;
        name = status.Value.Name.ToString();

        ShowedStatusId.Add(id);
        var icon = status.Value.Icon;

        if (isGain)
        {
            return icon + (uint)Math.Max(0, status.Value.MaxStacks - 1);
        }
        else
        {
            if (stack == byte.MaxValue && Player.Object != null)
            {
                // Replace LINQ with manual loop for better performance
                stack = 0;
                var statusList = Player.Object.StatusList;
                for (int i = 0; i < statusList.Length; i++)
                {
                    if (statusList[i] != null && statusList[i]!.StatusId == id)
                    {
                        stack = (byte)statusList[i]!.Param;
                        break;
                    }
                }
                stack++;
            }
            return icon + (uint)Math.Max(0, stack - 1);
        }
    }

    private void ActionFromSelf(ActionEffectSet set)
    {
        if (!Player.Available) return;

        //Svc.Chat.Print($"Id: {set.Header.ActionID}; {set.Header.ActionType}; Source: {set.Source}");
 
        if (set.Source?.GameObjectId != Player.Object?.GameObjectId || !Plugin.Settings.Record) return;

        DamageType damage = DamageType.None;
        SortedSet<(uint, string?)> statusGain = [], statusLose = [];

        for (int i = 0; i < set.Header.TargetCount; i++)
        {
            var effect = set.TargetEffects[i];
            var recordTarget = Plugin.Settings.RecordTargetStatus 
                || effect.TargetID == Player.Object?.GameObjectId;

            if (effect[0].type is ActionEffectType.Damage or ActionEffectType.Heal)
            {
                var flag = effect[0].param0;
                var hasDirect = (flag & 64) == 64;
                var hasCritical = (flag & 32) == 32;
                damage |= hasCritical ? (hasDirect ? DamageType.CriticalDirect : DamageType.Critical)
                    : hasDirect ? DamageType.Direct : DamageType.None;
            }

            effect.ForEach(x =>
            {
                switch (x.type)
                {
                    case ActionEffectType.ApplyStatusEffectTarget when recordTarget:
                    case ActionEffectType.ApplyStatusEffectSource:
                        var icon = GetStatusIcon(x.value, true, out var name);
                        if (icon != 0) statusGain.Add((icon, name));
                        break;

                    case ActionEffectType.LoseStatusEffectTarget when recordTarget:
                    case ActionEffectType.LoseStatusEffectSource:
                        icon = GetStatusIcon(x.value, false, out name);
                        if (icon != 0) statusLose.Add((icon, name));
                        break;
                }
            });
        }

        var now = DateTime.Now;
        var type = GetActionType(set.Header.ActionID, set.Header.ActionType);

        if (Plugin.Settings.PrintClipping && type == TimelineItemType.GCD)
        {
            // Replace LINQ with manual loop for better performance
            TimelineItem? lastGcd = null;
            foreach (var item in _items)
            {
                if (item.Type == TimelineItemType.GCD)
                {
                    lastGcd = item;
                }
            }
            
            if(lastGcd != null)
            {
                var time = (int)(now - lastGcd.EndTime).TotalMilliseconds;
                if(time >= Plugin.Settings.PrintClippingMin &&  time <= Plugin.Settings.PrintClippingMax)
                {
                    Svc.Chat.Print($"Clipping: {time}ms ({lastGcd.Name} - {set.Name})");
                }
            }
        }

        if (_lastItem != null && _lastItem.CastingTime > 0 && type == TimelineItemType.GCD
            && _lastItem.State == TimelineItemState.Casting) // Finish the casting.
        {
            _lastItem.AnimationLockTime = set.Header.AnimationLockTime;
            _lastItem.Name = set.Name;
            _lastItem.Icon = set.IconId;
            _lastItem.Damage = damage;
            _lastItem.State = TimelineItemState.Finished;
        }
        else
        {
            AddItem(new TimelineItem()
            {
                StartTime = now,
                AnimationLockTime = type == TimelineItemType.AutoAttack ? 0 : set.Header.AnimationLockTime,
                GCDTime = type == TimelineItemType.GCD ? GCD : 0,
                Type = type,
                Name = set.Name,
                Icon = set.IconId,
                Damage = damage,
                State = TimelineItemState.Finished,
            });
        }
        var effectItem = _lastItem;

        if (effectItem == null) return;

        effectItem.IsHq = set.Header.ActionType != ActionType.Item || set.Header.ActionID > 1000000;

        foreach (var i in statusGain)
        {
            effectItem.StatusGainIcon.Add(i);
        }
        foreach (var i in statusLose)
        {
            effectItem.StatusLoseIcon.Add(i);
        }

        if (effectItem.Type is TimelineItemType.AutoAttack) return;

        UpdateEndTime(effectItem.EndTime);

        if (set.Header.TargetCount > 0)
        {
            AddStatusLine(effectItem, set.TargetEffects[0].TargetID);
        }
    }

    private void AddStatusLine(TimelineItem? effectItem, ulong targetId)
    {
        if (effectItem == null) return;

        if (effectItem.StatusGainIcon.Count == 0) return;

        Svc.Framework.RunOnTick(() =>
        {
            List<StatusLineItem> list = new(4);
            foreach (var icon in effectItem.StatusGainIcon)
            {
                if (Plugin.IconStack.TryGetValue(icon.icon, out var stack))
                {
                    var item = new StatusLineItem()
                    {
                        Icon = icon.icon,
                        Name = icon.name,
                        TimeDuration = 6,
                        Stack = stack,
                        StartTime = effectItem.StartTime,
                    };
                    list.Add(item);
                    AddItem(item);
                }
            }

            // Replace LINQ with manual loops for better performance
            if (Player.Object == null) return;
            var playerGameObjectId = Player.Object.GameObjectId;
            
            // Process player status list
            var playerStatusList = Player.Object.StatusList;
            for (int i = 0; i < playerStatusList.Length; i++)
            {
                var status = playerStatusList[i];
                if (status != null && status.SourceId == playerGameObjectId)
                {
                    var statusSheet = Svc.Data.GetExcelSheet<Status>();
                    var statusRow = statusSheet?.GetRow(status.StatusId);
                    if (statusRow != null)
                    {
                        var icon = statusRow.Value.Icon;
                        foreach (var item in list)
                        {
                            if (item.Icon == icon)
                            {
                                item.TimeDuration = (float)(DateTime.Now - effectItem.StartTime).TotalSeconds + status.RemainingTime;
                                break;
                            }
                        }
                    }
                }
            }

            // Process target status list if available
            if (Svc.Objects.SearchById(targetId) is IBattleChara battleChar)
            {
                var targetStatusList = battleChar.StatusList;
                for (int i = 0; i < targetStatusList.Length; i++)
                {
                    var status = targetStatusList[i];
                    if (status != null && status.SourceId == playerGameObjectId)
                    {
                        var statusSheet = Svc.Data.GetExcelSheet<Status>();
                        var statusRow = statusSheet?.GetRow(status.StatusId);
                        if (statusRow != null)
                        {
                            var icon = statusRow.Value.Icon;
                            foreach (var item in list)
                            {
                                if (item.Icon == icon)
                                {
                                    item.TimeDuration = (float)(DateTime.Now - effectItem.StartTime).TotalSeconds + status.RemainingTime;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    private void OnActorControl(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg7, uint arg8, uint arg9, uint arg10, ulong targetId, byte arg12)
    {
        _onActorControlHook?.Original(entityId, type, buffID, direct, actionId, sourceId, arg7, arg8, arg9, arg10, targetId, arg12);

        try
        {
            if (Player.Object == null || entityId != Player.Object.GameObjectId) return;

            // CancelAbility ActorControlCategory value
            if (type == 15)
            {
                CancelCasting();
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Error in OnActorControl: {ex.Message}");
        }
    }

    private unsafe void OnCast(uint sourceId, IntPtr ptr)
    {
        _onCastHook?.Original(sourceId, ptr);

        try
        {
            if (sourceId != Player.Object?.GameObjectId || !Plugin.Settings.Record) return;

            var actionId = *(ushort*)ptr;

            var action = Svc.Data.GetExcelSheet<Action>()?.GetRow(actionId);

            AddItem(new TimelineItem()
            {
                Name =  action?.Name.ToString() ?? string.Empty,
                Icon =  actionId == 4 ? (ushort)118 //Mount
                        : action?.Icon ?? 0,
                StartTime = DateTime.Now,
                GCDTime = GCD,
                CastingTime = Player.Object.TotalCastTime - Player.Object.CurrentCastTime,
                Type = TimelineItemType.GCD,
                State = TimelineItemState.Casting,
            });
        }
        catch(Exception ex)
        {
            Svc.Log.Warning(ex, "Something wrong with OnCast1");
        }
    }
}
