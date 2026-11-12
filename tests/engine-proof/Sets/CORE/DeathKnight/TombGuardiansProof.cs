using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class TombGuardiansProof
{
    // data/rules-evidence/corpses/{summoned-token-corpses,bonus-full-board,bonus-partial-board,fixed-spend-shortfall}.yaml
    // A/C/D; owner assumptions: fixed payment even with partial/full board; shortfall spends zero.
    internal static void Run() => DeathKnightC1Proof.Case("CORE_RLK_118", Unpinned_TombGuardians);
    private static void Unpinned_TombGuardians(Game g, Controller p, string id)
    {
        CorpseRules.Gain(p, 4 - 1);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        Play(g, p, id);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "RLK_118t3").ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 2 && m.Health == 2 && m.HasTaunt == true && m.IsRace(Race.UNDEAD)
            && m[GameTag.REBORN] == 0), "exact tokens without unfunded bonus");
        Core2026Proof.Check(CorpseRules.Available(p) == 4 - 1 && tokens[0].ZonePosition == 2
            && tokens.Select(m => m.Id).SequenceEqual(tokens.OrderBy(m => m.Id).Select(m => m.Id)), "shortfall spends zero; appends in creation order after location");
        int corpses = CorpseRules.Available(p); Kill(g, tokens[0]);
        Core2026Proof.Check(CorpseRules.Available(p) == corpses + 1, "ordinary summoned token leaves Corpse");
        StandardSetsProof.ParkBoard(p);
        CorpseRules.Gain(p, 4 - CorpseRules.Available(p));
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
        tokens = p.BoardZone.ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m[GameTag.REBORN] == 1)
            && CorpseRules.Available(p) == 0, "exact affordable bonus once for all summons");
        tokens[0].Silence(); Core2026Proof.Check(tokens[0][GameTag.REBORN] == 0, "Silence removes granted keyword");
        End(g); End(g); Core2026Proof.Check(tokens.Skip(1).All(m => m[GameTag.REBORN] == 1), "bonus permanent");
        StandardSetsProof.ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        CorpseRules.Gain(p, 4); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Last().Card.Id == "RLK_118t3"
            && p.BoardZone.Last()[GameTag.REBORN] == 1 && CorpseRules.Available(p) == 0, "one physical slot pays entire bonus");
        CorpseRules.Gain(p, 4);
        var forced = Add(p, id);
        PoolPilotProof.Resume(g, next => PoolPilotProof.Resolve(next, next.IdEntityDic[forced.Id]));
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && CorpseRules.Available(p) == 0, "forced full-board resolution pays without summons");
    }
}
