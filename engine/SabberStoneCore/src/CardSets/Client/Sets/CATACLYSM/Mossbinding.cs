using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Mossbinding
    {
        internal const int DbfId = 123023;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                var summoned = new[] { ctx.Summon("CATA_135t", position), ctx.Summon("CATA_135t", position + 1) };
                int mana = Math.Max(0, c.RemainingMana);
                Generic.SpendMana(c, (IPlayable)s, mana, HistoryPaymentKind.Other, preserveZeroWrites: true);
                foreach (int entityId in summoned)
                    if (mana > 0 && entityId != 0 && g.IdEntityDic[entityId].Zone == c.BoardZone)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_135e"), (IPlayable)s, g.IdEntityDic[entityId], mana, mana, 0);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
