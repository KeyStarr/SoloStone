using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwilightTimehopper
    {
        internal const int DbfId = 120482;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx =>
                ctx.RunLegacy(new AddCardTo("TIME_025t", EntityType.DECK, 2))) };
        }

        internal static void ConfigureShred(Card card)
        {
            card.Implemented = true;
            // DrawBlock already handles burning and moving a drawn spell out of hand.
            card.Power = new Power {
                PowerTask = new DamageTask(3, EntityType.HERO, spellDmg: true),
                TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                    new PlayTask(PlayType.SPELL).Process(g, c, s, null,
                        new TaskStack { Playables = new[] { (IPlayable)s } });
                    new DrawTask().Process(g, c, s, null);
                })
            };
        }

        internal static bool CastShred(Game game, Controller controller, IEntity source)
        {
            var shreds = controller.DeckZone.Where(c => c.Card.Id == "TIME_025t").ToArray();
            if (shreds.Length == 0) return false;
            var stack = new TaskStack { Playables = shreds };
            new RandomTask(1, EntityType.STACK).Process(game, controller, source, null, stack);
            new PlayTask(PlayType.SPELL).Process(game, controller, source, null, stack);
            return true;
        }
    }
}
