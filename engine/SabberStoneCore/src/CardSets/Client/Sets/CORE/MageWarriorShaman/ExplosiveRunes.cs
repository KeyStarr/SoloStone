using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ExplosiveRunes
    {
        internal const int DbfId = 86235;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION)
            {
                SingleTask = new CustomTask((game, owner, source, target, stack) => {
                    if (!(target is Minion minion) || minion.Zone != minion.Controller.BoardZone || minion.IsDead || minion.Untouchable) return;
                    // Scale once using the secret owner's bonuses, then split before prevention.
                    int amount = (6 + SabberStoneCore.Actions.SpellDamageRules.Bonus((IPlayable)source)) * (int)Math.Pow(2, owner.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
                    int hit = Math.Min(amount, minion.Health);
                    minion.TakeDamage((IPlayable)source, hit);
                    if (amount > hit) minion.Controller.Hero.TakeDamage((IPlayable)source, amount - hit);
                    new SetGameTagTask(GameTag.REVEALED, 1, EntityType.SOURCE).Process(game, owner, source, target, stack);
                    new MoveToGraveYard(EntityType.SOURCE).Process(game, owner, source, target, stack);
                })
            } };
        }
    }
}
