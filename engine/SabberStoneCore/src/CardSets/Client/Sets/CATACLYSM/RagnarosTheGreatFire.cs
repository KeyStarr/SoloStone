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

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RagnarosTheGreatFire
    {
        internal const int DbfId = 124110;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_150t", "CATA_150t1");
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // Lean: snapshot left-to-right; the end-turn checkpoint resolves deaths after all activations.
                    var minions = c.BoardZone.GetAll().Where(m => !m.IsDead && m.HasDeathrattle).ToArray();
                    g.TaskQueue.StartEvent();
                    try
                    {
                        foreach (var minion in minions)
                        {
                            // Legacy DEATHRATTLE summons read this cursor even while their source is alive.
                            if (minion.Zone == minion.Controller.BoardZone)
                                minion.LastBoardPosition = EffectContext.DeathrattlePosition(minion.Controller, minion);
                            // Match SewerSwimmer: Silence removes printed text, not subsequently granted Deathrattles.
                            if (!minion.IsSilenced) minion.ActivateTask(PowerActivation.DEATHRATTLE, fromDeath: false);
                            if (minion.AppliedEnchantments != null)
                                foreach (var enchant in minion.AppliedEnchantments.ToArray())
                                    if (enchant.Power.DeathrattleTask != null)
                                        g.TaskQueue.Enqueue(enchant.Power.DeathrattleTask, minion.Controller, minion, enchant, triggerKeyword: (int)GameTag.DEATHRATTLE);
                            g.ProcessTasks();
                        }
                    }
                    finally { g.TaskQueue.EndEvent(); }
                })
            } };
        }

        internal static void ConfigureHand(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var targets = new TaskStack { Playables = c.Opponent.BoardZone.GetAll().Cast<ICharacter>().Append(c.Opponent.Hero)
                    .Where(m => !m.IsDead).Cast<IPlayable>().ToArray() };
                if (new RandomTask(1, EntityType.STACK).Process(g, c, s, t, targets) != TaskState.COMPLETE) return;
                new DamageTask(s[GameTag.TAG_SCRIPT_DATA_NUM_1], EntityType.STACK).Process(g, c, s, t, targets);
            }) };
        }
    }
}
