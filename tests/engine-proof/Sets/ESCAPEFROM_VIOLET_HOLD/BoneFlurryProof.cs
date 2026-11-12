using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BoneFlurryProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_445", (g, p, id) => {

        var source = Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.NumFriendlyMinionsThatDiedThisTurn == 0, "no friendly death: three missiles");
        var enemy = Summon(p.Opponent, "EX1_008"); Kill(g, enemy);
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "opponent death does not enable bonus");
        var own = Summon(p, "EX1_008"); Kill(g, own); int cursor = 0; var flurry = Play(g, p, id);
        SplitMissileHistory.Flat(g, cursor, flurry, 6);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 12 && p.Hero.Damage == 0, "friendly death enables six missiles");
        Summon(p, "EX1_012"); Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 20, "each printed damage packet independently gains spell damage");
        End(g); End(g); p.Opponent.Hero.Damage = 0; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.NumFriendlyMinionsThatDiedThisTurn == 0, "turn boundary clears friendly-death condition");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        var clone = g.CloneExact(history: true); Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "missile target selection deterministic across clone");
        Core2026Proof.Check(shield.Zone == p.Opponent.GraveyardZone || shield.Damage == 0, "dead minions removed from subsequent missile selection");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); End(g); End(g);
        p.Opponent.Hero.Armor = 100; var victim = Summon(p, "EX1_008");
        for (int i = 0; i < 7; i++) Summon(p.Opponent, "GVG_076");
        Core2026Proof.Check(p.NumFriendlyMinionsThatDiedThisTurn == 0, "no friendly deaths before first packet");
        Play(g, p, id);
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.Opponent.BoardZone.Count == 0 && p.Opponent.Hero.Armor == 95,
            "first-packet Sheep deathrattle kills friendly minion; second packet observes real core death counter");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); End(g); End(g);
        p.Opponent.Hero.Armor = 100; p.Opponent.Hero.Damage = 0;
        var velen = Summon(p, "EX1_350"); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 94, "Velen doubles three missiles to six");
        Summon(p, "EX1_012"); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 86, "Velen doubles base plus spell damage to eight missiles");
        Kill(g, Summon(p, "EX1_008")); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 70, "Velen doubles both qualifying packets independently");
        velen.Silence(); g.DeathProcessingAndAuraUpdate(); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 62, "silenced Velen no longer doubles either packet");
    });
}
