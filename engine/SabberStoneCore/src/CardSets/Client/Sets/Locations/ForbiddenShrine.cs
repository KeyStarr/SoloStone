using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForbiddenShrine
    {
        internal const int DbfId = 114293;
        [StandardCard("EDR_520", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Task = new CustomTask((g, c, s, t, stack) => {
                    int mana = Math.Max(0, c.RemainingMana);
                    Generic.SpendMana(c, (IPlayable)s, mana, HistoryPaymentKind.Other);
                    var context = new EffectContext(g, c.Id, s.Id, stack: new TaskStack { Number = mana });
                    var chosen = context.PickRandomCard(B14K3EmeraldDreamPools.Spell(), PoolCostBinding.StackNumber, DbfId);
                    if (chosen == null) return;
                    var spell = Entity.FromCard(c, chosen, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
                    new PlayTask(PlayType.SPELL, true).Process(g, c, s, null, new TaskStack { Playables = new[] { spell } });
                })
            } };
        }
    }
}
