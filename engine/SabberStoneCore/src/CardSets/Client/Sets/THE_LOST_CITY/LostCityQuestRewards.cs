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
    internal static class LostCityQuestRewards
    {
        private static void Supported(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("UNG_028t", 41167)]
        internal static void TimeWarp(Card card)
        {
            Supported(card);
            card.Text = "Take an extra turn.\n<i>(Once per game)</i>";
            card.Tags[GameTag.SPELL_SCHOOL] = 1;
            card.Power = new Power { PowerTask = StateTaskList.Chain(new CustomTask((g, c, s, t, stack) => {
                stack.Flag = c[(GameTag)33066] == 0;
                if (stack.Flag) c[(GameTag)33066] = 1;
            }), new FlagTask(true, new ExtraTurnEffectTask())) };
        }
        [StandardCard("UNG_067t1", 41221)]
        internal static void CrystalCore(Card card)
        {
            Supported(card); card.Text = "For the rest of the game, your minions are 5/5.";
        }
        [StandardCard("UNG_067t1e", 42991)]
        internal static void CrystalPlayer(Card card)
        {
            Supported(card); card.Text = "Your minions are 5/5.";
        }
        [StandardCard("UNG_067t1e2", 46043)]
        internal static void CrystalStats(Card card)
        {
            Supported(card); card.Text = "5/5."; card.Tags[(GameTag)314] = 1;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttackHealth(5)) };
        }
        [StandardCard("UNG_920t1", 41367)]
        internal static void Carnassa(Card card)
        {
            Supported(card); card.Text = "<b>Rush</b>\n<b>Battlecry:</b> Shuffle 20 Raptors into your deck.";
            card.Tags[GameTag.RUSH] = 1; card.Rush = true; card.Tags[(GameTag)1078] = 42013;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 20 && !c.DeckZone.IsFull; i++)
                {
                    var brood = Entity.FromCard(c, Cards.FromId("UNG_920t2"), new EntityData {
                        [GameTag.ZONE] = (int)Zone.DECK, [GameTag.DISPLAYED_CREATOR] = s.Id
                    });
                    Generic.ShuffleIntoDeck(c, s, brood);
                }
            }) };
        }
        [StandardCard("UNG_934t1", 41426)]
        internal static void Sulfuras(Card card) { Supported(card); card.SetPrintedStats(4, 2); }
        [StandardCard("UNG_829t1", 41942)]
        internal static void NetherPortal(Card card) { Supported(card); card.Tags[GameTag.SPELL_SCHOOL] = 6; }
        [StandardCard("UNG_829t2", 42224)]
        internal static void Portal(Card card)
        {
            Supported(card);
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = StateTaskList.Chain(new SummonTask("UNG_829t3", SummonSide.LEFT), new SummonTask("UNG_829t3", SummonSide.RIGHT))
            } };
        }
        [StandardCard("UNG_829t3", 42753)]
        internal static void Imp(Card card) { Supported(card); card.Text = string.Empty; card.Tags[(GameTag)1965] = 1; }
        [StandardCard("UNG_954t1", 41946)]
        internal static void Galvadon(Card card)
        {
            Supported(card); card.SetPrintedStats(8, 8);
            var choose = new DiscoverThenContinueTask((g, c, s, stack) =>
                AshalonRidgeGuardian.Adaptations.ChooseNElements(3, g.Random).Select(Cards.FromId).ToArray());
            var apply = new CustomTask((g, c, s, t, stack) => {
                if (s is Minion minion && minion.Zone == c.BoardZone)
                    Generic.AddEnchantmentBlock(g, Cards.FromId(stack.Playables.Single().Card.Id + "e"), minion, minion);
            });
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/UNG_954t1/v1",
                choose, apply, choose, apply, choose, apply, choose, apply, choose, apply) };
        }
        internal static OfflinePoolFilter Murlocs() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.MURLOC);
        [StandardCard("UNG_942t", 41498)]
        internal static void Megafin(Card card)
        {
            Supported(card);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                while (!c.HandZone.IsFull)
                {
                    var chosen = ctx.PickRandomCard(Murlocs(), recipeDbfId: 41498);
                    if (chosen == null) break;
                    ctx.AddToHand(chosen.Id);
                }
            }) };
        }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class B15QuestClosurePools
    {
        [OfflinePool("UNG_942t", 41498)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("quest-reward-murlocs", Sets.LostCityQuestRewards.Murlocs(), new[] { "SHAMAN" })
        };
    }
}
