using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IllidariInquisitor
    {
        internal const int DbfId = 66176;
        private static bool CanFollow(IPlayable source) => source is Minion body && !body.IsDead
            && body.Zone == body.Controller.BoardZone && !body.Untouchable
            && source.Game.CurrentEventData?.EventTarget is ICharacter defender && !defender.IsDead
            && defender.Controller != body.Controller
            && (!(defender is Minion minion) || minion.Zone == body.Controller.Opponent.BoardZone && !minion.Untouchable);
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                OwnerCondition = new SelfCondition(CanFollow),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // Earlier after-attack reactions can invalidate an already queued follow-up.
                    if (!CanFollow((IPlayable)s)) return;
                    var defender = g.CurrentEventData.EventTarget;
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, defender);
                })
            } };
        }
    }
}
