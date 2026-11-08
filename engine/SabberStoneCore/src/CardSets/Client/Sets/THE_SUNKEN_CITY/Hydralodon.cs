using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Hydralodon
    {
        [StandardCard("TSC_950", 72935)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "TSC_950t", "TSC_950t2");
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,k) => {
                foreach (var head in c.BoardZone.Where(IsHead)) Effects.Rush.ApplyTo(head);
            }) };
        }

        [StandardCard("TSC_950t", 72937)]
        [StandardCard("TSC_950t2", 85857)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,k) => {
                if (!c.BoardZone.Any(m => m.Card.Id == "TSC_950" && !m.IsDead && !m.IsDormant)) return;
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
                    new EffectContext(g,c.Id,s.Id).Summon("TSC_950t", Math.Min(((Minion)s).LastBoardPosition + i, c.BoardZone.SlotCount));
            }) };
        }
        internal static bool IsHead(Minion m) => m.Card.Id == "TSC_950t" || m.Card.Id == "TSC_950t2";
    }
}
