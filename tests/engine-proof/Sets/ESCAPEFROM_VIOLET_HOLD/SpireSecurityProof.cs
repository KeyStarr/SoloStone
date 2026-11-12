using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpireSecurityProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_379", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0, "no spell means no damage");
        var expensiveMinion = Entity.FromCard(p, Cards.FromId("CS2_200")); p.DeckZone.Add(expensiveMinion);
        var revealsBefore = g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Count(b => b.BlockType == BlockType.REVEAL_CARD);
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 0 && expensiveMinion.Zone == p.DeckZone
            && g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Count(b => b.BlockType == BlockType.REVEAL_CARD) == revealsBefore, "expensive-minion-only deck reveals nothing and deals no damage");
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(spell);
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0 && spell.Zone == p.DeckZone && p.HandZone.Count == 0, "four-cost spell revealed but not drawn, no damage");
        var reveal = g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Last(b => b.BlockType == BlockType.REVEAL_CARD);
        Core2026Proof.Check(reveal.Target == spell.Id && g.IdEntityDic[reveal.Target] is Spell && expensiveMinion.Zone == p.DeckZone, "mixed deck reveals the spell, never the expensive minion");
        spell.Cost = 5; var friendly = Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friendly.Damage == 0 && p.Opponent.Hero.Damage == 0 && spell.Zone == p.DeckZone, "current five-cost spell deals five only among enemy minions");
        Core2026Proof.Check(g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
            .Where(b => b.BlockType == BlockType.REVEAL_CARD).All(b => b.Target == spell.Id && g.IdEntityDic[b.Target] is Spell), "mixed deck always reveals a spell at either cost");
        var one = Summon(p.Opponent, "CS2_182"); var two = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(one.Damage + two.Damage == 5, "five damage split among random targets");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "no enemy minions does not hit hero");
        Core2026Proof.Check(g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>().Any(b => b.BlockType == BlockType.REVEAL_CARD), "reveal history emitted");
    });
}
