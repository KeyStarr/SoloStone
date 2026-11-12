using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WaveOfTarProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_439", (g, p, id) => {
        var own = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var hand = Add(p.Opponent, "CS2_182"); var spell = Add(p.Opponent, "CS2_029"); var friendly = Add(p, "CS2_182");
        var cast = Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 2 && own.Damage == 0 && p.Opponent.Hero.Damage == 0, "two damage hits enemy minions only");
        Core2026Proof.Check(cast.Zone == p.GraveyardZone && hand.Cost == 6 && spell.Cost == 4 && friendly.Cost == 4, "minion tax survives spell leaving play; spells and own hand unaffected");
        var later = Add(p.Opponent, "CS2_182"); g.AuraUpdate(); Core2026Proof.Check(later.Cost == 6, "newly drawn enemy minions taxed");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player2.HandZone.First(m => m.Id == hand.Id).Cost == 6, "tax persists throughout opponent turn"); End(clone);
        Core2026Proof.Check(clone.Player2.HandZone.First(m => m.Id == hand.Id).Cost == 4 && hand.Cost == 6, "clone expiry independent");
        End(g); Core2026Proof.Check(hand.Cost == 6, "own turn end does not expire tax"); End(g);
        Core2026Proof.Check(hand.Cost == 4 && later.Cost == 4, "opponent turn end expires tax");
        ClearBoard(g, p.Opponent); Summon(p, "CS2_142"); enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 3, "damage scales with Spell Damage");
        Play(g, p, id); Core2026Proof.Check(hand.Cost == 8, "multiple spell taxes stack");
        own.Silence(); ClearBoard(g, p); End(g); End(g);
        Core2026Proof.Check(hand.Cost == 4, "Silence and source removal do not cancel spell tax; both taxes expire");
    });
}
