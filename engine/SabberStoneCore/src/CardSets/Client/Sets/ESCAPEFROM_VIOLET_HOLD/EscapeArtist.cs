using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EscapeArtist
    {
        internal const int DbfId = 125643;
        // Camouflaged Dirigible's impact-only Stealth effect provides a cue without changing any gameplay tags.
        private const string EscapeEffect = "ReuseFX_KeywordImpact_Stealth_Super_ImpactOnly:f391dcf35565e4e4e80d15ebe8a2de8f";

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.SELF,
                Condition = SelfCondition.IsNotDead,
                SingleTask = ComplexTask.Create(new DrawTask(), new CustomTask((g, c, s, t, stack) => {
                    if (s is not Minion minion || minion.Zone != minion.Controller.BoardZone) return;
                    if (g.History)
                    {
                        // Play on the board actor before HIDE retires it.
                        g.PowerHistory.Add(new PowerHistorySubSpellStart {
                            Source = minion.Id, Targets = { minion.Id }, SpellPrefabGuid = EscapeEffect
                        });
                        g.PowerHistory.Add(new PowerHistorySubSpellEnd());
                    }
                    Generic.RemoveMinionFromGame(minion);
                }))
            } };
        }
    }
}
