using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreIdentityPools
    {
        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Discover(type: CardType.MINION);
        internal static OfflinePoolFilter Spell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        internal static OfflinePoolFilter Taunt() => OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.TAUNT);
        [OfflinePool("Core_LOE_115", 113109)]
        internal static PoolRequirement[] RavenPools() => new[] {
            new PoolRequirement("minion", Minion(), new[] { "DRUID" }),
            new PoolRequirement("spell", Spell(), new[] { "DRUID" })
        };

        [OfflinePool("Core_UNG_072", 112923)]
        internal static PoolRequirement[] StonehillPools() => new[] {
            new PoolRequirement("taunt-minion", Taunt(), new[] { "NEUTRAL" })
        };

        internal static void Discover(Game game, Controller owner, IEntity source, int recipeDbfId, OfflinePoolFilter filter)
        {
            var context = new EffectContext(game, owner.Id, source.Id);
            var offers = OfflineCardPools.SampleDistinct(owner.Game, filter, owner.HeroClass, recipeDbfId, sourceClassMask: OfflineCardPools.PrintedClassMask(source.Card));
            if (context.Discover(offers))
            {
                var choice = owner.Choice;
                while (choice.NextChoice != null) choice = choice.NextChoice;
                foreach (int id in choice.Choices) owner.Game.IdEntityDic[id][Generic.PrivateDecisionOfferTag] = owner.PlayerId;
            }
        }
    }
}
