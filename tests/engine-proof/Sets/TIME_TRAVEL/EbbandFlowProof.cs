using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EbbandFlowProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_702", (g, p, id) => {
        Play(g, p, "CS2_171"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Armor == 0 && p.Opponent.Hero.Damage == 3, "play before holding gives no armor");
        var held = Add(p, id); var away = Add(p, id); p.HandZone.Remove(away); p.SetasideZone.Add(away);
        Summon(p, "CS2_182"); Play(g, p, "CS2_008", p.Opponent.Hero);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "summoning and spell casts do not count");
        End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "opponent minion play ignored");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CS2_171");
        PlayEntity(clone, clone.Player1, clone.IdEntityDic[held.Id], clone.Player2.Hero);
        Core2026Proof.Check(clone.Player1.Hero.Armor == 5 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && p.Hero.Armor == 0, "clone hand memory isolated");
        Play(g, p, "CS2_171"); Play(g, p, "CS2_171");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && away[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "held flag set once; setaside ignored");
        End(g); End(g); PlayEntity(g, p, held, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Armor == 5, "held memory persists across turns and grants armor once");
        ClearBoard(g, p); Play(g, p, "CS2_171");
        Core2026Proof.Check(held.ActivatedTrigger == null, "played spell no longer tracks minion plays");
    });
}
