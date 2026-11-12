using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ArmyOfTheDeadProof
{
    // corpses/{raise-fits-board,risen-no-corpse,risen-silence-corpse}.yaml
    internal static void Run() => DeathKnightC1Proof.Case("RLK_060", Unpinned_RaiseFitsBoard);
    private static void Unpinned_RaiseFitsBoard(Game original, Controller _, string id)
    {
        foreach (int corpses in new[] { 0, 2, 8 })
        foreach (int slots in new[] { 0, 1, 2, 7 })
        {
            var game = DeathKnightC1Proof.NewGame(original.Seed); var p = game.Player1;
            LocationProofs.Location(p);
            while (p.BoardZone.FreeSpace > Math.Min(slots, 6)) Summon(p, "CS2_182");
            if (slots == 7) StandardSetsProof.ParkBoard(p);
            CorpseRules.Gain(p, corpses + 3); CorpseRules.TrySpend(p, 3);
            int count = Math.Min(5, Math.Min(corpses, slots));
            PoolPilotProof.Resume(game, next => {
                if (slots == 0) PoolPilotProof.Resolve(next, Add(next.Player1, id));
                else Play(next, next.Player1, id);
            });
            var tokens = p.BoardZone.Where(m => m.Card.Id == "RLK_008t").ToArray();
            Core2026Proof.Check(tokens.Length == count && p[GameTag.CORPSES_SPENT_THIS_GAME] == 3 + count
                && CorpseRules.Available(p) == corpses - count, "Unpinned_Army spends only fitting raises");
            Core2026Proof.Check(tokens.All(m => m.AttackDamage == 2 && m.Health == 2 && m.IsRush && m.IsRace(Race.UNDEAD)
                && m[(GameTag)2217] == 1 && m[GameTag.DISPLAYED_CREATOR] != 0), "Army exact Risen Ghouls");
            foreach (var token in tokens) { token.Silence(); Kill(game, token); }
            Core2026Proof.Check(CorpseRules.Available(p) == corpses - count, "Unpinned_Army silenced Risen leave no Corpses");
        }
    }
}
