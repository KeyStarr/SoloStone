using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class BoneguardCommanderProof
{
    // corpses/{raise-fits-board,risen-no-corpse,risen-silence-corpse}.yaml
    internal static void Run() => DeathKnightC1Proof.Case("CORE_RLK_506", Unpinned_RaiseFitsBoard);
    private static void Unpinned_RaiseFitsBoard(Game original, Controller _, string id)
    {
        foreach (int corpses in new[] { 0, 2, 8 })
        foreach (int slots in new[] { 1, 2, 3, 7 })
        {
            var game = DeathKnightC1Proof.NewGame(original.Seed); var p = game.Player1;
            while (p.BoardZone.FreeSpace > slots) Summon(p, "CS2_182");
            CorpseRules.Gain(p, corpses);
            PoolPilotProof.Resume(game, next => Play(next, next.Player1, id));
            var source = p.BoardZone.Single(m => m.Card.Id == id);
            var tokens = p.BoardZone.Where(m => m.Card.Id == "RLK_061t").ToArray();
            int count = Math.Min(6, Math.Min(corpses, slots - 1));
            Core2026Proof.Check(source.AttackDamage == 8 && source.Health == 8 && source.HasTaunt
                && tokens.Length == count && CorpseRules.Available(p) == corpses - count,
                "Unpinned_Commander counts its own occupied slot");
            Core2026Proof.Check(tokens.All(m => m.AttackDamage == 1 && m.Health == 3 && m.HasTaunt && m.IsRace(Race.UNDEAD)
                && m[(GameTag)2217] == 1 && m[GameTag.DISPLAYED_CREATOR] == source.Id), "Commander exact Risen Footmen");
            foreach (var token in tokens) Kill(game, token);
            Core2026Proof.Check(CorpseRules.Available(p) == corpses - count, "PinnedRule_Commander Risen deaths leave no Corpse");
        }
    }
}
