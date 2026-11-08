using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LieInWait
    {
        [StandardCard("TLC_513", 117222)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(117222, 5, QuestRewardKind.CardToHand, "TLC_513t"));
            card.Power = new Power { Trigger = new Trigger(TriggerType.SHUFFLE_INTO_DECK) {
                TriggerSource = TriggerSource.FRIENDLY,
                FastExecution = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var fact = g.HistoryFacts.Query.Events<ShuffleOperationFact>().LastOrDefault();
                    if (fact == null || fact.EntityId != t.Id || fact.ControllerId != c.Id
                        || fact.DestinationControllerId != c.Id) return;
                    int operation = unchecked((int)fact.OperationId);
                    if (s[PayloadRules.Generation] == operation) return;
                    s[PayloadRules.Generation] = operation;
                    QuestRules.Advance((Spell)s);
                })
            } };
        }
        [StandardCard("TLC_513t", 117224)]
        internal static void ConfigureHero(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.AppliedEnchantments?.Any(e => e.Card.Id == "TLC_513e2") != true)
                {
                    var effect = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("TLC_513e2"));
                    effect.Power.Trigger.Activate(effect);
                }
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
                    Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId("TLC_513t2")), -1, s);
            }) };
        }
        [StandardCard("TLC_513hp", 119197)]
        internal static void ConfigureHeroPower(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2; i++)
                {
                    var eligible = c.DeckZone.Where(p => !g.HistoryFacts.Query.StartedInDeck(p.Id, c.Id)).ToArray();
                    if (eligible.Length == 0) break;
                    Generic.Draw(c, eligible.Length == 1 ? eligible[0] : eligible[g.Random.Next(eligible.Length)]);
                }
            }) };
        }
        [StandardCard("TLC_513e2", 120950)]
        internal static void ConfigurePlayerEffect(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SabberStoneCore.Conditions.SelfCondition(p => p.Card.Id == "TLC_513t2"),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!c.DeckZone.IsFull)
                        new MoveToDeck(EntityType.TARGET).Process(g, c, s, t, stack);
                })
            } };
        }
    }
}
