using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PrimusofPowerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_171", (g, p, id) => {
        p.Opponent.Hero.Armor = 100;
        Play(g, p, "BE_842", p.Opponent.Hero);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && p.BoardZone.Count(m => m.Card.Id == id) == 1, "unheld spell excluded; printed Taunt");
        ClearBoard(g, p);
        var clean = g.CloneExact(history: true);
        source = (Minion)Add(p, id);
        Play(g, p, "GAME_005"); Play(g, p, "CS2_182");
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "schoolless spell and minion excluded");
        Play(g, p, "BE_842", p.Opponent.Hero); Play(g, p, "BE_842", p.Opponent.Hero);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "same held school counts once");
        End(g); Play(g, p.Opponent, "CORE_CS2_029", p.Hero); End(g);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "opponent spell excluded; progress survives turns");
        var clone = g.Clone(); var cp = clone.Player1;
        Play(clone, cp, "CORE_CS2_029", cp.Opponent.Hero);
        Core2026Proof.Check(clone.IdEntityDic[source.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "held mask isolated in clone");
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 1, "one school does not activate Battlecry");
        cp = clone.Player1;
        var held = (Minion)clone.IdEntityDic[source.Id]; held.AttackDamage = 7; held.BaseHealth = 8;
        Play(clone, cp, "EX1_366");
        var left = cp.BoardZone.First(); var right = Summon(cp, "CS2_182");
        cp.UsedMana = 0;
        Core2026Proof.Check(clone.Process(PlayCardTask.Any(cp, held, zonePosition: 1)), "qualified middle Primus play accepted");
        var copy = cp.BoardZone.Single(m => m.Card.Id == id && m.Id != held.Id);
        Core2026Proof.Check(copy.AttackDamage == 8 && copy.BaseHealth == 9 && copy.HasTaunt && held.AttackDamage == 8 && held.BaseHealth == 9,
            "qualified copy inherits 7/8 hand stats before its Sword trigger; original gets own summon buff");
        Core2026Proof.Check(cp.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, held.Id, copy.Id, right.Id }), "copy immediately right of Primus");
        held.Silence(); Kill(clone, held); Play(clone, cp, "BE_842", cp.Opponent.Hero);
        Core2026Proof.Check(held.ActivatedTrigger == null && copy.ActivatedTrigger == null, "no held trigger on board or departed source");
        var full = clean.CloneExact(history: true); var owner = full.Player1;
        held = (Minion)Add(owner, id);
        Play(full, owner, "BE_842", owner.Opponent.Hero); Play(full, owner, "CORE_CS2_029", owner.Opponent.Hero);
        while (owner.BoardZone.Count < 6) Summon(owner, "CS2_182");
        PlayEntity(full, owner, held);
        Core2026Proof.Check(owner.BoardZone.Count == 7 && owner.BoardZone.Count(m => m.Card.Id == id) == 1, "qualified full board suppresses copy");
        var summoned = Summon(clean.Player1, id);
        Play(clean, clean.Player1, "BE_842", clean.Player2.Hero); Play(clean, clean.Player1, "CORE_CS2_029", clean.Player2.Hero);
        Core2026Proof.Check(summoned[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && summoned.ActivatedTrigger == null, "spells after summon do not accumulate held progress");
    });
}
