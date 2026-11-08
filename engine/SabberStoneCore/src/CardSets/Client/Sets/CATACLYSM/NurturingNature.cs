using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NurturingNature
    {
        internal const int DbfId = 123510;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 },
                { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_TARGET_WITH_RACE, (int)Race.BEAST } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new AddEnchantmentTask("MEND_305e", EntityType.TARGET).Process(g, c, s, t, stack);
                var beasts = c.HandZone.GetAll().Where(p => p is Minion && p.Card.IsRace(Race.BEAST)).ToList();
                if (beasts.Count == 0) return;
                var selection = new TaskStack { Playables = beasts };
                new RandomTask(1, EntityType.STACK).Process(g, c, s, t, selection);
                new AddEnchantmentTask("MEND_305e", EntityType.STACK).Process(g, c, s, t, selection);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
