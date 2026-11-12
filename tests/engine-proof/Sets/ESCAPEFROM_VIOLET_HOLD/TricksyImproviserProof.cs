using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_TricksyImproviserProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_321", (g, p, id) => {
        var held = Add(p, id);
        Core2026Proof.Check(!PoweredUp.IsMet(held), "no spell yet this turn");
        var automatic = (Spell)Entity.FromCard(p, Cards.FromId("GAME_005")); AutomaticCastProof.Cast(g, automatic);
        Core2026Proof.Check(PoweredUp.IsMet(held), "effect cast counts for condition without a player play");
        LostCityPoolProof.WithPool(g, id, new[] { "CORE_EX1_287" }, () => {
            PlayEntity(g, p, held);
            var attempts = g.HistoryFacts.Query.Casts().Where(f => f.DbfId == Cards.FromId("CORE_EX1_287").AssetId).ToArray();
            Core2026Proof.Check(p.SecretZone.Count == 1 && p.GraveyardZone.Count(c => c.Card.Id == "CORE_EX1_287") == 1
                && attempts.Length == 4, "Unpinned_WithReplacement second duplicate fizzles without reroll");
        });
        End(g); End(g); ClearBoard(g, p);
        var fresh = Add(p, id); Core2026Proof.Check(!PoweredUp.IsMet(fresh), "condition expires next turn");
        PlayEntity(g, p, fresh); Core2026Proof.Check(p.SecretZone.Count == 1, "unmet condition casts nothing");
        var spell = (Spell)Entity.FromCard(p, Cards.FromId("CORE_EX1_287")); AutomaticCastProof.Cast(g, spell);
        var ready = Add(p, id); Core2026Proof.Check(PoweredUp.IsMet(ready), "fizzled completed attempt satisfies cast condition");
        var replay = HistoryFactsProof.Fixture();
        AutomaticCastProof.Cast(replay, (Spell)Entity.FromCard(replay.Player1, Cards.FromId("GAME_005")));
        LostCityPoolProof.WithPool(replay, id, new[] { "CORE_EX1_287" }, () => Play(replay, replay.Player1, id));
        PoolPilotProof.Resume(replay, next => LostCityPoolProof.WithPool(next, id, new[] { "CORE_EX1_287" }, () => {
            var shudder = Play(next, next.Player1, "GIL_820");
            var attempts = next.Player1.GraveyardZone.OfType<Spell>().Where(c => c[GameTag.CREATOR] == shudder.Id).ToArray();
            Core2026Proof.Check(attempts.Length == 2 && attempts.All(c => c.Card.Id == "CORE_EX1_287")
                && next.Player1.SecretZone.Count == 1, "Shudderwock uses Tricksy recipe pool for both duplicate attempts");
            foreach (var cast in attempts) AutomaticCastProof.Attempt(next, cast);
        }));
    });
}
