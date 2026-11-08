using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ZuramatsPrison
    {
        private const int Discards = 126497;
        [StandardCard("JAIL_887", 126497)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                LocationActivation = new LocationActivation {
                    RequiresFreeSlot = true,
                    TargetCondition = (c, t) => !c.HandZone.IsEmpty,
                    Task = StateTaskList.Suspendable("b16.prison.v1", new SelectEntityThenContinueTask(),
                        new CustomTask((g, c, s, t, stack) => {
                            var chosen = stack.Playables.LastOrDefault();
                            if (chosen != null && chosen.Zone == c.HandZone)
                            {
                                Card identity = Cards.FromId(chosen.Card.Id);
                                long before = g.HistoryFacts.Query.Discards().Count();
                                Generic.DiscardBlock(c, chosen);
                                if (g.HistoryFacts.Query.Discards().Skip((int)before).Any(f => f.EntityId == chosen.Id))
                                {
                                    PayloadRules.StoreCard(s, Discards, identity, PayloadPolicy.Copy);
                                    new EffectContext(g, c.Id, s.Id).SummonRightOfLocation("JAIL_887t3");
                                }
                            }
                            s[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
                        }))
                },
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var freed = (Minion)Entity.FromCard(c, Cards.FromId("JAIL_887t2"),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, c.SetasideZone);
                    foreach (var entry in PayloadRules.Entries(s, Discards))
                        PayloadRules.StoreCard(freed, Discards, entry.Card);
                    int position = s.Zone == c.BoardZone ? ((BoardObject)s).ZonePosition + 1 : ((BoardObject)s).LastBoardPosition;
                    Generic.RemoveFromZone(c, freed);
                    Generic.SummonBlock(g, freed, Math.Clamp(position, 0, c.BoardZone.SlotCount), s);
                })
            };
        }
        [StandardCard("JAIL_887t2", 126499)]
        internal static void Zuramat(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var entries = PayloadRules.Entries(s, Discards);
                    if (entries.Length == 0) return;
                    var entry = entries[g.Random.Next(entries.Length)];
                    var definition = entry.Card;
                    PayloadRules.Remove(entry);
                    var replay = Entity.FromCard(c, definition, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, c.SetasideZone);
                    AutoPlayRules.Play(replay, s);
                })
            } };
        }
        [StandardCard("JAIL_887t3", 127277)]
        internal static void Whisper(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
    }
}
