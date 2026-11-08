using SabberStoneCore.Conditions;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CryptMap
    {
        internal const int DbfId = 117431;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TLC_435/v1",
                new CustomTask((g, c, s, t, stack) => { stack.Number = c.SetasideZone.Select(p => p.Id).DefaultIfEmpty().Max(); }),
                new DiscoverThenContinueTask(TheLostCityPools.FrostRuneDiscover(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen == null) return;
                    var others = c.SetasideZone.Where(p => p.Id != chosen.Id && p.Id > stack.Number &&
                        p[GameTag.DISPLAYED_CREATOR] == s.Id).Select(p => p.Id).ToArray();
                    c.SetasideZone.Remove(chosen);
                    Generic.AddHandPhase.Invoke(c, chosen);
                    if (chosen.Zone != c.HandZone || others.Length == 0) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_435e"), (IPlayable)s, c, chosen.Id, others[0]);
                    var effect = c.AppliedEnchantments.Last();
                    if (others.Length > 1) effect[GameTag.TAG_SCRIPT_DATA_ENT_1] = others[1];
                })) };
        }

        internal static void ConfigureEffect(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                OwnerCondition = new SelfCondition(owner => owner.Game.CurrentEventData?.EventSource?.Id == ((Enchantment)owner).ScriptTag1),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var effect = (Enchantment)s;
                    var ids = new[] { effect.ScriptTag2, effect[GameTag.TAG_SCRIPT_DATA_ENT_1] }.Where(i => i != 0).ToList();
                    var choice = new Choice(c) { ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND,
                        SourceId = effect.Creator.Id, Choices = ids };
                    // A played card may already have opened its own Discover; append this bonus.
                    if (c.Choice == null) c.Choice = choice;
                    else
                    {
                        var tail = c.Choice;
                        while (tail.NextChoice != null) tail = tail.NextChoice;
                        tail.NextChoice = choice;
                    }
                    effect.Remove();
                })
            } };
        }
    }
}
