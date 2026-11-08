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
    internal static class FireBreath
    {
        internal const int DbfId = 118480;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new DamageTask(4, EntityType.TARGET, true).Process(g, c, s, t, stack);
                g.DeathProcessingAndAuraUpdate();
                foreach (var minion in c.BoardZone.GetAll().Where(m => m.IsRace(Race.ELEMENTAL)).ToArray())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("DINO_406e"), (IPlayable)s, minion);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
