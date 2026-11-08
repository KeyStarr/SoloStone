using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Blasphemy
    {
        internal const int DbfId = 129802;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(WillBeHighestCost);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var cast = ctx.History.Casts().Last(f => f.EntityId == s.Id && f.Outcome == HistoryCastOutcome.Started);
                int cost = cast.CasterKind == HistoryCastKind.Player ? s[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] : ((IPlayable)s).Cost;
                bool repeat = IsHighestCost((IPlayable)s, cost);
                new DamageTask(2, EntityType.ENEMIES, true).Process(g, c, s, t, stack);
                if (repeat) new DamageTask(2, EntityType.ENEMIES, true).Process(g, c, s, t, stack);
            }) };
        }

        private static bool WillBeHighestCost(IPlayable source)
        {
            int cost = source.Cost;
            var quote = CostRules.Quote(source.Controller, source);
            if (quote.Rules.Length == 0 && !source.Game.Auras.OfType<Aura>()
                .Any(a => a.RemoveTrigger.Type == TriggerType.CAST_SPELL))
                return IsHighestCost(source, cost);

            // Consume shared discounts on a clone so glow leaves live costs, auras and history untouched.
            var game = source.Game.CloneExact();
            var projected = game.IdEntityDic[source.Id];
            CostRules.ConsumeRules(projected.Controller, CostRules.Quote(projected.Controller, projected));
            projected.Controller.SetasideZone.Add(projected.Controller.HandZone.Remove(projected));
            foreach (var aura in game.Auras.OfType<Aura>().Where(a => a.RemoveTrigger.Type == TriggerType.CAST_SPELL
                && (a.RemoveTrigger.Condition == null || a.RemoveTrigger.Condition.Eval(projected))).ToArray())
                aura.Remove();
            game.AuraUpdate();
            return IsHighestCost(projected, cost);
        }

        private static bool IsHighestCost(IPlayable source, int cost) => source.Controller.HandZone
            .All(held => held.Id == source.Id || held.Cost <= cost);
    }
}
