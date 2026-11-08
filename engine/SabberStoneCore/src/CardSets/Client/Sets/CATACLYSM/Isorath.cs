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
    internal static class Isorath
    {
        internal const int DbfId = 123332;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    for (int i = 0; i < 2 && c.Opponent.HandZone.Count > 0; i++)
                    {
                        var victim = c.Opponent.HandZone[g.Random.Next(c.Opponent.HandZone.Count)]; g.OnRandomHappened(true);
                        var stored = PayloadRules.Store(s, 1, victim); stored[PayloadRules.Recipient] = victim.Controller.Id;
                        PayloadRules.HandExit(stored);
                        victim[PayloadRules.Stored] = 1;
                        c.Opponent.SetasideZone.Add(c.Opponent.HandZone.Remove(victim));
                    }
                    DormantRules.Make((Minion)s, 2, "CATA_481e", s);
                }),
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                    foreach (var entry in PayloadRules.Entries(s, 1))
                        Generic.Copy(g.ControllerById(entry[PayloadRules.Recipient]), s, entry, Zone.HAND, preserveCapturedState: true);
                })
            };
        }
        internal static void ConfigureSleep(Card card) { card.Implemented = true; card.Power = new Power(); }
    }
}
