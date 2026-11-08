using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrystalCluster
    {
        [StandardCard("DEEP_028", 102432)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                int gained = Math.Min(3, Math.Max(0, c[GameTag.MAXRESOURCES] - c.BaseMana));
                Generic.ChangeManaCrystal(c, gained, false);
                if (gained < 3) new SummonTask("DEEP_028t", 3 - gained).Process(g,c,s,t,stack);
            }) };
        }
        [StandardCard("DEEP_028t", 102433)]
        internal static void Elemental(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
