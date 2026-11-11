using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MoraggProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_906", (g, p, id) => {

        var demon = Entity.FromCard(p, Cards.FromId("EX1_301")); p.DeckZone.Add(demon);
        var board = DeathrattleSlotBoard(p, () => Summon(p, id)); var left = board.Left; var right = board.Right;
        var source = (Minion)p.BoardZone.GetSlot(2);
        Kill(g, source); Core2026Proof.Check(demon.Zone == p.BoardZone && !p.DeckZone.Contains(demon), "recruits original Demon from deck");
        Core2026Proof.Check(demon.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_906e"), "recruit gains exact deathrattle enchantment");
        CheckDeathrattleSlot(p, board, new[] { demon }, "recruit fills the dying Moragg's physical slot");
        var clone = g.CloneExact(); Kill(clone, (Minion)clone.IdEntityDic[demon.Id]);
        Core2026Proof.Check(clone.Player1.BoardZone.Count(m => m.Card.Id == id) == 1 && !p.BoardZone.Any(m => m.Card.Id == id), "clone deathrattle isolated");
        Kill(g, (Minion)demon); source = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 5 && source[GameTag.PREPARE] == 1, "granted deathrattle summons fresh Moragg");
        CheckDeathrattleSlot(p, board, new[] { source }, "granted Deathrattle fills the dying recruit's physical slot");
        Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 2, "no Demon in deck produces nothing");
        ParkBoard(p);
        demon = Entity.FromCard(p, Cards.FromId("EX1_301")); p.DeckZone.Add(demon);
        source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(demon.Zone == p.DeckZone, "Silence removes recruitment");
        source = Summon(p, id); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(demon.Zone == p.DeckZone && p.BoardZone.Count == 7, "full board leaves deck untouched");
    });
}
