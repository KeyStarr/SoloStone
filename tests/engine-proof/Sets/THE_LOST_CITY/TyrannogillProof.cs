using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TyrannogillProof
{
    internal static void Run() => Unpinned_Tyrannogill();
    private static void Unpinned_Tyrannogill() => StandardSetsProof.Case("TLC_240", (g, p, id) => {
        var board = DeathrattleSlotBoard(p, () => PlayAt(g, p, id, 2));
        var m = p.BoardZone.Single(x => x.Card.Id == id); Core2026Proof.Check(m.IsRush, "printed Rush");
        Kill(g, m);
        var tokens = p.BoardZone.Where(x => x.Card.Id.StartsWith("TLC_240t")).ToArray();
        Core2026Proof.Check(tokens.Select(x => x.Card.Id).SequenceEqual(new[] { "TLC_240t", "TLC_240t2", "TLC_240t3" })
            && tokens.All(x => x.AttackDamage == 2 && x.BaseHealth == 1 && x.IsRace(Race.MURLOC) && BonusEffectsProof.Count(x) == 1), "three exact Dinolocs with independent grants");
        CheckDeathrattleSlot(p, board, tokens, "Dinolocs retain physical Deathrattle slot");
        ParkBoard(p); var shortSource = (Minion)Play(g, p, id);
        for (int i=0;i<5;i++) Summon(p, "CS2_182");
        Kill(g, shortSource);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(x => x.Card.Id.StartsWith("TLC_240t")) == 2, "board capacity grants only successful summons");
        End(g);
    });
}
