using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B15PlayLocksProof
{
    [GroupProof("b15-play-locks-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_215", Daze);
        StandardSetsProof.Case("MEND_044", Unpinned_TranquilSleep);
        B15EmeraldPlayLocksProof.Run();
        B15WingProof.Run();
    }

    private static void Daze(Game game, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182");
        var friendly = Summon(p, "CS2_182");
        var spell = Add(p, id);
        Core2026Proof.Check(spell.ValidPlayTargets.Contains(enemy) && !spell.ValidPlayTargets.Contains(friendly), "Daze enemy minion targeting");
        PlayEntity(game, p, spell, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.HandZone && enemy[GameTag.CANT_PLAY] == 1, "bounce attaches next-turn lock");
        foreach (var next in new[] { game.CloneExact(true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var victim = next.IdEntityDic[enemy.Id];
            End(next); Core2026Proof.Check(!victim.IsPlayableByPlayer, "Daze blocks victim's next turn");
            End(next); Core2026Proof.Check(victim[GameTag.CANT_PLAY] == 0, "Daze expires at victim end");
        }
        while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182");
        enemy = Summon(p.Opponent, "CS2_182");
        Play(game, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && enemy[GameTag.CANT_PLAY] == 0
            && !(enemy.AppliedEnchantments?.Any() ?? false), "full-hand bounce burns without stale lock");
    }

    private static void Unpinned_TranquilSleep(Game game, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id);
        var target = Summon(p.Opponent, "CS2_182");
        int health = target.BaseHealth;
        LocationProofs.Use(location, target);
        Core2026Proof.Check(target.BaseHealth == health + 2 && target.HasTaunt && !target.IsDormant && target.CantAttack,
            "Unpinned_Tranquil grants separate stats/Taunt and targetable sleep");
        foreach (var next in new[] { game.CloneExact(true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var minion = (Minion)next.IdEntityDic[target.Id];
            new ControlTask(EntityType.TARGET).Process(next, next.Player1, next.Player1.Hero, minion);
            End(next); Core2026Proof.Check(minion.CantAttack, "Unpinned_Tranquil first caster end still asleep");
            End(next); Core2026Proof.Check(minion.CantAttack, "Unpinned_Tranquil opponent end still asleep");
            minion.NumAttacksThisTurn = 1;
            End(next);
            Core2026Proof.Check(!minion.CantAttack && minion.BaseHealth == health + 2 && minion.HasTaunt, "Unpinned_Tranquil caster next end removes only sleep, no attack refund");
        }
        var silent = PlayLockRulesProof.Fixture();
        location = LocationProofs.Play(silent.Player1, id);
        target = Summon(silent.Player1, "CS2_182"); target.IsExhausted = false;
        LocationProofs.Use(location, target);
        target.NumAttacksThisTurn = 1; target.IsExhausted = true;
        target.Silence();
        Core2026Proof.Check(target.NumAttacksThisTurn == 1 && target.IsExhausted && !target.CantAttack && !target.HasTaunt && target.BaseHealth == target.Card.Health,
            "Unpinned_TranquilSilence removes sleep and stats immediately");
        target.CantAttack = true;
        End(silent); End(silent); End(silent);
        Core2026Proof.Check(target.CantAttack, "cancelled sleep cannot later clear unrelated restriction");
        var bounced = PlayLockRulesProof.Fixture();
        location = LocationProofs.Play(bounced.Player1, id);
        target = Summon(bounced.Player1, "CS2_182");
        LocationProofs.Use(location, target);
        Core2026Proof.Check(Generic.ReturnToHandBlock(bounced.Player1, target) && !PlayLockRules.IsSleeping(target)
            && target[GameTag.CANT_ATTACK] == 0, "Unpinned_Tranquil bounce cancels sleep");
        Core2026Proof.Check(!target.AppliedEnchantments.Any(e => e.Card.Id == "MEND_044e4"),
            "Tranquil bounce removes native strength attachment with reset stats");
        PlayEntity(bounced, bounced.Player1, target);
        End(bounced); End(bounced); End(bounced);
        Core2026Proof.Check(!target.CantAttack, "old sleep expiry cannot affect later board residence");
        var copies = PlayLockRulesProof.Fixture();
        location = LocationProofs.Play(copies.Player1, id);
        target = Summon(copies.Player2, "CS2_182");
        LocationProofs.Use(location, target);
        var copy = (Minion)Generic.Copy(copies.Player2, copies.Player2.Hero, target, Zone.PLAY, preserveBoardMinionState: true);
        target.Silence();
        Core2026Proof.Check(copy.CantAttack && !target.CantAttack, "sleep copy has independent removable attachment");
        copy.CantAttack = true;
        End(copies); End(copies); End(copies);
        Core2026Proof.Check(!PlayLockRules.IsSleeping(copy) && copy.CantAttack && copy.HasTaunt,
            "copied sleep keeps caster-bound expiry and preserves unrelated attack restriction");
    }
}
