using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Chromatus
    {
        internal const int DbfId = 122401;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_432t1", "CATA_432t2", "CATA_432t3", "CATA_432t4");
            card.Power = new Power();
        }

        internal static void ConfigureHead(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            string id = card.Id;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var body = ColossalRules.BodyOf((Minion)s);
                if (body == null) return;
                switch (id)
                {
                    case "CATA_432t1": body.HasTaunt = false; break;
                    case "CATA_432t2": body.HasLifeSteal = false; break;
                    case "CATA_432t3": body.CantBeTargetedBySpells = false; break;
                    case "CATA_432t4": body.HasDivineShield = false; break;
                }
            }) };
        }
    }
}
