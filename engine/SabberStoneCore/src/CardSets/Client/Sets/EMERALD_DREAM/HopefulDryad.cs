using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HopefulDryad
    {
        [StandardCard("EDR_001", 113328)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            Card[] dreams = { ClientOverlay.Snapshot("DREAM_01"), ClientOverlay.Snapshot("DREAM_02"),
                ClientOverlay.Snapshot("DREAM_03"), ClientOverlay.Snapshot("DREAM_04"), ClientOverlay.Snapshot("DREAM_05") };
            foreach (var dream in dreams) dream.UseBehaviourFrom(Cards.FromId(dream.Id));
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                Generic.AddHandPhase(c, Entity.FromCard(c, dreams[g.Random.Next(dreams.Length)],
                    new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
                g.OnRandomHappened(true);
            }) };
        }
    }
}
