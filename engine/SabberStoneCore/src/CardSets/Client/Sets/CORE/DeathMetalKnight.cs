using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DeathMetalKnight
    {
        [StandardCard("CORE_ETC_523",117395)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.HealthCostCondition=Healed;
        }
        private static bool Healed(IPlayable source)=>source.Game.HistoryFacts.Query.HealthChanges(source.Game.HistoryFacts.Query.ThisTurn)
            .Any(f=>f.ControllerId==source.Controller.Id && f.Reason==HistoryHealthChangeReason.Heal && f.NewHealth>f.OldHealth);
    }
}
