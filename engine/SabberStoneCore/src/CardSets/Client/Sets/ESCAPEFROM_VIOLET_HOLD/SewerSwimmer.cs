using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SewerSwimmer
    {
        internal const int DbfId = 125934;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_TARGET_WITH_DEATHRATTLE, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion) || !minion.HasDeathrattle) return;
            if (!minion.IsSilenced) minion.ActivateTask(PowerActivation.DEATHRATTLE, fromDeath: false);
                if (minion.AppliedEnchantments == null) return;
                foreach (var enchant in minion.AppliedEnchantments.ToArray())
                    if (enchant.Power.DeathrattleTask != null)
                        g.TaskQueue.Enqueue(enchant.Power.DeathrattleTask, minion.Controller, minion, enchant, triggerKeyword: (int)GameTag.DEATHRATTLE);
            }) };
        }
    }
}
