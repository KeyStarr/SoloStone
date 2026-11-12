using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WiltedShadowProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_821", Unpinned_Wilted);
    private static void Unpinned_Wilted(Game g, Controller p, string id)
    {
        var shadow = Summon(p, id); var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 20;
        var spell = Add(p, "CS1_130"); p.Hero.Damage = 15;
        var multiple = g.CloneExact(history: true); Summon(multiple.Player1, id);
        ((Minion)multiple.IdEntityDic[enemy.Id]).TakeHeal((IPlayable)multiple.IdEntityDic[spell.Id], 3);
        Core2026Proof.Check(((Minion)multiple.IdEntityDic[enemy.Id]).Damage == 9,
            "Unpinned_Wilted both forced combats resolve before one heal");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var victim = (Minion)next.IdEntityDic[enemy.Id];
            victim.TakeHeal((IPlayable)next.IdEntityDic[spell.Id], 3);
            Core2026Proof.Check(victim.Damage == 3 && next.Player1.Hero.Damage == 9,
                "full-health intent triggers attack; Unpinned_Wilted heal clamps after combat; Lifesteal resolves");
        }
        var doomed = Summon(p.Opponent, "CS2_231"); doomed.TakeHeal(spell, 10);
        Core2026Proof.Check(doomed.Zone.Type == Zone.GRAVEYARD, "Unpinned_Wilted death resolves before heal; cannot rescue");
        shadow.Silence(); int before = enemy.Damage; enemy.TakeHeal(spell, 1);
        Core2026Proof.Check(enemy.Damage == before - 1, "Silence removes heal-intent listener");
        ParkBoard(p); var other = Summon(p, id); ParkBoard(p); before = enemy.Damage; enemy.TakeHeal(spell, 1);
        Core2026Proof.Check(enemy.Damage == before - 1 && other.Zone.Type == Zone.SETASIDE, "leave-play removes listener");
    }
}
