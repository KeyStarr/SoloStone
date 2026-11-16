using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    // Forgetful, "50% chance to attack the wrong enemy" (https://hearthstone.wiki.gg/wiki/Forgetful): when the character
    // attacks, it has a 50% chance to attack an enemy that is not the intended target, if such an alternate target exists,
    // chosen at random among all other enemy characters (heroes and minions; Stealth and Immune ones too). It runs in the
    // attack step with Secrets (an on-attack trigger), in the order the cards came into play.
    public static class ForgetfulRules
    {
        public static Trigger Trigger() => new Trigger(TriggerType.ATTACK) {
            TriggerSource = TriggerSource.SELF,
            SingleTask = new CustomTask((game, controller, source, target, stack) => {
                var current = game.CurrentEventData?.EventTarget as ICharacter;
                if (current == null) return;
                var others = new List<ICharacter>();
                if (controller.Opponent.Hero != current) others.Add(controller.Opponent.Hero);
                others.AddRange(controller.Opponent.BoardZone.Where(m => m != current && !m.Untouchable && !m.IsDead));
                if (others.Count == 0) return;
                game.OnRandomHappened(true);
                if (game.Random.Next(2) == 0) return;
                var wrong = others[game.Random.Next(others.Count)];
                game.ProposedDefender = wrong.Id;
                game.CurrentEventData.EventTarget = wrong;
            })
        };
    }
}
