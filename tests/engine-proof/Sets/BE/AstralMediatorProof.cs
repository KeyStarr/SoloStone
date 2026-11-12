using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_AstralMediatorProof
{
    internal static void Run() => StandardSetsProof.Case("BE_256", (g, p, id) => {
        LostCityPoolProof.WithPool(g, id, new[] { "CORE_EX1_287" }, () => {
            var source = (Minion)Play(g, p, id);
            var active = (Spell)p.SecretZone.Single(); AutomaticCastProof.Attempt(g, active);
            Kill(g, source);
            var attempt = (Spell)p.GraveyardZone.Single(c => c.Card.Id == "CORE_EX1_287");
            Core2026Proof.Check(p.SecretZone.Single().Id == active.Id, "Unpinned_Deathrattle duplicate Secret fizzles");
            AutomaticCastProof.Attempt(g, attempt);
            source = Summon(p, id); source.Silence(); int casts = g.HistoryFacts.Query.Casts().Count(); Kill(g, source);
            Core2026Proof.Check(g.HistoryFacts.Query.Casts().Count() == casts, "silence removes deathrattle cast");
        });
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        LostCityPoolProof.WithPool(g, id, new[] { "CORE_BT_801" }, () => {
            var source = Summon(p, id); Kill(g, source);
            var cast = (Spell)p.GraveyardZone.Single(c => c.Card.Id == "CORE_BT_801");
            Core2026Proof.Check(cast.CardTarget == 0, "Unpinned_SampleBeforeTarget: empty board still selects targeted spell");
            AutomaticCastProof.Attempt(g, cast);
        });
        LostCityPoolProof.WithPool(g, id, Array.Empty<string>(), () => {
            var rng = g.Random.CaptureState(); Play(g, p, id);
            Core2026Proof.Check(g.Random.CaptureState().Equals(rng), "empty random spell pool uses no RNG");
        });
        var replay = HistoryFactsProof.Fixture();
        LostCityPoolProof.WithPool(replay, id, new[] { "CORE_EX1_287" }, () => Play(replay, replay.Player1, id));
        PoolPilotProof.Resume(replay, next => LostCityPoolProof.WithPool(next, id, new[] { "CORE_EX1_287" }, () => {
            var shudder = Play(next, next.Player1, "GIL_820");
            var cast = (Spell)next.Player1.GraveyardZone.Single(c => c.Card.Id == "CORE_EX1_287");
            Core2026Proof.Check(cast[GameTag.CREATOR] == shudder.Id && next.Player1.SecretZone.Count == 1,
                "Shudderwock uses Mediator recipe pool and fizzles duplicate without replacing active Secret");
            AutomaticCastProof.Attempt(next, cast);
        }));
    });
}
