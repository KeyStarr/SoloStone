using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class B17TransformAttackNativeProof
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("b17 native: " + message); }
    [GroupProof("b17-transform-attack-native", ProofKind.Native)]
    internal static void Run()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (var id in new[] { "JAIL_875", "JAIL_458" })
        {
            var f = new PoweredUpGlowTests.Fixture();
            if (opponent) f.Process(EndTurnTask.Any(f.Player));
            var g = f.Game; var p = g.CurrentPlayer;
            p.BaseMana = 10; p.UsedMana = 0;
            var card = Generic.DrawCard(p, Cards.FromId(id)); f.Flush();
            f.Process(PlayCardTask.Any(p, card));
            var allOffers = new System.Collections.Generic.HashSet<int>();
            void Pick(int selected)
            {
                var decision = g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out var error), "answer: " + error);
                f.Flush();
            }
            if (id == "JAIL_458")
            {
                allOffers.UnionWith(p.Choice.Choices);
                Pick(p.Choice.Choices.Single(i => g.IdEntityDic[i].Card.Id == "JAIL_458t4"));
            }
            p.Hero.IsExhausted = false;
            int handBefore = p.HandZone.Count;
            f.Process(HeroAttackTask.Any(p, p.Opponent.Hero));
            Check(p.Choice?.Choices.Count == 3, "after-attack choices reach native boundary");
            var offers = p.Choice.Choices.ToArray();
            allOffers.UnionWith(offers);
            Pick(offers[0]);
            Check(p.Choice == null, "native choice completes");
            var hidden = g.IdEntityDic.Values.Where(e => PayloadRules.IsStored(e) ||
                opponent && (e.Zone == p.HandZone || allOffers.Contains(e.Id))).Select(e => e.Id).ToHashSet();
            foreach (var entry in f.Entries)
            {
                var shape = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                if (shape != null)
                {
                    Check(shape.Tags.All(t => !PayloadRules.IsInternalTag((GameTag)t.Name) && !Generic.IsDecisionInternalTag((GameTag)t.Name)), "internal state filtered");
                    if (hidden.Contains(shape.Entity))
                        Check(string.IsNullOrEmpty(shape.Name) && shape.Tags.All(t => t.Name == (int)GameTag.ENTITY_ID || t.Name == (int)GameTag.CONTROLLER ||
                            t.Name == (int)GameTag.ZONE || t.Name == (int)GameTag.ZONE_POSITION), "opponent hidden card/offer/payload identity never emitted");
                }
                if (entry.HasTagChange)
                    Check(!PayloadRules.IsInternalTag((GameTag)entry.TagChange.Tag) && !Generic.IsDecisionInternalTag((GameTag)entry.TagChange.Tag), "private delta filtered");
            }
            if (id == "JAIL_458")
                Check(f.Entries.Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == card.Id && e.ChangeEntity.Name.StartsWith("JAIL_458t")), "reload uses CHANGE_ENTITY on same weapon");
            Check(p.HandZone.Count == handBefore + 1, "effect produced hidden hand card for privacy assertion");
        }
        var transform = new PoweredUpGlowTests.Fixture(); var podling = transform.Summon("EDR_529", transform.Player);
        Generic.ChangeEntityBlock(transform.Player, podling, Cards.FromId("hexfrog"), true); transform.Flush();
        Check(transform.Entries.Count(e => e.HasChangeEntity && e.ChangeEntity.Entity == podling.Id) == 1 && podling.Card.Cost == 2,
            "Podling emits final retail-shaped replacement only");
        Console.WriteLine("PASS b17 native choices, transforms, hidden hand/offer/payload privacy both seats");
    }
}
