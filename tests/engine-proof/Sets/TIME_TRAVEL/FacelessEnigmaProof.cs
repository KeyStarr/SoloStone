using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_FacelessEnigmaProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_860", (g, p, id) => {
        foreach (int available in new[] { 0, 1, 2, 3 })
        {
            var run = g.CloneExact(history: true);
            var pool = new[] { "CORE_EX1_287", "CORE_EX1_610", "CORE_EX1_289" }.Take(available).ToArray();
            LostCityPoolProof.WithPool(run, id, pool, () => {
                var owner = run.Player1; var source = Play(run, owner, id);
                if (available == 0) { Core2026Proof.Check(owner.Choice == null, "empty Secret pool finishes"); return; }
                int[] offers = owner.Choice.Choices.ToArray();
                Core2026Proof.Check(offers.Length == Math.Min(2, available) && offers.Select(i => run.IdEntityDic[i].Card.Id).Distinct().Count() == offers.Length,
                    "Unpinned_AllClassDistinctPair");
                var chosen = run.IdEntityDic[offers[0]].Card.Id;
                var other = offers.Length == 2 ? run.IdEntityDic[offers[1]].Card.Id : null;
                if (other != null) AutomaticCastProof.Cast(run, (Spell)Entity.FromCard(owner.Opponent, Cards.FromId(other)));
                var fork = run.CloneExact(history: true);
                foreach (var next in new[] { fork, run })
                {
                    var p1 = next.Player1;
                    LostCityPoolProof.Answer(next, offers[0]);
                    Core2026Proof.Check(p1.Choice == null && !next.Resolution.HasSuspendedEffect && p1.SecretZone.Single().Card.Id == chosen,
                        "chosen saved Secret installs for owner");
                    if (other != null)
                    {
                        Core2026Proof.Check(p1.Opponent.SecretZone.Single().Card.Id == other && p1.Opponent.GraveyardZone.Single().Card.Id == other,
                            "Unpinned_OpponentDuplicate preserves original and finishes continuation");
                        AutomaticCastProof.Attempt(next, (Spell)p1.Opponent.GraveyardZone.Single());
                    }
                }
            });
        }
        Add(p, "JAIL_321");
        Play(g, p, id);
        Core2026Proof.Check(!g.HistoryFacts.Query.Discoveries().Any(), "Look/pick Secret offer is not a Discover event");
        int[] saved = p.Choice.Choices.ToArray();
        string chosenId = g.IdEntityDic[saved[0]].Card.Id;
        string otherId = g.IdEntityDic[saved[1]].Card.Id;
        PoolPilotProof.Resume(g, next => {
            LostCityPoolProof.Answer(next, saved[0]);
            Core2026Proof.Check(!next.HistoryFacts.Query.Discoveries().Any(), "Look/pick completion is not a Discover");
            Core2026Proof.Check(next.Player1.SecretZone.Single().Card.Id == chosenId
                && next.Player2.SecretZone.Single().Card.Id == otherId
                && !next.Resolution.HasSuspendedEffect, "saved all-class pair survives snapshot and casts for separate owners");
        });
        foreach (var owner in new[] { p, p.Opponent })
            foreach (var secretId in new[] { "CORE_EX1_287", "CORE_EX1_289", "CORE_LOOT_101", "CORE_BAR_812", "CORE_EX1_610", "CORE_EX1_611" })
                if (owner.SecretZone.Count < 5 && !owner.SecretZone.Any(s => s.Card.Id == secretId))
                    AutomaticCastProof.Cast(g, (Spell)Entity.FromCard(owner, Cards.FromId(secretId)));
        var activeOwn = p.SecretZone.Select(s => s.Id).ToArray();
        var activeEnemy = p.Opponent.SecretZone.Select(s => s.Id).ToArray();
        Play(g, p, id);
        int selected = p.Choice.Choices.First();
        int facts = g.HistoryFacts.Query.Casts().Count();
        PoolPilotProof.Resume(g, next => {
            LostCityPoolProof.Answer(next, selected);
            Core2026Proof.Check(next.Player1.SecretZone.Select(s => s.Id).SequenceEqual(activeOwn)
                && next.Player2.SecretZone.Select(s => s.Id).SequenceEqual(activeEnemy)
                && next.HistoryFacts.Query.Casts().Count() == facts + 4 && !next.Resolution.HasSuspendedEffect,
                "Unpinned_FullBothZones: both saved casts attempted in order despite first fizzle; no overwrites");
        });
    });
}
