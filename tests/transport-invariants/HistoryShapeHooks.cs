using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using EngineHistory = SabberStoneCore.Kettle.PowerHistory;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

// All patches are record-only void prefixes/postfixes. No ref arguments, result replacement, or engine writes.
internal static class HistoryShapeHooks
{
    private static readonly ConditionalWeakTable<Game, HistoryShape> Games = new();
    private static readonly ConditionalWeakTable<EngineHistory, HistoryShape> Histories = new();
    private static readonly ConditionalWeakTable<EngineHistory, Stack<HashSet<int>>> selections = new();
    [ThreadStatic] private static Stack<Call> calls;
    [ThreadStatic] private static Stack<TaskCall> tasks;
    [ThreadStatic] private static Stack<(IPlayable Source, int Depth)> healing;
    private static readonly FieldInfo QueueGame = AccessTools.Field(typeof(TaskQueue), "_game");
    private static readonly PropertyInfo CurrentQueue = AccessTools.Property(typeof(TaskQueue), "CurrentQueue");
    private static readonly MethodInfo PeekTask = CurrentQueue.PropertyType.GetMethod("Peek");
    private static readonly FieldInfo DeathId = AccessTools.Field(typeof(TaskQueue).GetNestedType("TaskInstance", BindingFlags.NonPublic), "DeathId");
    private static readonly FieldInfo TaskBody = AccessTools.Field(DeathId.DeclaringType, "Task");
    private static readonly FieldInfo TaskSource = AccessTools.Field(DeathId.DeclaringType, "Source");
    private static readonly FieldInfo TaskTarget = AccessTools.Field(DeathId.DeclaringType, "Target");
    private static readonly FieldInfo RecorderGame = AccessTools.Field(typeof(Game).Assembly.GetType("SabberStoneCore.Model.HistoryRecorder"), "_game");

    internal sealed class Call
    {
        internal Character Victim;
        internal IPlayable Source;
        internal HistoryShape Shape;
        internal bool Heal, Lifesteal, Selected, DestroyedBeforeHit, Combat, Converted;
        internal int TaskDepth, MaximumHealth;
        internal bool HealthLossReplacement;
        internal readonly List<(GameTag Tag, HistoryShape.Mutation Mutation)> Mutations = new();
    }
    private sealed class TaskCall
    {
        internal Game Game;
        internal PowerActivation Activation;
        internal int DeathEventId, Source, Invocation;
        internal bool Started;
    }
    internal static void Attach(Game game, HistoryShape shape)
    {
        Games.Remove(game); Games.Add(game, shape);
        Histories.Remove(game.PowerHistory); Histories.Add(game.PowerHistory, shape);
    }
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Character), nameof(Character.TakeDamage)),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeDamage)), postfix: new(typeof(HistoryShapeHooks), nameof(AfterCall)));
        harmony.Patch(AccessTools.Method(typeof(Character), nameof(Character.TakeHeal)),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeHeal)), postfix: new(typeof(HistoryShapeHooks), nameof(AfterCall)));
        harmony.Patch(AccessTools.Method(typeof(HealingRules), nameof(HealingRules.Heal)),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeHealing)), postfix: new(typeof(HistoryShapeHooks), nameof(AfterHealing)));
        harmony.Patch(AccessTools.Method(typeof(EngineHistory), nameof(EngineHistory.Add)),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeHistory)));
        harmony.Patch(AccessTools.Method(typeof(TaskQueue), nameof(TaskQueue.Process)),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeTask)), postfix: new(typeof(HistoryShapeHooks), nameof(AfterTask)));
        harmony.Patch(AccessTools.Method(typeof(TaskQueue), nameof(TaskQueue.Execute)),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeExecute)), postfix: new(typeof(HistoryShapeHooks), nameof(AfterTask)));
        var recorder = RecorderGame.DeclaringType;
        harmony.Patch(AccessTools.Method(recorder, "DamageResolved"), prefix: new(typeof(HistoryShapeHooks), nameof(ResolvedDamage)));
        harmony.Patch(AccessTools.Method(recorder, "HealthChanged"), prefix: new(typeof(HistoryShapeHooks), nameof(ResolvedHeal)));
        var scope = typeof(Game).Assembly.GetType("SabberStoneCore.Model.HistoryRecorder+AttackScope");
        harmony.Patch(AccessTools.Method(scope, "Finish"), prefix: new(typeof(HistoryShapeHooks), nameof(BeforeAttackFinish)));
        harmony.Patch(AccessTools.PropertySetter(typeof(Choice), nameof(Choice.Choices)),
            postfix: new(typeof(HistoryShapeHooks), nameof(RecordChoices)));
        harmony.Patch(AccessTools.Method(typeof(RandomTask), nameof(RandomTask.Process)),
            postfix: new(typeof(HistoryShapeHooks), nameof(AfterSelection)));
        harmony.Patch(AccessTools.Method(typeof(IncludeTask), nameof(IncludeTask.Process)),
            postfix: new(typeof(HistoryShapeHooks), nameof(AfterSelection)));
        harmony.Patch(AccessTools.Method(typeof(EngineHistory), "SelectResolvedTarget"),
            prefix: new(typeof(HistoryShapeHooks), nameof(BeforeResolvedSelection)));
        foreach (var type in new[] { typeof(DamageTask), typeof(DamageNumberTask), typeof(HealTask), typeof(HealNumberTask), typeof(HealFullTask) })
            harmony.Patch(AccessTools.Method(type, "Process"), prefix: new(typeof(HistoryShapeHooks), nameof(BeforeEffectTask)));
    }
    private static HashSet<int> Selection(Game game) => game.PowerHistory != null
        && selections.TryGetValue(game.PowerHistory, out var frames) && frames.Count > 0 ? frames.Peek() : null;
    private static void BeforeResolvedSelection(ICharacter target)
    {
        var selected = Selection(target.Game);
        if (selected == null) return;
        selected.Clear(); selected.Add(target.Id);
    }
    private static void AfterSelection(object __instance, object[] __args, TaskState __result)
    {
        var selected = Selection((Game)__args[0]);
        if (selected == null) return;
        if (__instance is IncludeTask && (EntityType)AccessTools.Field(typeof(IncludeTask), "_includeType").GetValue(__instance) == EntityType.STACK) return;
        selected.Clear();
        if (__result != TaskState.COMPLETE) return;
        var type = __instance is RandomTask ? EntityType.TARGET : (EntityType)AccessTools.Field(typeof(IncludeTask), "_includeType").GetValue(__instance);
        if (type is EntityType.TARGET or EntityType.EVENT_TARGET or EntityType.EVENT_SOURCE)
            selected.UnionWith(((TaskStack)__args[4]).Playables.OfType<ICharacter>().Select(p => p.Id));
    }
    private static void BeforeEffectTask(object __instance, object[] __args)
    {
        var selected = Selection((Game)__args[0]);
        if (selected == null) return;
        var type = (EntityType)AccessTools.Property(__instance.GetType(), "Type").GetValue(__instance);
        if (type == EntityType.STACK) return;
        selected.Clear();
        var game = (Game)__args[0];
        if (type == EntityType.TARGET && __args[3] is ICharacter target) selected.Add(target.Id);
        if (type == EntityType.EVENT_TARGET && game.CurrentEventData?.EventTarget is ICharacter eventTarget) selected.Add(eventTarget.Id);
        if (type == EntityType.EVENT_SOURCE && game.CurrentEventData?.EventSource is ICharacter eventSource) selected.Add(eventSource.Id);
    }
    private static void RecordChoices(Choice __instance)
    {
        if (__instance.Controller.PlayerId != 2 || __instance.ChoiceType == ChoiceType.MULLIGAN
            || __instance.ChoiceAction == ChoiceAction.REWIND
            || !Games.TryGetValue(__instance.Controller.Game, out var shape)) return;
        foreach (int id in __instance.Choices ?? Enumerable.Empty<int>()) shape.RecordPrivateChoice(id);
    }
    private static Call Begin(Character victim, IPlayable source, bool heal)
    {
        var call = new Call { Victim = victim, Source = source, Heal = heal, DestroyedBeforeHit = victim.ToBeDestroyed, TaskDepth = tasks?.Count ?? 0 };
        call.MaximumHealth = victim.BaseHealth;
        call.HealthLossReplacement = !heal && victim is Hero && victim.Game.CurrentPlayer == victim.Controller
            && victim.Controller.BoardZone.Any(m => m.Card.Id == "CATA_155" && !m.IsSilenced && !m.Untouchable && !m.ToBeDestroyed);
        Games.TryGetValue(victim.Game, out call.Shape);
        call.Selected = Selection(victim.Game)?.Contains(victim.Id) == true;
        call.Lifesteal = heal && calls?.Count > 0 && !calls.Peek().Heal && ReferenceEquals(calls.Peek().Source, source)
            && (tasks?.Count ?? 0) == calls.Peek().TaskDepth && ReferenceEquals(victim, source.Controller.Hero) && source.HasLifeSteal && !source.Controller.RestoreToDamage;
        call.Converted = !heal && healing?.Count > 0 && ReferenceEquals(healing.Peek().Source, source)
            && healing.Peek().Depth == (calls?.Count ?? 0) && HealingRules.IsConvertedHeal(source);
        (calls ??= new()).Push(call);
        return call;
    }
    private static void BeforeDamage(Character __instance, IPlayable source, bool combat, out Call __state)
    {
        __state = Begin(__instance, source, false);
        __state.Combat = combat;
    }
    private static void BeforeHeal(Character __instance, IPlayable source, out Call __state) => __state = Begin(__instance, source, true);
    private static void BeforeHealing(IPlayable source) => (healing ??= new()).Push((source, calls?.Count ?? 0));
    private static void AfterHealing() => healing.Pop();
    private static void AfterCall(Call __state)
    {
        // Shield resolution returns before DamageResolved; only the recorded shield mutation is a hit.
        if (!__state.Heal && __state.Mutations.Any(m => m.Tag == GameTag.DIVINE_SHIELD && m.Mutation.Value == 0))
            Commit(__state, 0, false, true, false);
        if (!ReferenceEquals(calls.Pop(), __state)) throw new InvalidOperationException("history-shape hook call stack lost");
    }
    private static void BeforeTask(TaskQueue __instance)
    {
        var call = new TaskCall { Game = (Game)QueueGame.GetValue(__instance) };
        if (Games.TryGetValue(call.Game, out _))
        {
            // Granted activations lack DeathId; printed bodies can also be shared with Battlecries.
            object queue = CurrentQueue.GetValue(__instance);
            object next = PeekTask.Invoke(queue, null);
            call.DeathEventId = (int)DeathId.GetValue(next);
            var source = TaskSource.GetValue(next) as IPlayable;
            var granted = TaskTarget.GetValue(next) as Enchantment;
            object body = TaskBody.GetValue(next);
            call.Activation = body != null && (call.DeathEventId != 0 && ReferenceEquals(body, source?.Power?.DeathrattleTask)
                || ReferenceEquals(body, granted?.Power?.DeathrattleTask)) ? PowerActivation.DEATHRATTLE : PowerActivation.POWER;
            RecordInvocation(call, source, granted, body);
        }
        (tasks ??= new()).Push(call);
    }
    private static void RecordInvocation(TaskCall call, IPlayable source, Enchantment granted, object body)
    {
        if (source == null || !Games.TryGetValue(call.Game, out var shape)) return;
        var power = source.Power;
        // Complete registered bodies can repeat; a queued per-hit continuation is still the same activation.
        bool registered = body != null && (ReferenceEquals(body, power?.PowerTask) || ReferenceEquals(body, power?.ComboTask)
            || ReferenceEquals(body, power?.DeathrattleTask) || ReferenceEquals(body, power?.Trigger?.SingleTask)
            || ReferenceEquals(body, source.ActivatedTrigger?.SingleTask)
            || ReferenceEquals(body, power?.TopdeckTask) || ReferenceEquals(body, power?.OverkillTask)
            || ReferenceEquals(body, granted?.Power?.DeathrattleTask)
            || source.ChooseOnePlayables?.Any(option => ReferenceEquals(body, option.Power?.PowerTask)
                || ReferenceEquals(body, option.Power?.DeathrattleTask) || ReferenceEquals(body, option.Power?.ComboTask)) == true);
        call.Source = source.Id; call.Invocation = registered ? shape.NextInvocation() : 0;
    }
    private static void BeforeExecute(TaskQueue __instance, object[] __args)
    {
        var call = new TaskCall { Game = (Game)QueueGame.GetValue(__instance) };
        RecordInvocation(call, __args[2] as IPlayable, __args[3] as Enchantment, __args[0]);
        (tasks ??= new()).Push(call);
    }
    private static void AfterTask() => tasks.Pop();
    private static void BeforeAttackFinish(object __instance, bool reached)
    {
        object owner = AccessTools.Field(__instance.GetType(), "_owner").GetValue(__instance);
        var game = (Game)RecorderGame.GetValue(owner);
        if (game.PowerHistory != null && attackByHistory.TryGetValue(game.PowerHistory, out var attacks) && attacks.Count > 0)
        {
            var attack = attacks.Pop(); attack.Cancelled = !reached;
            attack.Target = ((ICharacter)AccessTools.Field(__instance.GetType(), "_target").GetValue(__instance)).Id;
        }
    }
    private static void BeforeHistory(EngineHistory __instance, IPowerHistoryEntry entry)
    {
        if (!Histories.TryGetValue(__instance, out var shape)) return;
        if (entry is PowerHistoryBlockStart start)
        {
            selections.GetValue(__instance, _ => new()).Push(new());
            var task = tasks?.Count > 0 ? tasks.Peek() : null;
            var block = new HistoryShape.GroundBlock { Type = (int)start.BlockType, Source = start.Source, Target = start.Target,
                Activation = task != null && !task.Started ? task.Activation : PowerActivation.POWER,
                Invocation = task?.Source == start.Source ? task.Invocation : 0,
                DeathEventId = task?.DeathEventId ?? 0 };
            if (task != null) task.Started = true;
            shape.RecordBlock(block);
            if (start.BlockType == BlockType.ATTACK)
            attackByHistory.GetValue(__instance, _ => new()).Push(block);
            return;
        }
        if (entry is PowerHistoryBlockEnd && selections.TryGetValue(__instance, out var frames) && frames.Count > 0) frames.Pop();
        if (entry is not SabberStoneCore.Kettle.PowerHistoryTagChange tag
            || tag.Tag is not (GameTag.DAMAGE or GameTag.ARMOR or GameTag.DIVINE_SHIELD or GameTag.FATIGUE)) return;
        var mutation = shape.RecordMutation(tag.EntityId, tag.Tag, tag.Value);
        var call = calls?.Count > 0 ? calls.Peek() : null;
        if (call?.Shape == shape && call.Victim.Id == tag.EntityId)
            call.Mutations.Add((tag.Tag, mutation));
    }
    private static void ResolvedDamage(IPlayable source, ICharacter victim, int armorDamage, int healthDamage)
    {
        var call = calls?.Count > 0 ? calls.Peek() : null;
        if (call?.Shape == null || call.Heal || !ReferenceEquals(call.Source, source) || !ReferenceEquals(call.Victim, victim)) return;
        if (armorDamage > 0 || healthDamage > 0) Commit(call, Math.Max(0, armorDamage) + Math.Max(0, healthDamage), false, false, armorDamage > 0, healthDamage > 0);
        // Independent oracle: observe the resolved hit before the engine applies its keyword.
        if (victim is Minion minion && healthDamage > 0 && !call.DestroyedBeforeHit
            && minion.Zone.Type == SabberStoneCore.Enums.Zone.PLAY)
        {
            if (call.Combat && source is Hero hero && hero.Weapon?.Poisonous == true) source = hero.Weapon;
            if (source is Minion m ? m.Poisonous : source[GameTag.POISONOUS] == 1)
                call.Shape.RecordPoison(source.Id, victim.Id);
        }
    }
    private static void ResolvedHeal(ICharacter entity, int oldHealth, HistoryHealthChangeReason reason)
    {
        var call = calls?.Count > 0 ? calls.Peek() : null;
        if (call?.Shape == null || !call.Heal || reason != HistoryHealthChangeReason.Heal || !ReferenceEquals(call.Victim, entity)) return;
        int healed = entity.Health - oldHealth;
        if (healed > 0) Commit(call, healed, true, false, false);
    }
    private static void Commit(Call call, int amount, bool heal, bool shield, bool armor, bool health = true)
    {
        if (call.Shape == null) return;
        var hit = new HistoryShape.Hit { Source = call.Source.Id, SourceCard = call.Source.Card.Id, Victim = call.Victim.Id,
            Amount = amount, Heal = heal, Shield = shield, Fatigue = !heal && call.Victim is Hero
                // Converted hero self-hits are ordinary damage only when they leave the counter alone.
                && (call.Mutations.Any(m => m.Tag == GameTag.FATIGUE)
                    || ReferenceEquals(call.Source, call.Victim) && !call.Converted), Lifesteal = call.Lifesteal,
            Selected = call.Selected,
            ReplacedHealth = call.HealthLossReplacement ? Math.Max(0, call.Victim.BaseHealth - call.MaximumHealth) : 0,
            // Further hits on a pending-dead saturated minion have metadata but no changed counter.
            SaturatedDamage = !heal && !shield && call.DestroyedBeforeHit && call.Victim is Minion
                && call.Victim.Damage == int.MaxValue && !call.Mutations.Any(m => m.Tag == GameTag.DAMAGE) };
        if (call.Source is Enchantment enchantment)
        {
            if (enchantment.Target != null) hit.Related.Add(enchantment.Target.Id);
            if (enchantment.Creator != null) hit.Related.Add(enchantment.Creator.Id);
        }
        if (call.Source is Weapon weapon && ReferenceEquals(weapon.Controller.Hero.Weapon, weapon)) hit.Related.Add(weapon.Controller.Hero.Id);
        foreach (var tag in shield ? new[] { GameTag.DIVINE_SHIELD } : armor && health ? new[] { GameTag.ARMOR, GameTag.DAMAGE }
            : armor ? new[] { GameTag.ARMOR } : new[] { GameTag.DAMAGE })
        {
            var candidate = call.Mutations.LastOrDefault(m => m.Tag == tag);
            if (candidate.Mutation != null) candidate.Mutation.Hit = hit;
        }
        call.Shape.RegisterHit(hit);
    }
    private static readonly ConditionalWeakTable<EngineHistory, Stack<HistoryShape.GroundBlock>> attackByHistory = new();
}
