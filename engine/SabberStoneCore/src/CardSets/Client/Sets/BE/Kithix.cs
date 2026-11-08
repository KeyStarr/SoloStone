using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Kithix
    {
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION, rarity: Rarity.LEGENDARY);
        [OfflinePool("BE_090", 129824)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("sinister-legendary-minions", Filter(), new[] { "WARLOCK" }, destination: PoolDestination.Summon) };
        private static bool Ready(IPlayable card) => ObjectiveRules.CompletedRituals(card.Controller) > 0;
        [StandardCard("BE_090", 129824)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(Ready);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_090/v1",
                new ConditionTask(EntityType.SOURCE, new SelfCondition(p => ObjectiveRules.CompletedRituals(p.Controller) > 0)),
                new FlagTask(false, EffectContext.Task(_ => TaskState.STOP)),
                new DiscoverThenContinueTask("BE_090t1", "BE_090t2", "BE_090t3"),
                new CustomTask((g,c,s,t,stack) => ObjectiveRules.Install((Spell)stack.Playables.Last()))) };
        }
        [StandardCard("BE_090t1", 130669)]
        internal static void Famine(Card card) => ObjectiveRules.Declare(card, 2, new CustomTask((g,c,s,t,stack) => {
            for (int i=0;i<5 && g.State != State.COMPLETE;i++) { Generic.DrawFrom(c,c.Opponent);g.DeathProcessingAndAuraUpdate(); }
        }));
        [StandardCard("BE_090t2", 130670)]
        internal static void Subjugation(Card card) => ObjectiveRules.Declare(card, 2, new CustomTask((g,c,s,t,stack) => {
            var pool = OfflineCardPools.Candidates(g,c.HeroClass,Filter(),129824, PoolDestination.Summon);
            if (pool.Count == 0) return;
            var selected = Enumerable.Range(0,3).Select(_ => pool[g.Random.Next(pool.Count)]).ToArray();
            foreach (var chosen in selected) {
                new SummonTask(chosen.Id).Process(g,c,s,t,stack);
                g.DeathProcessingAndAuraUpdate();
            }
            foreach (var chosen in selected) {
                var copy = Generic.DrawCard(c, chosen);
                if (copy?.Zone == c.HandZone) Generic.AddEnchantmentBlock(g,Cards.FromId("BE_090e2"),(IPlayable)s,copy);
            }
        }));
        [StandardCard("BE_090t3", 130671)]
        internal static void Conquest(Card card) => ObjectiveRules.Declare(card, 2, new CustomTask((g,c,s,t,stack) => {
            foreach (var minion in c.BoardZone.GetAll().Cast<IPlayable>().Concat(c.HandZone).Concat(c.DeckZone).OfType<Minion>().ToArray())
                Generic.AddEnchantmentBlock(g,Cards.FromId("BE_090e1"),(IPlayable)s,minion);
        }));
        [StandardCard("BE_090e1", 130683)]
        internal static void Buff(Card card) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(6),Effects.Health_N(6)) }; }
        [StandardCard("BE_090e2", 130759)]
        internal static void Discount(Card card) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Enchant=new Enchant(Effects.SetCost(1)) }; }
    }
}
