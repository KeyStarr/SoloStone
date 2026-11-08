using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KingMaluk
    {
        [StandardCard("TIME_042", 120795)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var held in c.HandZone.ToArray()) Generic.DiscardBlock(c, held);
                Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId("TIME_042t"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
            }) };
        }
        [StandardCard("TIME_042t", 120794)]
        internal static void ConfigureBanana(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq,int> {
                [PlayReq.REQ_TARGET_TO_PLAY] = 0, [PlayReq.REQ_MINION_TARGET] = 0 });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (t is Minion minion && minion.Zone == minion.Controller.BoardZone && !minion.ToBeDestroyed)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_042te"), (IPlayable)s, minion);
                Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId("TIME_042t"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
            }) };
        }
        [StandardCard("TIME_042te", 120796)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(1)) };
        }
    }
}
