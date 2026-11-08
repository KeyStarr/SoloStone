using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NoxiousBribe
    {
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Discover(keyword: GameTag.CHOOSE_ONE);
        [StandardCard("JAIL_861", 126054)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/JAIL_861/v1",
                new DiscoverThenContinueTask(Filter()),
                new CustomTask((g, c, s, t, stack) => {
                    if (stack.Playables == null || stack.Playables.Count == 0) return;
                    var chosen = stack.Playables[stack.Playables.Count - 1];
                    var definition = chosen.Card;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_861e"), (IPlayable)s, chosen);
                    Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                    Generic.DrawCard(c.Opponent, definition);
                })) };
        }
        [StandardCard("JAIL_861e", 132587)]
        internal static void ConfigureMark(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.CHOOSE_BOTH, EffectOperator.SET, 1)) };
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class B17TargetingConditionsPools
    {
        [OfflinePool("JAIL_861", 126054)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("noxious-bribe-choose-one", Sets.NoxiousBribe.Filter(), new[] { "DRUID" }) };
    }
}
