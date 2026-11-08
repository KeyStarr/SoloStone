using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public enum AuraSpellClock { OwnerEnd, OwnerStart, EnemyEnd }

    /// <summary>Independent public objective actors; all mutable state is in entities.</summary>
    public static class AuraSpellRules
    {
        private const GameTag Bonus = (GameTag)32500, LastTick = (GameTag)32501;
        public const GameTag CastCost = (GameTag)32502;
        private const int PendingCastKey = 32502;
        private const int AfterCastKey = 32503;
        public static bool IsInternalTag(GameTag tag) => tag == Bonus || tag == LastTick || tag == CastCost;

        // Definition-only recipes, never games, entities or captured runtime delegates.
        private sealed class Definition
        {
            internal int Ticks;
            internal AuraSpellClock Clock;
            internal ISimpleTask Effect, HolyCast;
        }
        private static readonly Dictionary<int, Definition> Definitions = new Dictionary<int, Definition>();

        public static void Declare(Card card, int ticks = 3, AuraSpellClock clock = AuraSpellClock.OwnerEnd,
            ISimpleTask effect = null, IAura aura = null, ISimpleTask holyCast = null)
        {
            if (ticks <= 0 || card.Type != CardType.SPELL || card[(GameTag)3429] != 1)
                throw new ArgumentException("Timed Aura needs a positive clock and exported Aura spell identity.");
            Definitions.Add(card.AssetId, new Definition { Ticks = ticks, Clock = clock, Effect = effect, HolyCast = holyCast });
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => Install((Spell)s)),
                Aura = aura,
                Trigger = new Trigger(clock == AuraSpellClock.OwnerStart ? TriggerType.TURN_START : TriggerType.TURN_END) {
                    FastExecution = true,
                    EitherTurn = clock == AuraSpellClock.EnemyEnd,
                    TriggerSource = clock == AuraSpellClock.EnemyEnd ? TriggerSource.ENEMY : TriggerSource.ALL,
                    SingleTask = new CustomTask((g, c, s, t, stack) => Tick((Spell)s))
                }
            };
            if (holyCast != null)
                card.Power.Trigger = new MultiTrigger(card.Power.Trigger, new Trigger(TriggerType.AFTER_CAST) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = new SelfCondition(CardSets.Client.Sets.VeteranWarmedic.IsHoly),
                    SingleTask = new CustomTask((g, c, s, t, stack) => FlushCasts((Spell)t, AfterCastKey, s.Id))
                });
        }

        public static bool IsActive(IPlayable spell) => spell is Spell && spell.Zone == spell.Controller.SecretZone
            && spell.Controller.SecretZone.Auras.Contains(spell);
        public static bool HasActive(Controller owner) => owner.SecretZone.Auras.Count != 0;
        public static int Remaining(Spell spell) => Math.Max(0, spell[GameTag.QUEST_PROGRESS_TOTAL] - spell[GameTag.QUEST_PROGRESS]);
        public static bool DoublesMinionEnd(Controller owner) => owner.SecretZone.Auras.Any(a => a.Card.Id == "CATA_480");

        public static Spell Install(Spell spell)
        {
            if (!Definitions.TryGetValue(spell.Card.AssetId, out var definition))
                throw new ArgumentException("No timed Aura recipe: " + spell.Card.Id);
            if (IsActive(spell))
            {
                var original = spell;
                spell = (Spell)Entity.FromCard(spell.Controller, spell.Card,
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = original.Id, [Bonus] = original[Bonus] });
                PayloadRules.CopyState(original, spell);
                if (PayloadRules.ReferencesEntity(spell.Card, GameTag.TAG_SCRIPT_DATA_NUM_1))
                    spell[GameTag.TAG_SCRIPT_DATA_NUM_1] = PayloadRules.FirstReference(spell);
            }
            else if (spell.Zone != null) Generic.RemoveFromZone(spell.Controller, spell);
            spell[GameTag.QUEST_PROGRESS_TOTAL] = checked(definition.Ticks + spell[Bonus]);
            spell[GameTag.QUEST_PROGRESS] = 0;
            spell[LastTick] = -1;
            DisplayDuration(spell);
            spell.Controller.SecretZone.Add(spell);
            if (spell.Game.History) spell.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(spell));
            if (spell.ActivatedTrigger == null) spell.Power.Trigger.Activate(spell);
            if (spell.OngoingEffect == null) spell.Power.Aura?.Activate(spell);
            return spell;
        }

        /// <summary>Empower only this generated/active instance, retaining its normal counting side.</summary>
        public static void Extend(Spell spell, int ticks)
        {
            if (ticks <= 0 || !Definitions.TryGetValue(spell.Card.AssetId, out var definition))
                throw new ArgumentException("Duration extension requires an Aura and positive ticks.");
            spell[Bonus] = checked(spell[Bonus] + ticks);
            spell[GameTag.QUEST_PROGRESS_TOTAL] = IsActive(spell)
                ? checked(spell[GameTag.QUEST_PROGRESS_TOTAL] + ticks) : checked(definition.Ticks + spell[Bonus]);
            DisplayDuration(spell);
        }

        private static void DisplayDuration(Spell spell) =>
            spell[spell.Card.Id == "EDR_259e1" ? GameTag.TAG_SCRIPT_DATA_NUM_2 : GameTag.TAG_SCRIPT_DATA_NUM_1] = Remaining(spell);

        private static void Tick(Spell spell)
        {
            if (!IsActive(spell) || spell[LastTick] == spell.Game.Turn || Remaining(spell) == 0) return;
            spell[LastTick] = spell.Game.Turn;
            var definition = Definitions[spell.Card.AssetId];
            definition.Effect?.Process(spell.Game, spell.Controller, spell, null, new TaskStack());
            spell.Game.ProcessTasks();
            if (!IsActive(spell)) return;
            spell[GameTag.QUEST_PROGRESS]++;
            DisplayDuration(spell);
            if (definition.Clock == AuraSpellClock.OwnerStart && Remaining(spell) == 0) Expire(spell);
        }

        public static void ExpireEnd(Game game)
        {
            foreach (var spell in game.Player1.SecretZone.Auras.Concat(game.Player2.SecretZone.Auras).ToArray())
                if (Remaining(spell) == 0) Expire(spell);
        }
        private static void Expire(Spell spell)
        {
            spell.Controller.SecretZone.Remove(spell);
            PayloadRules.Clear(spell);
            spell[GameTag.ZONE_POSITION] = 0;
            spell.Controller.GraveyardZone.Add(spell);
        }

        internal sealed class CastWitness
        {
            internal int Cost;
            internal int[] Listeners;
            internal bool AfterCast;
        }
        internal static CastWitness BeginCast(Spell spell, HistoryCastKind kind)
        {
            if (!CardSets.Client.Sets.VeteranWarmedic.IsHoly(spell)) return null;
            var listeners = spell.Controller.SecretZone.Auras
                .Where(a => Definitions[a.Card.AssetId].HolyCast != null).Select(a => a.Id).ToArray();
            return listeners.Length == 0 ? null : new CastWitness { Listeners = listeners,
                AfterCast = kind == HistoryCastKind.Player,
                Cost = Math.Max(0, kind == HistoryCastKind.Player ? spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] : spell.Cost) };
        }
        internal static void CompleteCast(Spell spell, CastWitness witness)
        {
            if (witness == null) return;
            foreach (int id in witness.Listeners)
            {
                if (witness.AfterCast || Generic.SpellBodyPending(spell.Game))
                {
                    // Hand casts join ordered AFTER_CAST; effect/repeat casts have no such event.
                    var pending = PayloadRules.StoreValue(spell, witness.AfterCast ? AfterCastKey : PendingCastKey,
                        witness.Cost, (PayloadPolicy)0);
                    pending[PayloadRules.Recipient] = id;
                }
                else Notify(spell, id, witness.Cost);
            }
        }
        internal static void FlushCasts(Spell spell) => FlushCasts(spell, PendingCastKey);
        private static void FlushCasts(Spell spell, int key, int recipient = 0)
        {
            foreach (var pending in PayloadRules.Entries(spell, key))
            {
                int id = pending[PayloadRules.Recipient], cost = pending[PayloadRules.Value];
                if (recipient != 0 && recipient != id) continue;
                PayloadRules.Remove(pending);
                Notify(spell, id, cost);
            }
        }
        private static void Notify(Spell spell, int id, int cost)
        {
            if (!spell.Game.IdEntityDic.TryGetValue(id, out var listener) || !IsActive(listener)) return;
            var task = Definitions[listener.Card.AssetId].HolyCast;
            int previous = listener[CastCost];
            listener[CastCost] = cost;
            try { spell.Game.TaskQueue.Execute(task, listener.Controller, listener, spell, historyTrigger: true); }
            finally { listener[CastCost] = previous; }
            spell.Game.DeathProcessingAndAuraUpdate();
        }
    }
}
