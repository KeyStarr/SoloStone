using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArisenOnyxia
    {
        [StandardCard("CATA_155", 124138)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_155t", "CATA_155t1");
            card.Power = new Power();
        }
        [StandardCard("CATA_155t", 124139)]
        [StandardCard("CATA_155t1", 125416)]
        internal static void ConfigureWing(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var poolStack = new TaskStack { Number = ((IPlayable)s)[GameTag.TAG_SCRIPT_DATA_NUM_1] };
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: poolStack);
                    var definition = ctx.PickRandomCard(OfflinePoolFilter.Random(type: CardType.MINION), PoolCostBinding.StackNumber, s.Card.AssetId);
                    if (definition == null) return;
                    var generated = (Playable)Entity.FromCard(c, definition, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    var actor = Enchantment.GetInstance(c, (IPlayable)s, generated, Cards.FromId("CATA_155e2"));
                    CostRules.AddPaymentOverride(generated, CostPaymentKind.Health, g.Turn, actor.Id);
                    Generic.AddHandPhase(c, generated);
                }) } };
        }
        [StandardCard("CATA_155e", 124143)]
        internal static void ConfigureScales(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(0)) { UseScriptTag = true } };
        }
        [StandardCard("CATA_155e2", 129203)]
        internal static void ConfigureBlood(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class ArisenOnyxiaPools
    {
        [OfflinePool("CATA_155t1", 125416)]
        internal static PoolRequirement[] DeclareOtherWingPools() => DeclarePools();
        [OfflinePool("CATA_155t", 124139)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("onyxia-wing-exact-cost-minion", OfflinePoolFilter.Random(type: CardType.MINION),
                new[] { "DEATHKNIGHT" }, PoolCostBinding.StackNumber, 2)
        };
    }
}
