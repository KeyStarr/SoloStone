using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ConjurationSpecialist
    {
        internal const int DbfId = 123848;
        [StandardCard("CATA_979", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_979/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand, cardType: CardType.SPELL)),
                EffectContext.Task(context => context.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    context.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                        if (t.Zone != c.HandZone || t.Controller != c || !(t is Spell)) return;
                        int cost = Math.Max(0, CostRules.EffectiveCost((Playable)t));
                        // Use the existing private retirement shape; replacing a hand spell must not reveal it.
                        t[ShatterRules.PrivateHandOwner] = c.PlayerId;
                        foreach (var enchant in t.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>()) enchant.Remove();
                        c.SetasideZone.Add(c.HandZone.Remove(t));
                        t[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
                        if (g.History) g.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistoryHideEntity { EntityID = t.Id, Zone = Zone.REMOVEDFROMGAME });
                        var bound = new EffectContext(g, c.Id, s.Id, stack: new TaskStack { Number = cost });
                        for (int i = 0; i < 2; i++)
                        {
                            var chosen = bound.PickRandomCard(B14K3CataclysmPools.Spell(), PoolCostBinding.StackNumber, DbfId);
                            if (chosen != null) bound.AddToHand(chosen.Id);
                        }
                    }), context.LastChosenEntityId))) };
        }
    }
}
