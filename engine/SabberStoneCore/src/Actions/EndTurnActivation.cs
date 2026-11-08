using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public static class EndTurnActivation
    {
        private static IEnumerable<Trigger> Effects(Minion minion)
        {
            if (minion.ActivatedTrigger?.HasEndTurnEffect == true) yield return minion.ActivatedTrigger;
            if (minion.AppliedEnchantments == null) yield break;
            foreach (var actor in minion.AppliedEnchantments)
                if (actor.ActivatedTrigger?.HasEndTurnEffect == true) yield return actor.ActivatedTrigger;
        }

        public static void RandomFriendly(Controller controller)
        {
            var sources = controller.BoardZone.Where(m => !m.IsDormant && !m.IsDead && Effects(m).Any()).ToArray();
            if (sources.Length == 0) return;
            var chosen = sources[controller.Game.Random.Next(sources.Length)];
            controller.Game.OnRandomHappened(true);
            foreach (var effect in Effects(chosen).ToArray()) effect.ActivateEndTurnEffect();
        }
    }
}
