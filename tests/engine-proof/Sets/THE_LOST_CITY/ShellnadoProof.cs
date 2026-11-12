using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShellnadoProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_601", (g, p, id) => {
        var friend = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id); Core2026Proof.Check(friend.Damage == 0 && enemy.Damage == 0, "zero Armor produces zero waves");
        p.Hero.Armor = 8; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 3 && friend.Damage == 5 && enemy.Damage == 5 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "spends at most five Armor; five waves on both boards only");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); friend = Summon(p, "CS2_200"); friend.HasDivineShield = true;
        enemy = Summon(p.Opponent, "EX1_556"); Summon(p, "CS2_052"); p.Hero.Armor = 3;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 0 && !friend.HasDivineShield && friend.Damage == 2, "spends remaining Armor; Spell Damage rechecked each wave after totem dies");
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Opponent.BoardZone.Count == 0, "later wave kills Deathrattle token from earlier wave");
        ArmorLossStopsWaves(g, p, id);
        ArmorGainsSustainWaves(g, p, id);
    });

    private static void ArmorGainsSustainWaves(Game g, Controller p, string id)
    {
        foreach (int initialArmor in new[] { 1, 3 })
        {
            ClearBoard(g, p); ClearBoard(g, p.Opponent);
            var survivor = Summon(p, "CS2_200");
            var vanguard = Summon(p, "BE_572"); vanguard.Damage = 3;
            p.Hero.Armor = initialArmor;
            Play(g, p, id);
            Core2026Proof.Check(vanguard.Zone == p.GraveyardZone && survivor.Damage == (initialArmor == 1 ? 4 : 5)
                && p.Hero.Armor == (initialArmor == 1 ? 0 : 1),
                "Deathrattle Armor sustains later waves, capped at five; initial Armor=" + initialArmor);
        }
    }

    private static void ArmorLossStopsWaves(Game g, Controller p, string id)
    {
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var cinder = Summon(p.Opponent, "TLC_249");
        var survivor = Summon(p.Opponent, "CS2_200");
        p.Hero.Armor = 3; p.Hero.Damage = 0;
        Play(g, p, id);
        Core2026Proof.Check(cinder.Zone == p.Opponent.GraveyardZone && survivor.Damage == 1
            && p.Hero.Armor == 0 && p.Hero.Damage == 0,
            "enemy Cinder Deathrattle spends remaining Armor and stops later waves");
    }
}
