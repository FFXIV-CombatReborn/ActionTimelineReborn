using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using Action = Lumina.Excel.Sheets.Action;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using GameStatus = FFXIVClientStructs.FFXIV.Client.Game.Status;
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
        Svc.Framework.Update += UpdateStatuses;

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
        _statusChanges.Clear();
        _trackedStatuses.Clear();
        _actionCache.Clear();
        _statusCache.Clear();
        _itemCache.Clear();
        ShowedStatusId.Clear();

        Svc.Framework.Update -= UpdateStatuses;
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
        _statusChanges.Clear();
        _trackedStatuses.Clear();
        _statusesTracked = false;
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
    private static readonly Dictionary<ushort, (string name, uint icon, byte maxStacks)> _statusCache = [];
    private static readonly Dictionary<uint, (string name, ushort icon, float castTime)> _itemCache = [];

    public DateTime EndTime { get; private set; } = DateTime.Now;
    private static readonly int kMaxItemCount = 2048;
    private readonly List<TimelineItem> _items = new(kMaxItemCount);
    private TimelineItem? _lastItem = null;

    private DateTime _lastTime = DateTime.MinValue;
    private void AddItem(TimelineItem item)
    {
        if (item == null)
        {
            return;
        }

        if (_items.Count >= 2048)
        {
            _items.RemoveAt(0);
        }
        _items.Add(item);
        if (item.Type != TimelineItemType.AutoAttack)
        {
            _lastItem = item;
            _lastTime = DateTime.Now;
            UpdateEndTime(item.EndTime);
        }
    }

    private void UpdateEndTime(DateTime endTime)
    {
        if (endTime > EndTime)
        {
            EndTime = endTime;
        }
    }

    public IReadOnlyList<TimelineItem> Items => _items;

    public List<TimelineItem> GetItems(DateTime time, out DateTime lastEndTime)
    {
        List<TimelineItem> result = [];
        lastEndTime = DateTime.Now;
        foreach (TimelineItem item in _items)
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

    // A status timer jumping up by more than this means it was applied again.
    private const float StatusRefreshThreshold = 1f;

    private readonly record struct TrackedStatus(byte Stacks, float RemainingTime);

    private static readonly int kMaxStatusCount = 256;
    private readonly Queue<StatusChangeItem> _statusChanges = new(kMaxStatusCount);
    private Dictionary<ushort, TrackedStatus> _trackedStatuses = [];
    private Dictionary<ushort, TrackedStatus> _scannedStatuses = [];
    private bool _statusesTracked;

    public List<StatusChangeItem> GetStatusChanges(DateTime time)
    {
        return GetItems(_statusChanges, time, out _);
    }

    // Compares the statuses you're the source of against the last frame, and records what changed.
    private void UpdateStatuses(IFramework framework)
    {
        if (Plugin.Settings == null || !Plugin.Settings.Record || Player.Object is not { } player)
        {
            _trackedStatuses.Clear();
            _statusesTracked = false;
            return;
        }

        _scannedStatuses.Clear();
        if (Plugin.Settings.RecordTargetStatus)
        {
            foreach (IGameObject obj in Svc.Objects)
            {
                if (obj is IBattleChara)
                {
                    ScanStatuses(obj.Address, player.EntityId, _scannedStatuses);
                }
            }
        }
        else
        {
            ScanStatuses(player.Address, player.EntityId, _scannedStatuses);
        }

        // The first scan is what you already had, not something that just changed.
        if (_statusesTracked)
        {
            DateTime now = DateTime.Now;
            foreach ((ushort id, TrackedStatus status) in _scannedStatuses)
            {
                if (!_trackedStatuses.TryGetValue(id, out TrackedStatus before))
                {
                    AddStatusChange(id, status.Stacks, StatusChangeType.Gained, now);
                }
                else if (status.Stacks != before.Stacks)
                {
                    AddStatusChange(id, status.Stacks, status.Stacks > before.Stacks ? StatusChangeType.StacksUp : StatusChangeType.StacksDown, now);
                }
                else if (status.RemainingTime > before.RemainingTime + StatusRefreshThreshold)
                {
                    AddStatusChange(id, status.Stacks, StatusChangeType.Gained, now);
                }
            }

            foreach ((ushort id, TrackedStatus status) in _trackedStatuses)
            {
                if (!_scannedStatuses.ContainsKey(id))
                {
                    AddStatusChange(id, status.Stacks, StatusChangeType.Lost, now);
                }
            }
        }

        (_trackedStatuses, _scannedStatuses) = (_scannedStatuses, _trackedStatuses);
        _statusesTracked = true;
    }

    // The same status on several targets counts once, so it's only lost when it's gone from all of them.
    private static unsafe void ScanStatuses(nint address, uint sourceId, Dictionary<ushort, TrackedStatus> found)
    {
        BattleChara* chara = (BattleChara*)address;
        if (chara == null)
        {
            return;
        }

        foreach (GameStatus status in chara->StatusManager.Status)
        {
            if (status.StatusId == 0 || status.SourceObject.ObjectId != sourceId)
            {
                continue;
            }

            (_, uint icon, byte maxStacks) = GetStatusData(status.StatusId);
            if (icon == 0)
            {
                continue;
            }

            byte stacks = maxStacks > 1 ? (byte)Math.Min(status.Param, maxStacks) : (byte)0;
            float remaining = status.RemainingTime;
            if (found.TryGetValue(status.StatusId, out TrackedStatus other))
            {
                stacks = Math.Max(stacks, other.Stacks);
                remaining = MathF.Max(remaining, other.RemainingTime);
            }

            found[status.StatusId] = new TrackedStatus(stacks, remaining);
        }
    }

    private void AddStatusChange(ushort id, byte stacks, StatusChangeType type, DateTime time)
    {
        if (Plugin.Settings.HideStatusIds.Contains(id))
        {
            return;
        }

        ShowedStatusId.Add(id);
        (string name, uint icon, _) = GetStatusData(id);

        if (_statusChanges.Count >= kMaxStatusCount)
        {
            _statusChanges.Dequeue();
        }
        _statusChanges.Enqueue(new StatusChangeItem()
        {
            // Stacking statuses have an icon for each stack count, one after another.
            Icon = stacks > 1 ? icon + stacks - 1u : icon,
            Name = name,
            Stacks = stacks,
            Type = type,
            StartTime = time,
        });
    }

    private static (string name, uint icon, byte maxStacks) GetStatusData(ushort id)
    {
        if (!_statusCache.TryGetValue(id, out (string name, uint icon, byte maxStacks) data))
        {
            data = Svc.Data.GetExcelSheet<Status>()?.GetRowOrDefault(id) is Status status
                ? (status.Name.ToString(), status.Icon, status.MaxStacks)
                : (string.Empty, 0u, (byte)0);
            _statusCache[id] = data;
        }

        return data;
    }

    private static List<T> GetItems<T>(IEnumerable<T> items, DateTime time, out DateTime lastEndTime) where T : ITimelineItem
    {
        List<T> result = [];
        lastEndTime = DateTime.Now;
        foreach (T item in items)
        {
            if (item == null)
            {
                continue;
            }

            if (item.EndTime > time)
            {
                result.Add(item);
            }
            else if (item is TimelineItem tItem && tItem.Type == TimelineItemType.GCD)
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
            RecastDetail* cdGrp = ActionManager.Instance()->GetRecastGroupDetail(GCDCooldownGroup - 1);
            return cdGrp->Total;
        }
    }

    private static TimelineItemType GetActionType(uint actionId, ActionType type)
    {
        if (Svc.Data.GetExcelSheet<Action>()?.TryGetRow(actionId, out Action action) != true)
        {
            return TimelineItemType.OGCD; // Default or fallback type
        }

        if (actionId == 3)
        {
            return TimelineItemType.OGCD; // Sprint
        }

        bool isRealGcd = action.CooldownGroup == GCDCooldownGroup || action.AdditionalCooldownGroup == GCDCooldownGroup;
        return action.ActionCategory.Value.RowId == 1 // AutoAttack
            ? TimelineItemType.AutoAttack
            : !isRealGcd && action.ActionCategory.Value.RowId == 4 ? TimelineItemType.OGCD // Ability
            : TimelineItemType.GCD;
    }

    private void CancelCasting()
    {
        if (_lastItem == null || _lastItem.CastingTime == 0)
        {
            return;
        }

        _lastItem.State = TimelineItemState.Canceled;
        float maxTime = (float)(DateTime.Now - _lastItem.StartTime).TotalSeconds;
        _lastItem.GCDTime = 0;
        _lastItem.CastingTime = MathF.Min(maxTime, _lastItem.CastingTime);
    }

    private void ActionFromSelf(ActionEffectSet set)
    {
        if (!Player.Available)
        {
            return;
        }

        //Svc.Chat.Print($"Id: {set.Header.ActionID}; {set.Header.ActionType}; Source: {set.Source}");

        if (set.Source?.GameObjectId != Player.Object?.GameObjectId || !Plugin.Settings.Record)
        {
            return;
        }

        DamageType damage = DamageType.None;

        for (int i = 0; i < set.Header.TargetCount; i++)
        {
            TargetEffect effect = set.TargetEffects[i];
            if (effect[0].type is ActionEffectType.Damage or ActionEffectType.Heal)
            {
                byte flag = effect[0].param0;
                bool hasDirect = (flag & 64) == 64;
                bool hasCritical = (flag & 32) == 32;
                damage |= hasCritical ? (hasDirect ? DamageType.CriticalDirect : DamageType.Critical)
                    : hasDirect ? DamageType.Direct : DamageType.None;
            }
        }

        DateTime now = DateTime.Now;
        TimelineItemType type = GetActionType(set.Header.ActionID, set.Header.ActionType);

        if (Plugin.Settings.PrintClipping && type == TimelineItemType.GCD)
        {
            // Replace LINQ with manual loop for better performance
            TimelineItem? lastGcd = null;
            foreach (TimelineItem item in _items)
            {
                if (item.Type == TimelineItemType.GCD)
                {
                    lastGcd = item;
                }
            }

            if (lastGcd != null)
            {
                int time = (int)(now - lastGcd.EndTime).TotalMilliseconds;
                if (time >= Math.Max(Plugin.Settings.PrintClippingMin, 1) && time <= Plugin.Settings.PrintClippingMax)
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
        TimelineItem? effectItem = _lastItem;

        if (effectItem == null)
        {
            return;
        }

        effectItem.IsHq = set.Header.ActionType != ActionType.Item || set.Header.ActionID > 1000000;

        if (effectItem.Type is TimelineItemType.AutoAttack)
        {
            return;
        }

        UpdateEndTime(effectItem.EndTime);
    }

    private void OnActorControl(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId, uint arg7, uint arg8, uint arg9, uint arg10, ulong targetId, byte arg12)
    {
        _onActorControlHook?.Original(entityId, type, buffID, direct, actionId, sourceId, arg7, arg8, arg9, arg10, targetId, arg12);

        try
        {
            if (Player.Object == null || entityId != Player.Object.GameObjectId)
            {
                return;
            }

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
            if (sourceId != Player.Object?.GameObjectId || !Plugin.Settings.Record)
            {
                return;
            }

            ushort actionId = *(ushort*)ptr;

            Action? action = Svc.Data.GetExcelSheet<Action>()?.GetRow(actionId);

            AddItem(new TimelineItem()
            {
                Name = action?.Name.ToString() ?? string.Empty,
                Icon = actionId == 4 ? (ushort)118 //Mount
                        : action?.Icon ?? 0,
                StartTime = DateTime.Now,
                GCDTime = GCD,
                CastingTime = Player.Object.TotalCastTime - Player.Object.CurrentCastTime,
                Type = TimelineItemType.GCD,
                State = TimelineItemState.Casting,
            });
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Something wrong with OnCast1");
        }
    }
}
