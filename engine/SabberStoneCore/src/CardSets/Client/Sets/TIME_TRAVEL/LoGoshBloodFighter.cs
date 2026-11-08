using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LoGoshBloodFighter
    {
        internal const int DbfId = 119451;
        [StandardCard("TIME_850", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = Deathrattle(0) };
        }

        [StandardCard("TIME_850t", 119453)]
        internal static void ConfigureBroll(Card card)
        { card.Implemented = true; card.Power = new Power { DeathrattleTask = Deathrattle(1) }; }
        [StandardCard("TIME_850t1", 119454)]
        internal static void ConfigureValeera(Card card)
        { card.Implemented = true; card.Power = new Power { DeathrattleTask = Deathrattle(2) }; }
        [StandardCard("TIME_850e", 119452)]
        internal static void ConfigureBuff(Card card)
        { card.Implemented = true; card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(5)) }; }
        private static CustomTask Deathrattle(int instruction) => new CustomTask((g, c, s, t, stack) => {
            if (c.BoardZone.IsFull) return;
            var members = c.HandZone.Where(p => p is Minion &&
                (p.Card.Id == "TIME_850" || p.Card.Id == "TIME_850t" || p.Card.Id == "TIME_850t1")).ToArray();
            if (members.Length == 0) return;
            var chosen = new TaskStack { Playables = members };
            new RandomTask(1, EntityType.STACK).Process(g, c, s, t, chosen);
            var minion = (Minion)chosen.Playables[0];
            int position = EffectContext.DeathrattlePosition(c, s);
            minion.Zone.Remove(minion);
            Generic.SummonBlock(g, minion, position, s);
            var ctx = new EffectContext(g, c.Id, s.Id);
            ctx.GiveBuff(minion.Id, "TIME_850e");
            if (instruction == 1) new Effect(GameTag.TAUNT, EffectOperator.SET, 1).ApplyTo(minion);
            else if (instruction == 2)
            {
                minion.CantBeTargetedBySpells = true;
            }
            else if (minion.Zone == c.BoardZone && !minion.IsDead)
            {
                var targets = new TaskStack();
                new IncludeTask(EntityType.ENEMIES).Process(g, c, s, null, targets);
                targets.Playables = targets.Playables.Where(p => p is ICharacter character && !character.IsDead && !(p is Minion m && m.Untouchable)).ToArray();
                if (targets.Playables.Count == 0) return;
                new RandomTask(1, EntityType.STACK).Process(g, c, s, null, targets);
                new AttackTask(EntityType.SOURCE, EntityType.STACK).Process(g, c, minion, null, targets);
            }
        });
    }
}
