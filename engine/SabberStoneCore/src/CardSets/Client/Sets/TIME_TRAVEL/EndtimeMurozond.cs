using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EndtimeMurozond
    {
        internal const int DbfId=121203;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k) => {
                var context=new EffectContext(g,c.Id,s.Id);
                // Snapshot slots: summon reactions must not extend this fill into an unbounded loop.
                int slots=c.BoardZone.FreeSpace;
                for(int i=0;i<slots;i++)
                {
                    if(c.BoardZone.IsFull) break;
                    var chosen=context.PickRandomCard(TimeTravelPools.MurozondDragon(),recipeDbfId:DbfId);
                    if(chosen==null) break;
                    context.Summon(chosen.Id);
                }
                context.Heal(c.Hero.Id,c.Hero.Damage);
                TurnQueueRules.Prepend(g,c.Opponent,c.Opponent,c);
            }) };
        }
    }
}
