using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Stormbrewer
    {
        internal const int DbfId = 117885;
        [StandardCard("TLC_107", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g,c,s,t,stack) => { if (KindredRules.BeginMatchedClause((IPlayable)s) > 0) ((Minion)s).IsRush = true; }),
                Trigger = new Trigger(TriggerType.ATTACK) { TriggerSource = TriggerSource.SELF,
                    SingleTask = new CustomTask((g,c,s,t,stack) => {
                        var defender = g.CurrentEventData?.EventTarget as ICharacter;
                        if (defender != null) defender.TakeDamage((IPlayable)s,3);
                        g.DeathProcessingAndAuraUpdate();
                    }) } };
        }
    }
}
