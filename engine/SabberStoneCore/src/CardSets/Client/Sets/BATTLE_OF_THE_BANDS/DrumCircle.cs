using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DrumCircle
    {
        [StandardCard("ETC_373", 94201)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        [StandardCard("ETC_373a", 97382)]
        internal static void Treants(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.Power = new Power { PowerTask = new SummonTask("ETC_373t", 5) };
        }
        [StandardCard("ETC_373b", 97383)]
        internal static void Buff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var minion in c.BoardZone.GetAll()) {
                    Generic.AddEnchantmentBlock(g, Cards.FromId("ETC_373be"), (IPlayable)s, minion);
                    Effects.TauntEff.ApplyTo(minion);
                }
            }) };
        }
        [StandardCard("ETC_373t", 91248)]
        internal static void Treant(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        [StandardCard("ETC_373be", 97385)]
        internal static void Enchantment(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(4)) };
        }
    }
}
