using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class HallsofOriginationProof
{
    internal static void Run() => StandardSetsProof.Case("BE_183", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); p.Hero.Damage = 20; p.Opponent.Hero.Armor = 12;
        var spell = Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id]));
        SplitMissileHistory.Flat(g, 0, spell, 8);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 4 && p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 12 && friendly.Damage == 0,
            "Lean_LifestealArmorDamage: eight split missiles heal eight Armor damage; friendly excluded");
        BEPoolProof.Outputs(g, p, spell, 1, 8);
        int child = BEPoolProof.Children(p, spell).Single().Id;
        var hits = g.HistoryFacts.Query.Damage().Where(f => f.SourceId == spell.Id).ToArray();
        Core2026Proof.Check(hits.Length == 8 && hits.All(f => f.Amount == 1)
            && hits.Last().Sequence < g.HistoryFacts.Query.Summons().Single(f => f.EntityId == child).Sequence,
            "eight individual missiles finish before the 8-Cost summon");
        ParkBoard(p); Summon(p, "CS2_052"); p.Hero.Damage = 20; p.Opponent.Hero.Armor = 4;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 11 && p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 5,
            "Lean_LifestealArmorDamage: Spell Damage adds missile, mixed Armor/Health heals nine");
        ParkBoard(p); p.Opponent.Hero.Armor = 20; p.Hero.Damage = 20;
        var enemy = Summon(p.Opponent, "EX1_556"); enemy.Damage = 2;
        var other = Summon(p.Opponent, "CS2_182"); other.BaseHealth = 30;
        var dormant = Summon(p.Opponent, "EDR_840t2");
        g.SetRandomSeed(1);
        var split = Play(g, p, id);
        SplitMissileHistory.Flat(g, 0, split, 8);
        var death = g.HistoryFacts.Query.Deaths().Single(f => f.EntityId == enemy.Id);
        var replacement = g.HistoryFacts.Query.Summons().Single(f => f.Sequence > death.Sequence
            && f.ControllerId == p.Opponent.Id && g.IdEntityDic[f.EntityId].Card.Id == "skele21");
        var missiles = g.HistoryFacts.Query.Damage().Where(f => f.SourceId == split.Id).ToArray();
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD
            && missiles.Any(f => f.VictimId == enemy.Id && f.Sequence < death.Sequence)
            && missiles.Any(f => f.VictimId == replacement.EntityId && f.Sequence > replacement.Sequence),
            "Lean_HallsMissileDeathrattles: lethal missile, Deathrattle summon, later missile hits replacement");
        Core2026Proof.Check(20 - p.Hero.Damage == 8 && 20 - p.Opponent.Hero.Armor
            + g.IdEntityDic.Values.OfType<Minion>().Where(m => m.Controller == p.Opponent).Sum(m => m.Damage) - 2 == 8
            && dormant.Damage == 0, "eight one-damage missiles split among enemies, including hero; Dormant excluded");
        ParkBoard(p); ParkBoard(p.Opponent);
        BEPoolProof.Restricted(g, id, 0, () => { int before = p.Opponent.Hero.Armor; var s = Play(g, p, id);
            Core2026Proof.Check(p.Opponent.Hero.Armor == before - 8 && BEPoolProof.Children(p, s).Length == 0, "empty summon pool preserves damage"); });
        BEPoolProof.Restricted(g, id, 1, () => BEPoolProof.Outputs(g, p, Play(g, p, id), 1, 8));
        ParkBoard(p); BEPoolProof.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        p.Opponent.Hero.Armor = 20; var full = Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 12 && p.BoardZone.SlotCount == 7 && BEPoolProof.Children(p, full).Length == 0,
            "full physical board prevents summon but not damage");
    });
}
