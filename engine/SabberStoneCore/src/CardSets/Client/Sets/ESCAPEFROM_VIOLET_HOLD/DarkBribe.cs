using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DarkBribe
    {
        internal const int DbfId = 128019;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/JAIL_206/v1",
                new DrawAndCaptureTask(3),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                        using var historyTransfer=g.Facts.CardTransfer(t,c.Id,Zone.HAND,s);
                        using var temporaryTransfer = TemporaryRules.PreserveHandTransfer(t);
                        // Ordinary hand removal resets cost; giving preserves the physical card's modifiers.
                        c.HandZone.RemoveForTrade(t);
                        t.OngoingEffect?.Remove();
                        g.AuraUpdate();
                        t.Controller = c.Opponent;
                        t[GameTag.CONTROLLER] = c.Opponent.PlayerId;
                        if (t.AppliedEnchantments != null)
                            foreach (var enchantment in t.AppliedEnchantments.ToArray())
                                enchantment.RebindController(c.Opponent);
                        if (t.ChooseOnePlayables != null)
                            foreach (var option in t.ChooseOnePlayables)
                            {
                                option.Zone?.Remove(option);
                                option.Controller = c.Opponent;
                                option[GameTag.CONTROLLER] = c.Opponent.PlayerId;
                                c.Opponent.SetasideZone.Add(option);
                            }
                        Generic.AddHandPhase(c.Opponent, t);
                        QuestRules.SettleHandCountChanges(c);
                        QuestRules.SettleHandCountChanges(c.Opponent);
                    }), ctx.LastChosenEntityId))) };
        }
    }
}
