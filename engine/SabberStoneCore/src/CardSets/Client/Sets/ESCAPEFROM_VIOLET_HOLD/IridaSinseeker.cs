using System;
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
    internal static class IridaSinseeker
    {
        internal const int DbfId = 126211;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var deck = c.DeckZone.Reverse().ToArray(); if (deck.Length <= 1) return;
                var survivor = deck[g.Random.Next(deck.Length)]; g.OnRandomHappened(true);
                var ledger = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("JAIL_719e2"));
                ledger.Power.Trigger.Activate(ledger);
                foreach (var card in deck.Where(card => card.Id != survivor.Id))
                {
                    var stored = PayloadRules.Store(ledger, 1, card, (PayloadPolicy)0); stored[PayloadRules.Link] = card.Id;
                    card[PayloadRules.Stored] = 1;
                    c.SetasideZone.Add(c.DeckZone.Remove(card));
                }
            }) };
        }
        internal static void ConfigureVoid(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    foreach (var entry in PayloadRules.Entries(s, 1).Take(2))
                    {
                        Generic.Copy(c, s, entry, Zone.HAND, preserveCapturedState: true);
                        PayloadRules.Remove(entry);
                    }
                    if (PayloadRules.Entries(s, 1).Length == 0) ((Enchantment)s).Remove();
                })
            } };
        }
    }
}
