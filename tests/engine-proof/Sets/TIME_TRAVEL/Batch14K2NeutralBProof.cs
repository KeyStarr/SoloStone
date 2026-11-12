using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Batch14K2NeutralBProof
{
    [GroupProof("b14-k2-neutral-b-cards", ProofKind.Cards)]
    internal static void Run()
    {
        Whelp();
        Batch14K2NeutralBLostCityProof.Run();
    }

    private static void Whelp() => StandardSetsProof.Case("TIME_045", (g, p, id) => {
        Minion source = null;
        var slots = DeathrattleSlotBoard(p, () => source = PlayAt(g, p, id, 2));
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 4 && source.Poisonous && source[GameTag.REBORN] == 1
            && source.IsRace(Race.DRAGON), "printed 1/4 Poisonous Reborn Dragon");
        var enemy = Summon(p.Opponent, "CS2_186");
        Attack(g, source, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone, "one Poisonous damage kills five-Health minion");
        var reborn = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(reborn.Id != source.Id && reborn.Health == 1 && reborn.BaseHealth == 4
            && reborn.Poisonous && reborn[GameTag.REBORN] == 0, "fresh one-Health reborn retains Poisonous, consumes Reborn");
        CheckDeathrattleSlot(p, slots, new[] { reborn }, "Reborn uses physical death slot");
        Kill(g, reborn);
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == id), "Reborn happens only once");
        ParkBoard(p); ParkBoard(p.Opponent);
        source = (Minion)Play(g, p, id); enemy = Summon(p.Opponent, "CS2_171"); enemy.HasDivineShield = true;
        Attack(g, source, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone && !enemy.HasDivineShield && enemy.Damage == 0,
            "shielded target receives no Poisonous kill");
        source.NumAttacksThisTurn = 0; Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == 29, "Poisonous does not kill hero");
        source.Silence();
        Core2026Proof.Check(!source.Poisonous && source[GameTag.REBORN] == 0, "Silence removes both printed keywords");
        Kill(g, source);
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == id), "Silence removes Poisonous and Reborn");
        source = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        foreach (var copy in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Kill(copy, (Minion)copy.IdEntityDic[source.Id]);
            Core2026Proof.Check(copy.Player1.BoardZone.SlotCount == 7 && copy.Player1.BoardZone.Single(m => m.Card.Id == id).Health == 1,
                "full board frees one physical slot for Reborn in clone/load");
        }
        Core2026Proof.Check(source[GameTag.REBORN] == 1 && source.Health == 4, "clone Reborn leaves original independent");
    });

}
