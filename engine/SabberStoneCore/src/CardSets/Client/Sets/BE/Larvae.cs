using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Larvae
    {
        // Fixed exported family, not collectible generation. Teal is a placeholder, not an eligible power.
        private static readonly string[] Tokens = { "BE_720t1", "BE_720t2", "BE_720t3", "BE_720t4", "BE_720t5",
            "BE_720t6", "BE_720t7", "BE_720t8", "BE_720t9", "BE_720t10", "BE_720t11" };
        internal static void Generate(Controller controller, IEntity source, int count)
        {
            var pool = Tokens.Where(id => Cards.FromId(id).Class != controller.HeroClass).ToArray();
            for (int i = 0; i < count; i++)
            {
                string id = pool.Choose(controller.Game.Random);
                controller.Game.OnRandomHappened(true);
                new EffectContext(controller.Game, controller.Id, source.Id).AddToHand(id);
            }
        }
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            if (card.Class == CardClass.MAGE || card.Class == CardClass.PRIEST)
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { [PlayReq.REQ_TARGET_FOR_COMBO] = 0 });
            card.Power = new Power { ComboTask = new CustomTask((g, c, s, t, stack) =>
                BasicHeroPowerRules.Trigger(c, s, s.Card.Class, t as ICharacter)) };
        }
    }
}
