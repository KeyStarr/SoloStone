using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScorchfistScholarProof
{
    internal static void Run() => StandardSetsProof.Case("BE_303", (g, p, id) => {
        p.Opponent.Hero.Armor = 100;
        Play(g, p, "BE_842", p.Opponent.Hero); // Cast before holding does not count.
        var unheld = (Minion)Play(g, p, id);
        Core2026Proof.Check(unheld[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && p.BoardZone.Count == 1, "earlier spells excluded");
        ClearBoard(g, p);
        var source = (Minion)Add(p, id);
        Play(g, p, "GAME_005"); Play(g, p, "CS2_182");
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "schoolless spell and minion excluded");
        Play(g, p, "BE_842", p.Opponent.Hero); Play(g, p, "BE_842", p.Opponent.Hero);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "same school twice counts once");
        End(g); Play(g, p.Opponent, "CORE_CS2_029", p.Hero); End(g);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "opponent spell excluded; held progress survives turn");
        var clone = g.Clone(); var cp = clone.Player1;
        Play(clone, cp, "CORE_CS2_029", cp.Opponent.Hero);
        Core2026Proof.Check(clone.IdEntityDic[source.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "held school mask clone isolated");
        int health = p.Opponent.Hero.Health; int armor = p.Opponent.Hero.Armor;
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 1 && p.Opponent.Hero.Health == health && p.Opponent.Hero.Armor == armor, "one-school battlecry inactive");
        ClearBoard(g, p);
        source = (Minion)Add(p, id);
        Play(g, p, "BE_842", p.Opponent.Hero); Play(g, p, "CORE_CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "two different held schools tracked");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p.Opponent, "EX1_008");
        shield.HasDivineShield = true; armor = p.Opponent.Hero.Armor;
        PlayEntity(g, p, source);
        Core2026Proof.Check(enemy.Health == 1 && shield.Health == 1 && !shield.HasDivineShield && friend.Health == 5 && source.Health == 3 && p.Opponent.Hero.Armor == armor - 4, "four damage to all enemies only; shield absorbs");
        Play(g, p, "BE_228", source);
        Core2026Proof.Check(source.IsSilenced && source.ActivatedTrigger == null, "silence leaves no own trigger");
        Kill(g, source); Play(g, p, "BE_842", p.Opponent.Hero);
        Core2026Proof.Check(source.ActivatedTrigger == null, "no hand trigger after source leaves play");
    });
}
