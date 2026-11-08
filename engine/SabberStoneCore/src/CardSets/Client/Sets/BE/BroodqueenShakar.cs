using System;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BroodqueenShakar
    {
        internal const int DbfId = 130117;
        private const int Required = 5;
        // "5 friendly minions died this turn": MinionDiedFact (minions only, never locations) in the current global
        // turn window, owned by this controller at death.
        private static int Deaths(HistoryQuery history, int controllerId) =>
            history.Deaths(history.ThisTurn).Count(f => f.ControllerId == controllerId);
        // Printed TAG_SCRIPT_DATA_NUM_1 = 5 is the "({0} left!)" value; 0 means ready.
        private static int Left(IPlayable card) =>
            Math.Max(0, Required - Deaths(card.Game.HistoryFacts.Query, card.Controller.Id));

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Left);
            card.PoweredUpWhen(IsReady);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx =>
                IsReady(ctx.History, ctx.ControllerId)
                    ? ctx.RunLegacy(new AddEnchantmentTask("BE_565e", EntityType.CONTROLLER))
                    : TaskState.COMPLETE) };
        }

        /// <summary>BE_565e: player enchantment (survives the source) whose restless board aura re-checks Rush,
        /// so losing Rush (Silence) also drops the Charge and re-applies exhaustion.</summary>
        internal static void ConfigureRage(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new Aura(AuraType.BOARD, "BE_565e2") {
                Condition = SelfCondition.HasRush, Restless = true } };
        }

        internal static void ConfigureCharge(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Charge) };
        }

        private static bool IsReady(IPlayable source) => IsReady(source.Game.HistoryFacts.Query, source.Controller.Id);
        private static bool IsReady(HistoryQuery history, int controllerId) => Deaths(history, controllerId) >= Required;
    }
}
