using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CapturedArchmage
    {
        internal const int DbfId = 126874;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Trigger = new ProgressTrigger(Progress(TriggerActivation.HAND), Progress(TriggerActivation.DECK), Progress(TriggerActivation.PLAY)),
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                    if (OtherDeaths((IPlayable)s) < 4) return;
                    var enemies = c.Opponent.BoardZone.GetAll().Cast<ICharacter>().Append(c.Opponent.Hero).ToArray();
                    var spell = (Spell)Entity.FromCard(c, Cards.FromId("CS2_029"));
                    Generic.CastSpell(c, g, spell, enemies[g.Random.Next(enemies.Length)], 0);
                })
            };
        }

        // Burned/discarded cards enter the graveyard without the death marker (Generic.AddHandPhase/DiscardBlock).
        private static int OtherDeaths(IPlayable source)
            => source.Controller.GraveyardZone.Count(p => p.Id != source.Id && p.Card.Id == "JAIL_974" && p.ToBeDestroyed);

        private static Trigger Progress(TriggerActivation activation)
            => new Trigger(TriggerType.DEATH) {
                TriggerActivation = activation,
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p.Card.Id == "JAIL_974"),
                FastExecution = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // DEATH fires before the dying minion is added to its graveyard (Game.GraveYard).
                    int pending = t.Id != s.Id && !c.GraveyardZone.Contains(t) ? 1 : 0;
                    s[GameTag.TAG_SCRIPT_DATA_NUM_1] = Math.Min(4, OtherDeaths((IPlayable)s) + pending);
                })
            };

        private sealed class ProgressTrigger : MultiTrigger
        {
            internal ProgressTrigger(params Trigger[] triggers) : base(triggers) { }

            public override Trigger Activate(IPlayable source, TriggerActivation activation = TriggerActivation.PLAY,
                bool cloning = false, bool asAncillary = false)
            {
                // Initialize late-created copies on zone entry; runtime instances use standard MultiTrigger cloning.
                source[GameTag.TAG_SCRIPT_DATA_NUM_1] = Math.Min(4, OtherDeaths(source));
                return base.Activate(source, activation, cloning, asAncillary);
            }
        }
    }
}
