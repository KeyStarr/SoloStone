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
            Core2026Proof.Check(p.SecretZone.Count == 1 && p.SecretZone.Single().Card.Id == "CORE_EX1_287"
                && p.GraveyardZone.All(c => c.Card.Id != "CORE_EX1_287"), "Unpinned_TricksyNoDuplicates a one-Secret pool casts once; the second pick is not a duplicate that fizzles");
        });
        End(g); End(g); ClearBoard(g, p);
        var fresh = Add(p, id); Core2026Proof.Check(!PoweredUp.IsMet(fresh), "condition expires next turn");
        PlayEntity(g, p, fresh); Core2026Proof.Check(p.SecretZone.Count == 1, "unmet condition casts nothing");
        var spell = (Spell)Entity.FromCard(p, Cards.FromId("CORE_EX1_287")); AutomaticCastProof.Cast(g, spell);
        var ready = Add(p, id); Core2026Proof.Check(PoweredUp.IsMet(ready), "fizzled completed attempt satisfies cast condition");
        var replay = HistoryFactsProof.Fixture();
        AutomaticCastProof.Cast(replay, (Spell)Entity.FromCard(replay.Player1, Cards.FromId("GAME_005")));
        var three = Unpinned_TwigSecretsDormantProof.PoolOf(replay, id);
        LostCityPoolProof.WithPool(replay, id, three, () => Play(replay, replay.Player1, id));
        PoolPilotProof.Resume(replay, next => LostCityPoolProof.WithPool(next, id, three, () => {
            Core2026Proof.Check(next.Player1.SecretZone.Count == 2, "Tricksy cast two distinct Secrets from the three-Secret pool");
            var shudder = Play(next, next.Player1, "GIL_820");
            var added = next.Player1.SecretZone.Where(c => c[GameTag.CREATOR] == shudder.Id).ToArray();
            Core2026Proof.Check(added.Length == 1 && next.Player1.SecretZone.Select(c => c.Card.Id).OrderBy(x => x).SequenceEqual(three.OrderBy(x => x)),
                "Shudderwock's repeated Tricksy uses the recipe pool and casts only the Secret not yet active");
        }));
    });
}
