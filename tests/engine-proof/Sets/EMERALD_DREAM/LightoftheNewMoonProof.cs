using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LightoftheNewMoonProof
{
    // Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection.
    internal static void Run() => StandardSetsProof.Case("FIR_918", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("FIR_918t", 115643); EmeraldDreamCardStateProof.Token("FIR_918e1", 121627);
        EmeraldDreamCardStateProof.HiddenUpgrade(id, "FIR_918t");
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); PoolPilotProof.EmptyHand(p);
        var baseSpell = Add(p, id); baseSpell[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        PlayEntity(g, p, baseSpell, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 7 && enemy.Health == 8 && enemy.AppliedEnchantments.Single().Card.Id == "FIR_918e1"
            && baseSpell.Card.Id == id && p.HandZone.Count == 0, "base buffs enemy +3/+3; cannot self-upgrade or return");
        enemy.Silence(); Core2026Proof.Check(enemy.AttackDamage == 4 && enemy.Health == 5, "Silence removes exact buff");
        var upgraded = EmeraldDreamCardStateProof.UpgradeBySpells(g, p, id, "FIR_918t");
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[upgraded.Id], (Minion)next.IdEntityDic[friend.Id]));
        var fresh = p.HandZone.Single(c => c.Card.Id == id);
        Core2026Proof.Check(friend.AttackDamage == 7 && friend.Health == 8 && fresh.Id != upgraded.Id && fresh.Cost == 3
            && fresh[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && fresh[GameTag.DISPLAYED_CREATOR] == upgraded.Id,
            "Unpinned_HandFresh creator adds fresh New Moon at three; no creator-cast credit");
        Kill(g, friend); Play(g, p, "GAME_005");
        Core2026Proof.Check(fresh[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "generated progress independent of target/source leaving");
        var target = Summon(p, "CS2_182"); PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
        Play(g, p, "FIR_918t", target);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == id) == 1, "casting from full hand frees exactly one slot for base copy");
        var automatic = Entity.FromCard(p, Cards.FromId("FIR_918t"));
        PoolPilotProof.Resolve(g, automatic, target);
        Core2026Proof.Check(p.HandZone.Count == 10 && target.AttackDamage == 10 && target.Health == 11
            && p.GraveyardZone.Any(c => c.Card.Id == id && c[GameTag.DISPLAYED_CREATOR] == automatic.Id),
            "full-hand body still buffs; generated New Moon burns instead of overflowing");
        PoolPilotProof.EmptyHand(p); ParkBoard(p); ParkBoard(p.Opponent);
        var blocked = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id) && !g.Process(PlayCardTask.Any(p, blocked)) && blocked.Zone == p.HandZone,
            "no minion: no untargeted or hero play");
        var sleeper = Summon(p, "TIME_046");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id && t.Target?.Id == sleeper.Id), "Dormant minion cannot receive buff");
        var elusive = Summon(p.Opponent, "EDR_598");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id && t.Target?.Id == elusive.Id), "Elusive spell target excluded");
    });
}
