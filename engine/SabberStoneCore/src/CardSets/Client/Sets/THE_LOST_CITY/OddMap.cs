using SabberStoneCore.Conditions;
using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OddMap
    {
        internal const int DbfId = 117596;
        private static OfflinePoolFilter Filter() => new OfflinePoolFilter(classMode: PoolClassMode.ControllerAndNeutral,
            type: CardType.MINION, race: Race.BEAST, attackParity: 1, discoverFallback: true);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("odd-attack-beast", Filter(), new[] { "HUNTER" }) };
        [StandardCard("TLC_824", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TLC_824/v1",
                new CustomTask((g, c, s, t, stack) => stack.Number = c.SetasideZone.Select(p => p.Id).DefaultIfEmpty().Max()),
                new DiscoverThenContinueTask(Filter(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    var others = c.SetasideZone.Where(p => p.Id > stack.Number && p.Id != chosen.Id
                        && p[GameTag.DISPLAYED_CREATOR] == s.Id).Select(p => p.Card).Distinct().ToArray();
                    Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                    if (chosen.Zone != c.HandZone || others.Length == 0) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_824e"), (IPlayable)s, c);
                    var effect = c.AppliedEnchantments.Last(e => e.Card.Id == "TLC_824e");
                    PayloadRules.StoreCard(effect, 1, chosen.Card, (PayloadPolicy)0)[PayloadRules.Value] = chosen.Id;
                    foreach (var other in others) PayloadRules.StoreCard(effect, 2, other, (PayloadPolicy)0);
                })) };
        }
        [StandardCard("TLC_824e", 117597)]
        internal static void ConfigureMarker(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                OwnerCondition = new SelfCondition(owner => owner.Game.CurrentEventData?.EventSource?.Id == PayloadRules.Entries(owner, 1).SingleOrDefault()?[PayloadRules.Value]),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var effect = (Enchantment)s;
                    var offers = PayloadRules.Entries(effect, 2).Select(e => e.Card).Distinct().ToArray();
                    var creator = effect.Creator;
                    effect.Remove();
                    if (t is Minion minion && minion.IsDormant || offers.Length == 0) return;
                    Generic.CreateChoiceCards(c, creator, null, ChoiceType.GENERAL, ChoiceAction.HAND, offers, null);
                    var choice = c.Choice;
                    while (choice.NextChoice != null) choice = choice.NextChoice;
                    foreach (int offer in choice.Choices) g.IdEntityDic[offer][Generic.PrivateDecisionOfferTag] = c.PlayerId;
                })
            } };
        }
    }
}
