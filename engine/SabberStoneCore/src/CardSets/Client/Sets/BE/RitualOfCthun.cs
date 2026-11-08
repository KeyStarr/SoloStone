using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RitualOfCthun
    {
        [StandardCard("BE_726", 130281)]
        internal static void Configure(Card card) => ObjectiveRules.Declare(card, 2,
            CthunRules.ExternalAbility("client/BE_726/due/v1"));
    }
}
