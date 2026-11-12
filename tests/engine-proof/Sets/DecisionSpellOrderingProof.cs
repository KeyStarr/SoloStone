using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

internal static class DecisionSpellOrderingProof
{
    internal static void Run(long seed, string id, Action<SabberStoneCore.Model.Game, IPlayable> result)
    {
        var game = GiftedDiscoverProof.New(seed, CardClass.ROGUE);
        var p = game.Player1;
        var pyro = Core2026Proof.Summon(p, "CORE_NEW1_020");
        int spells = p.NumSpellsPlayedThisGame;
        var source = Core2026Proof.Play(game, p, id);
        int chosen = p.Choice.Choices[0];
        Core2026Proof.Check(pyro.Damage == 0 && game.SpellCompletion?.SourceId == source.Id
            && source.Zone != p.GraveyardZone && p.NumSpellsPlayedThisGame == spells,
            id + " defers after-cast and completion until Discover resolves");
        PoolPilotProof.Resume(game, next => {
            CoreCostProof.Answer(next, chosen);
            Core2026Proof.Check(((Minion)next.IdEntityDic[pyro.Id]).Damage == 1 && next.SpellCompletion == null
                && !next.Resolution.HasSuspendedEffect && next.Player1.Choice == null
                && next.IdEntityDic[source.Id].Zone == next.Player1.GraveyardZone
                && next.Player1.NumSpellsPlayedThisGame == spells + 1,
                id + " completes once and runs after-cast after the answer");
            result(next, next.IdEntityDic[chosen]);
            Core2026Proof.End(next);
        });
    }
}
