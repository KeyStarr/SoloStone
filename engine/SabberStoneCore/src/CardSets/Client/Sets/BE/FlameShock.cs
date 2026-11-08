using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FlameShock
    {
        [StandardCard("BE_302", 130974)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> { {PlayReq.REQ_TARGET_TO_PLAY,0}, {PlayReq.REQ_ENEMY_TARGET,0} });
            var select = new SelectorSpec(SelectorSource.Board,SelectorSide.Enemy,includeHeroes:true,
                groupKey:"flame-shock",excludeEarlierPicks:true);
            card.Power = new Power { PowerTask = new DecisionSpellTask("b14-h2/flame-shock/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.PlayTargetOrBoard,SelectorSide.Enemy,includeHeroes:true,
                    groupKey:"flame-shock",excludeEarlierPicks:true)),
                Hit(), new CustomTask((g,c,s,t,stack) => g.DeathProcessingAndAuraUpdate()),
                new SelectEntityThenContinueTask(select), Hit()) };
        }
        private static ISimpleTask Hit() => new CustomTask((g,c,s,t,stack) => {
            int id = g.Resolution.CurrentEffect.LastSelectionEntityId;
            if (id != 0) new EffectContext(g,c.Id,s.Id,stack:stack).Damage(id,2,spellDamage:true);
        });
    }
}
