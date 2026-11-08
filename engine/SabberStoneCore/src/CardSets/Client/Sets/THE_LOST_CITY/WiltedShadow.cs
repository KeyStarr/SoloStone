using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Conditions;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WiltedShadow
    {
        internal const int DbfId = 117566;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.HEAL_INTENT) {
                TriggerSource = TriggerSource.ENEMY,
                Condition = new SelfCondition(target => target.Game.CurrentEventData.EventSource.Controller == target.Controller.Opponent),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion shadow) || shadow.IsDead || shadow.Zone.Type != Zone.PLAY
                        || c.Hero.ToBeDestroyed || c.Opponent.Hero.ToBeDestroyed
                        || shadow.IsSilenced || !(t is ICharacter victim) || victim.IsDead
                        || t is Minion minion && (minion.Zone.Type != Zone.PLAY || minion.Untouchable)) return;
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, t, stack);
                    g.DeathProcessingAndAuraUpdate();
                })
            } };
        }

    }
}
