using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VoidBlast
    {
        [StandardCard("JAIL_891", 127157)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = ComplexTask.Create(new DamageTask(3, EntityType.TARGET, true),
                new CustomTask((g, c, s, t, stack) => {
                    if (t is Minion minion && minion.IsDead) new AddCardTo("JAIL_732", EntityType.HAND).Process(g, c, s, t, stack);
                })) };
        }
    }
}
