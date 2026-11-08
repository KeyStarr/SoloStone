using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LethalRecipe
    {
        internal const int DbfId = 126205;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasTenMana);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool buff = HasTenMana((IPlayable)s);
                var drawn = new TaskStack();
                ComplexTask.Create(new IncludeTask(EntityType.DECK), new FilterStackTask(SelfCondition.IsMinion),
                    new RandomTask(2, EntityType.STACK)).Process(g, c, s, t, drawn);
                if (drawn.Playables == null) return;
                foreach (var minion in drawn.Playables)
                {
                    int index = c.DeckZone.ToList().IndexOf(minion);
                    // Explicit-entity Draw suppresses draw triggers; indexed Draw preserves them.
                    if (index >= 0) Generic.Draw(c, index);
                }
                if (!buff) return;
                foreach (var minion in drawn.Playables)
                    if (minion.Zone == c.HandZone) new EffectContext(g, c.Id, s.Id).GiveBuff(minion.Id, "JAIL_866e");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3), Effects.Health_N(3)) };
        }
        private static bool HasTenMana(IPlayable source) =>
            source.Controller.BaseMana >= 10;

    }
}
