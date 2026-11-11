using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TricksoftheTradeProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_006", (g, p, id) => {
        var ordinary = Summon(p, "EX1_008"); var target = Summon(p.Opponent, "CS2_200");
        var held = Add(p, id); Attack(g, ordinary, p.Opponent.Hero);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "ordinary attack does not upgrade");
        PlayEntity(g, p, held, target); Core2026Proof.Check(target.Damage == 1, "baseline one damage");
        var hidden = Summon(p, "EX1_008"); hidden.HasStealth = true; Attack(g, hidden, p.Opponent.Hero);
        held = Add(p, id); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "past attack before draw does not count");
        var deckCopy = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(deckCopy);
        var clone = g.Clone(); var cloneHidden = Summon(clone.Player1, "EX1_008"); cloneHidden.HasStealth = true;
        Attack(clone, cloneHidden, clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "upgrade state clone independent");
        End(g); var enemy = Summon(p.Opponent, "EX1_008"); enemy.HasStealth = true; Attack(g, enemy, p.Hero);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && deckCopy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "enemy Stealth attack qualifies only held copy"); End(g);
        Summon(p, "EX1_012"); PlayEntity(g, p, held, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "upgrade persists across turns; three plus Spell Damage");
        var fresh = Add(p, id); var silenced = Summon(p, "EX1_008"); silenced.HasStealth = true; silenced.Silence();
        Attack(g, silenced, p.Opponent.Hero); Core2026Proof.Check(fresh[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "silenced Stealth cannot qualify");
        foreach (var owner in new[] { p, p.Opponent }) foreach (var card in owner.HandZone.ToArray()) { owner.HandZone.Remove(card); owner.SetasideZone.Add(card); }
        var check = Add(p, id); var options = p.Options().Where(t => t.Source?.Id == check.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == p.Hero.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id)
            && options.Any(t => t.Target?.Id == ordinary.Id) && options.Any(t => t.Target?.Id == target.Id)
            && options.All(t => t.Target != null), "requires any valid character target");
    });
}
