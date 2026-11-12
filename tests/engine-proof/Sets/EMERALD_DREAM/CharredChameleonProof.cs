using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class CharredChameleonProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_908", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_06"));
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory && Cards.FromId("FIR_908e3").AssetId == 121681, "history consumer and exact buff identity");
        var friendly = (Minion)Play(g, p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id).All(t => t.Target == null), "no Hero Power means no target");
        PlayEntity(g, p, source);
        Core2026Proof.Check(friendly.AttackDamage == 4 && friendly.Health == 5 && !friendly.IsRush, "unmet condition no buff");
        End(g); p.Opponent.UsedMana = 0;
        Core2026Proof.Check(Generic.HeroPower(p.Opponent, p.Hero), "opponent uses power"); End(g);
        var opponentOnly = (Minion)Play(g, p, id);
        Core2026Proof.Check(!opponentOnly.IsRush && friendly.AttackDamage == 4, "opponent power does not enable buff");
        // A zero-Mana committed Hero Power is still used; do not test the paid amount.
        CostRules.AddNextHeroPowerRule(p, setCost: 0);
        Core2026Proof.Check(Generic.HeroPower(p), "zero-cost own power accepted");
        var target = (Minion)Play(g, p, "CS2_182"); source = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion m && m.Controller == p)
            && options.Any(t => t.Target.Id == target.Id), "condition targets friendly minions only, excludes heroes/enemies/no-target");
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (Minion)next.IdEntityDic[target.Id]));
        Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 7 && target.IsRush
            && target.AppliedEnchantments.Single().Card.Id == "FIR_908e3", "exact +1/+2 Rush grant");
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == target.Id && t.Target?.Id == enemy.Id)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == target.Id && t.Target?.Id == p.Opponent.Hero.Id), "new Rush attacks minions only");
        Kill(g, (Minion)source); End(g); End(g);
        Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 7 && target.IsRush, "buff persists after source dies and turn ends");
        var later = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == later.Id).All(t => t.Target == null), "previous-turn power not enough");
        PlayEntity(g, p, later);
        Core2026Proof.Check(target.AttackDamage == 5, "previous-turn power does not rebuff");
        target.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 5 && !target.IsRush, "Silence removes stats and Rush");
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        p.Hero.HeroPower.IsExhausted = false; p.UsedMana = 0;
        Core2026Proof.Check(Generic.HeroPower(p), "power with empty board");
        var alone = (Minion)Play(g, p, id);
        Core2026Proof.Check(alone.AttackDamage == 1 && alone.Health == 2 && !alone.IsRush, "no pre-play friendly target means no self buff");
        LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var blocked = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id)
            && !g.Process(PlayCardTask.Any(p, blocked, alone)) && blocked.Zone == p.HandZone
            && p.BoardZone.SlotCount == 7 && p.UsedMana == 0, "full mixed board rejects play without spending");
    });
}
