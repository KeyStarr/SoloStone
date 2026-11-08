using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class InspectorMurlocHolmes
    {
        internal const int DbfId = 126602;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_851/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.EnemyHand, count: 4),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables?.LastOrDefault();
                    if (chosen == null) return;
                    var actor = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("JAIL_851e"));
                    actor[DiscoverRealRules.WatchedDefinition] = chosen.Card.AssetId;
                    actor[DiscoverRealRules.WatchedController] = c.Opponent.Id;
                    actor[DiscoverRealRules.WatchedTurn] = g.HistoryFacts.Query.TurnsTaken(c.Opponent.Id) + 1;
                    actor.Power.Trigger.Activate(actor);
                })) };
        }
        internal static void ConfigureWatch(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.PLAY_CARD) {
                TriggerSource = TriggerSource.ENEMY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (g.CurrentPlayer != c.Opponent || g.HistoryFacts.Query.TurnsTaken(c.Opponent.Id) != s[DiscoverRealRules.WatchedTurn]
                        || t.Card.Name != Cards.FromAssetId(s[DiscoverRealRules.WatchedDefinition]).Name) return;
                    ((Enchantment)s).Remove();
                    for (int i = 0; i < 3; i++) new EffectContext(g, c.Id, s.Id).AddToHand("GAME_005");
                }) } };
        }
    }
}
