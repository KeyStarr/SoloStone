using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PrimordialOverseerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_213", (g, p, id) => {
        Play(g, p, "EX1_238", p.Opponent.Hero);
        var no = (Minion)Play(g, p, id); Core2026Proof.Check(no.AttackDamage == 2 && no.Health == 3, "Nature cast before holding does not count");
        var away = Add(p, id); p.HandZone.Remove(away); p.SetasideZone.Add(away);
        var held = Add(p, id); var wrong = Add(p, id); Play(g, p, "CS2_029", p.Opponent.Hero);
        PlayEntity(g, p, wrong); Core2026Proof.Check(((Minion)wrong).AttackDamage == 2, "non-Nature cast ignored");
        End(g); Play(g, p.Opponent, "EX1_238", p.Hero); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "opponent Nature spell ignored");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "EX1_238", clone.Player2.Hero); PlayEntity(clone, clone.Player1, clone.IdEntityDic[held.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[held.Id]).AttackDamage == 3 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "clone hand memory isolated");
        Play(g, p, "CORE_EX1_169"); Play(g, p, "EX1_238", p.Opponent.Hero);
        Core2026Proof.Check(away[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && wrong[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "setaside and played minions do not track casts");
        int deck = p.DeckZone.Count; PlayEntity(g, p, held);
        Core2026Proof.Check(((Minion)held).AttackDamage == 3 && ((Minion)held).Health == 4 && p.DeckZone.Count == deck - 1, "multiple Nature spells while held buff once and draw once");
        Play(g, p, "BE_228", (Minion)held); Core2026Proof.Check(((Minion)held).AttackDamage == 2 && ((Minion)held).Health == 3, "Silence removes gains");
        Generic.ReturnToHandBlock(p, (Minion)held); PlayEntity(g, p, held);
        Core2026Proof.Check(((Minion)held).AttackDamage == 2 && ((Minion)held).Health == 3, "return to hand resets casting memory");
    });
}
