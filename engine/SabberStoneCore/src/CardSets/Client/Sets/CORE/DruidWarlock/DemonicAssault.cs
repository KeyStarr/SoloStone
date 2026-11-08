using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DemonicAssault
    {
        internal const int DbfId = 117062;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            // Finish the spell text before death processing (Advanced rulebook, "Playing a spell"):
            // https://hearthstone.wiki.gg/wiki/Advanced_rulebook#Playing_a_spell
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(3, EntityType.TARGET, true),
                new SummonTask("CS2_065", 2)) };
        }
    }
}
