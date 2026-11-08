using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FacelessReplicator
    {
        [StandardCard("CATA_185", 123779)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.CaptureDeath] = 1;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (g.IdEntityDic.TryGetValue(s[DeathContextRules.FatalSource], out var killer)
                    && killer is Minion minion && minion.Zone == minion.Controller.BoardZone && !minion.ToBeDestroyed)
                    new TransformTask("CATA_185", EntityType.TARGET, clearNativeKeywords: true)
                        .Process(g, minion.Controller, s, minion, stack);
            }) };
        }
    }
}
