using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class VoidlordProof
{
    internal static void Run() => Case("CORE_LOOT_368", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.AttackDamage == 1 && m.Health == 3 && m.HasTaunt && m.IsRace(Race.DEMON)), "Voidlord summons three 1/3 Taunt Demons");
        var board = StandardSetsProof.DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        var demons = p.BoardZone.Where(m => m.Card.Id == "CS2_065").OrderBy(m => m.Id).ToArray();
        StandardSetsProof.CheckDeathrattleSlot(p, board, demons, "three Demons fill the dying Voidlord's physical slot rightward");
        StandardSetsProof.ParkBoard(p);
        source = Summon(p, id); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        var anchor = p.BoardZone.Last(); Kill(g, source);
        demons = p.BoardZone.Where(m => m.Card.Id == "CS2_065").ToArray();
        Core2026Proof.Check(demons.Length == 2 && p.BoardZone.Count == 7 && p.BoardZone[0] == demons[0] && p.BoardZone[1] == demons[1]
            && p.BoardZone.Last() == anchor, "leftmost Voidlord on a near-full board fills its slot until the board is full");
    });
}
