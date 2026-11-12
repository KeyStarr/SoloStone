using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class UnnaturalColdProof
{
    internal static void Run() => StandardSetsProof.Case("BE_842", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == enemy.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id) && options.All(t => t.Target is ICharacter && t.Target.Controller == p.Opponent), "required enemy character target, including hero; friends excluded");
        int deck = p.DeckZone.Count; PlayEntity(g, p, spell, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.IsFrozen && p.Hero.Armor == 3 && p.DeckZone.Count == deck - 1 && p.HandZone.Single().Card.Id == "CS2_182", "freeze enemy hero, gain three Armor, draw one");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.IsFrozen && !friend.IsFrozen && p.Hero.Armor == 6 && p.DeckZone.Count == deck - 2, "enemy minion target and additive Armor");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        spell.Card.Power.PowerTask.Process(g, p, spell, enemy, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 3 && p.Hero.Armor == 9, "full hand burns draw but retains freeze and Armor");
    });
}
