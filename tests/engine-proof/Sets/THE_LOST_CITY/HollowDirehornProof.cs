using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HollowDirehornProof
{
    // data/rules-evidence/corpses/direhorn-{already-reborn,dying-source}.yaml (C/D; owner assumptions).
    internal static void Run() => DeathKnightC1Proof.Case("DINO_416", Unpinned_HollowDirehorn);
    private static void Unpinned_HollowDirehorn(Game g, Controller p, string id)
    {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && source[GameTag.REBORN] == 0, "printed Rush, no initial Reborn");
        Kill(g, Summon(p.Opponent, "CS2_101t"));
        Core2026Proof.Check(CorpseRules.Available(p) == 0 && source[GameTag.REBORN] == 0, "opponent death does not trigger");
        Kill(g, Summon(p, "CS2_101t"));
        Core2026Proof.Check(CorpseRules.Available(p) == 1 && source[GameTag.REBORN] == 0, "shortfall spends nothing");
        CorpseRules.Gain(p, 1);
        PoolPilotProof.Resume(g, next => Kill(next, Summon(next.Player1, "CS2_101t")));
        Core2026Proof.Check(source[GameTag.REBORN] == 1 && CorpseRules.Available(p) == 0, "death's new Corpse funds exact three");
        CorpseRules.Gain(p, 2); Kill(g, Summon(p, "CS2_101t"));
        Core2026Proof.Check(source[GameTag.REBORN] == 1 && CorpseRules.Available(p) == 0, "already Reborn still spends three");
        source.Silence(); CorpseRules.Gain(p, 3); Kill(g, Summon(p, "CS2_101t"));
        Core2026Proof.Check(source[GameTag.REBORN] == 0 && CorpseRules.Available(p) == 4, "Silence removes trigger");
        StandardSetsProof.ParkBoard(p);
        source = Summon(p, id); var other = Summon(p, "CS2_101t"); int before = CorpseRules.Available(p);
        source.ToBeDestroyed = true; other.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && !p.BoardZone.Any(m => m.Card.Id == id)
            && CorpseRules.Available(p) == before + 2, "dying source cannot spend or acquire Reborn in same batch");
        Kill(g, Summon(p, "CS2_101t")); Core2026Proof.Check(CorpseRules.Available(p) == before + 3, "source leaves, no lingering trigger");
    }
}
