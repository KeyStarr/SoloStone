using System.Linq;
using System.Collections.Generic;
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
    internal static class LoGoshsLastStand
    {
        internal const int DbfId = 123382;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_610e", EntityType.TARGET) };
        }

        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var candidates = c.HandZone.Where(p => p is Minion).ToArray();
                if (candidates.Length == 0) return;
                var choice = new TaskStack { Playables = candidates };
                new RandomTask(1, EntityType.STACK).Process(g, c, s, t, choice);
                var minion = (Minion)choice.Playables[0];
                int position = EffectContext.DeathrattlePosition(c, s);
                minion.Zone.Remove(minion);
                Generic.SummonBlock(g, minion, position, s);
            }) };
        }
    }
}
