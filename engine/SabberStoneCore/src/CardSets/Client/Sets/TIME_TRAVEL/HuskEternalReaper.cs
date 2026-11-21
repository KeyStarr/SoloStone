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
    internal static class HuskEternalReaper
    {
        [StandardCard("TIME_618", 121000)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (c.AppliedEnchantments?.Any(e => e.Card.Id == "TIME_618e") == true) return;
                Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("TIME_618e"));
                // The client draws the hero Deathrattle skull from the hero's PLAYER_DEATHRATTLE tag, whose value is
                // the entity id of the set-aside "Eternal Life" card it shows on hover.
                var life = Entity.FromCard(c, Cards.FromId("TIME_618t"),
                    new EntityData { [GameTag.CREATOR] = s.Id }, zone: c.SetasideZone);
                c.Hero[CorpseRules.PlayerDeathrattle] = life.Id;
            }) };
        }
        [StandardCard("TIME_618e", 126656)]
        internal static void ConfigureGrant(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        [StandardCard("TIME_618t", 123094)]
        internal static void ConfigureToken(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        [StandardCard("TIME_618e2", 121002)]
        internal static void ConfigureResurrection(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
