using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AlternateReality
    {
        [StandardCard("TIME_707",122833)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=> {
                var held=c.HandZone.ToArray(); var deck=c.DeckZone.ToArray();
                foreach(var old in held.Concat(deck)) {
                    bool hand=old.Zone==c.HandZone;
                    var picked=TimeTravelPastPools.Pick(g,c,s,"TIME_707",hand ? "choose-one" : "choose-one-deck");
                    if(picked==null) continue;
                    old[CardStateBoundary.ReplacedHiddenZone]=(int)old.Zone.Type;
                    c.SetasideZone.Add(old.Zone.Remove(old));
                    var replacement=Entity.FromCard(c,picked,new EntityData { [GameTag.CREATOR]=s.Id,[GameTag.DISPLAYED_CREATOR]=s.Id });
                    CostRules.AddModifier((Playable)replacement,EffectOperator.SUB,1);
                    if(hand) Generic.AddHandPhase(c,replacement);
                    else Generic.ShuffleIntoDeck(c,s,replacement);
                }
            }) };
        }
    }
}
