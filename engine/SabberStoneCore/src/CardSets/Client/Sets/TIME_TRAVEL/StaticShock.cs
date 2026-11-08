using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StaticShock
    {
        internal const int DbfId = 121020;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(1, EntityType.TARGET, spellDmg: true),
                new AddEnchantmentTask("TIME_218e", EntityType.HERO)) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) { IsOneTurnEffect = true } };
        }
    }
}
