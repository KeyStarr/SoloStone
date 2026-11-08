using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EliseTheNavigator
    {
        [StandardCard("TLC_100", 117762)]
        internal static void Configure(Card card)
        {
            Supported(card);
            card.PoweredUpWhen(IsEligible);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("navigator/craft/v1",
                new DiscoverThenContinueTask(Bases), new CustomTask(SaveBase),
                new DiscoverThenContinueTask(FirstAbilities), new CustomTask(SaveFirst),
                new DiscoverThenContinueTask(SecondAbilities), new CustomTask(Finish)) };
        }
        private static bool IsEligible(IPlayable p) => CompositeLocationRules.Eligible(p.Controller);
        private static void Supported(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        private static Card[] Bases(Game g, Controller c, IEntity s, TaskStack stack)
        {
            stack.Number = stack.Number1 = 0;
            return CompositeLocationRules.Eligible(c) ? new[] { Cards.FromId("TLC_100t1"), Cards.FromId("TLC_100t2"), Cards.FromId("TLC_100t3") } : Array.Empty<Card>();
        }
        private static void SaveBase(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => stack.Number = stack.Playables?.LastOrDefault()?.Id ?? 0;
        private static Card[] FirstAbilities(Game g, Controller c, IEntity s, TaskStack stack) => Abilities(g, stack, false);
        private static Card[] SecondAbilities(Game g, Controller c, IEntity s, TaskStack stack) => Abilities(g, stack, true);
        private static Card[] Abilities(Game g, TaskStack stack, bool second)
        {
            if (stack.Number == 0) return Array.Empty<Card>();
            string body = g.IdEntityDic[stack.Number].Card.Id;
            string excluded = second && stack.Number1 != 0 ? g.IdEntityDic[stack.Number1].Card.Id : null;
            return AssembleRules.Take(g, CompositeLocationRules.Components(body.Last() - '0')
                .Where(id => id != excluded).Select(Cards.FromId).ToList(), 3);
        }
        private static void SaveFirst(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => stack.Number1 = stack.Playables?.LastOrDefault()?.Id ?? 0;
        private static void Finish(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            var last = stack.Playables?.LastOrDefault();
            if (stack.Number == 0 || stack.Number1 == 0 || last == null) return;
            var definition = CompositeLocationRules.Compose(g.IdEntityDic[stack.Number].Card, g.IdEntityDic[stack.Number1].Card, last.Card);
            Generic.AddHandPhase(c, Entity.FromCard(c, definition, new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id }));
        }
        [StandardCard("TLC_100t1", 117761)]
        internal static void Base1(Card card) => CompositeLocationRules.ConfigureBase(card);
        [StandardCard("TLC_100t2", 117760)]
        internal static void Base2(Card card) => CompositeLocationRules.ConfigureBase(card);
        [StandardCard("TLC_100t3", 117759)]
        internal static void Base3(Card card) => CompositeLocationRules.ConfigureBase(card);
        [StandardCard("TLC_100t11", 117765)]
        internal static void Component11(Card card) => Supported(card);
        [StandardCard("TLC_100t12", 117774)]
        internal static void Component12(Card card) => Supported(card);
        [StandardCard("TLC_100t13", 117779)]
        internal static void Component13(Card card) => Supported(card);
        [StandardCard("TLC_100t14", 117784)]
        internal static void Component14(Card card) => Supported(card);
        [StandardCard("TLC_100t15", 117768)]
        internal static void Component15(Card card) => Supported(card);
        [StandardCard("TLC_100t16", 117793)]
        internal static void Component16(Card card) => Supported(card);
        [StandardCard("TLC_100t12e", 117773)]
        internal static void Enchant12e(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 1)) };
        }
        [StandardCard("TLC_100t14e2", 117783)]
        internal static void Enchant14e2(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) { IsOneTurnEffect = true } };
        }
        [StandardCard("TLC_100t21", 117764)]
        internal static void Component21(Card card) => Supported(card);
        [StandardCard("TLC_100t22", 117778)]
        internal static void Component22(Card card) => Supported(card);
        [StandardCard("TLC_100t23", 117780)]
        internal static void Component23(Card card) => Supported(card);
        [StandardCard("TLC_100t24", 117785)]
        internal static void Component24(Card card) => Supported(card);
        [StandardCard("TLC_100t25", 117769)]
        internal static void Component25(Card card) => Supported(card);
        [StandardCard("TLC_100t26", 117808)]
        internal static void Component26(Card card) => Supported(card);
        [StandardCard("TLC_100t27", 117843)]
        internal static void Component27(Card card) => Supported(card);
        [StandardCard("TLC_100t22e", 117775)]
        internal static void Enchant22e(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 2)) };
        }
        [StandardCard("TLC_100t24e2", 117787)]
        internal static void Enchant24e2(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2)) { IsOneTurnEffect = true } };
        }
        [StandardCard("TLC_100t27e", 117840)]
        internal static void Enchant27e(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(Effects.SetAttackHealth(3)) };
        }
        [StandardCard("TLC_100t31", 117763)]
        internal static void Component31(Card card) => Supported(card);
        [StandardCard("TLC_100t32", 117777)]
        internal static void Component32(Card card) => Supported(card);
        [StandardCard("TLC_100t33", 117781)]
        internal static void Component33(Card card) => Supported(card);
        [StandardCard("TLC_100t34", 117786)]
        internal static void Component34(Card card) => Supported(card);
        [StandardCard("TLC_100t35", 117770)]
        internal static void Component35(Card card) => Supported(card);
        [StandardCard("TLC_100t36", 117809)]
        internal static void Component36(Card card) => Supported(card);
        [StandardCard("TLC_100t37", 117844)]
        internal static void Component37(Card card) => Supported(card);
        [StandardCard("TLC_100t32e", 117776)]
        internal static void Enchant32e(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 4)) };
        }
        [StandardCard("TLC_100t34e2", 117788)]
        internal static void Enchant34e2(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(4)) { IsOneTurnEffect = true } };
        }
        [StandardCard("TLC_100t37e", 117841)]
        internal static void Enchant37e(Card card)
        {
            Supported(card); card.Power = new Power { Enchant = new Enchant(Effects.SetAttackHealth(5)) };
        }
        [StandardCard("TLC_101t", 122797)]
        internal static void Raptor(Card card) => Supported(card);
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class B18Autoplay2Pools
    {
        [OfflinePool("TLC_100", 117762)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("navigator-runic-spells", CompositeLocationRules.Spells,
                new[] { "DEATHKNIGHT", "DEMONHUNTER", "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR" }, destination: PoolDestination.Other)
        };
    }
}
