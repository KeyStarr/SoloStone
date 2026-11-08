using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Cannonmaster
    {
        internal const int DbfId = 127099;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddCardTo("CAP_107t", EntityType.HAND) };
        }

        internal static void Fire(Minion cannon)
        {
            int shots = 1 + cannon.Controller.BoardZone.GetAll().Count(m => m.Card.Id == "CAP_106"
                && !m.IsSilenced && !m.IsDormant && !m.IsDead);
            for (int i = 0; i < shots; i++)
            {
                var stack = new TaskStack();
                if (new RandomTask(1, EntityType.ENEMIES).Process(cannon.Game, cannon.Controller, cannon, null, stack) == TaskState.STOP) return;
                var target = (ICharacter)stack.Playables[0];
                Generic.DamageCharFunc(cannon, target, 1, false);
                cannon.Game.DeathProcessingAndAuraUpdate();
            }
        }

        internal static void ConfigureCannoneer(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => Fire((Minion)s))
            } };
        }
    }
}
