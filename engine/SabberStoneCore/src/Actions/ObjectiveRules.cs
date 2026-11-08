using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Objectives;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.Actions
{
    public static class ObjectiveRules
    {
        private sealed class Definition
        {
            internal int Turns;
            internal bool OwnerEnd, EveryTick;
            internal ISimpleTask Effect;
        }
        private static readonly Dictionary<int, Definition> Definitions = new Dictionary<int, Definition>();
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 32900 && (int)tag <= 32902;
        public static bool IsObjective(IPlayable card) => card is Spell && Definitions.ContainsKey(card.Card.AssetId);
        public static bool IsActive(IPlayable card) => IsObjective(card) && card.Zone == card.Controller.SecretZone;
        public static int CompletedRituals(Controller owner) => owner[ObjectiveState.CompletedRituals];
        public static int Remaining(IPlayable actor) => Math.Max(0, actor[GameTag.QUEST_PROGRESS_TOTAL] - actor[GameTag.QUEST_PROGRESS]);
        public static void Declare(Card card, int turns, ISimpleTask effect, bool ownerEnd = false, bool everyTick = false)
        {
            if (turns < 1 || card.Type != CardType.SPELL || card[(GameTag)2311] != 1)
                throw new ArgumentException("Objective needs exported identity and positive duration.");
            Definitions.Add(card.AssetId, new Definition { Turns = turns, OwnerEnd = ownerEnd, EveryTick = everyTick, Effect = effect });
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => Install((Spell)s)) };
            if (ownerEnd) card.Power.Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerSource = TriggerSource.ALL,
                SingleTask = new CustomTask((g,c,s,t,stack) => Tick((Spell)s))
            };
        }
        internal static void CopyComposition(IPlayable source, IPlayable destination)
        {
            if (!source.Card.Id.StartsWith("CAP_405tb", StringComparison.Ordinal)) return;
            destination[GameTag.MODULAR_ENTITY_PART_1] = source[GameTag.MODULAR_ENTITY_PART_1];
            destination[GameTag.MODULAR_ENTITY_PART_2] = source[GameTag.MODULAR_ENTITY_PART_2];
        }
        public static Spell Install(Spell actor)
        {
            if (!Definitions.TryGetValue(actor.Card.AssetId, out var definition)) throw new ArgumentException("Unknown objective.");
            if (IsActive(actor))
            {
                var original = actor;
                actor = (Spell)Entity.FromCard(original.Controller, original.Card,
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = original.Id });
                PayloadRules.CopyState(original, actor);
                CopyComposition(original, actor);
            }
            else if (actor.Zone != null) Generic.RemoveFromZone(actor.Controller, actor);
            actor[Generic.PrivateDecisionOfferTag] = 0;
            actor[GameTag.QUEST_PROGRESS] = 0;
            actor[GameTag.QUEST_PROGRESS_TOTAL] = definition.Turns;
            actor[GameTag.TAG_SCRIPT_DATA_NUM_1] = definition.Turns;
            actor[ObjectiveState.LastTick] = definition.OwnerEnd ? -1 : actor.Game.Turn;
            actor.Controller.SecretZone.Add(actor);
            if (actor.Game.History) actor.Game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(actor));
            if (actor.ActivatedTrigger == null) actor.Power.Trigger?.Activate(actor);
            return actor;
        }
        private static void Tick(Spell actor)
        {
            if (!IsActive(actor) || actor.Controller != actor.Game.CurrentPlayer || actor[ObjectiveState.LastTick] == actor.Game.Turn) return;
            actor[ObjectiveState.LastTick] = actor.Game.Turn;
            actor[GameTag.QUEST_PROGRESS]++;
            actor[GameTag.TAG_SCRIPT_DATA_NUM_1] = Remaining(actor);
            var definition = Definitions[actor.Card.AssetId];
            bool due = Remaining(actor) == 0;
            if (due)
            {
                actor.Controller.SecretZone.Remove(actor);
                actor[GameTag.ZONE_POSITION] = 0;
                actor.Controller.GraveyardZone.Add(actor);
                if (actor.Card[(GameTag)4960] != 0) actor.Controller[ObjectiveState.CompletedRituals]++;
            }
            if (due || definition.EveryTick)
                actor.Game.TaskQueue.Execute(definition.Effect, actor.Controller, actor, null, historyTrigger: true);
        }
        // Freeze each actor by LastTick before executing it; newly installed clocks start next turn.
        // shortcut: objectives precede other start listeners; revisit when retail evidence pins interleaving.
        internal static bool Start(Controller owner)
        {
            foreach (var actor in owner.SecretZone.Where(IsActive).OrderBy(s => s.OrderOfPlay).ToArray())
            {
                if (Definitions[actor.Card.AssetId].OwnerEnd) continue;
                Tick(actor);
                if (owner.Game.Resolution.HasSuspendedEffect)
                {
                    owner.Game[ObjectiveState.PendingStart] = 1;
                    return false;
                }
                owner.Game.DeathProcessingAndAuraUpdate();
                if (owner.Game.State == State.COMPLETE) return false;
            }
            return true;
        }
        internal static void Resume(Game game)
        {
            if (game[ObjectiveState.PendingStart] == 0 || game.Resolution.HasSuspendedEffect || game.State == State.COMPLETE) return;
            game[ObjectiveState.PendingStart] = 0;
            game.MainStartTriggers();
        }
    }
}
