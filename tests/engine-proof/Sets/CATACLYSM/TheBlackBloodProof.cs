using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TheBlackBloodProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_300", (g, p, id) => {
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CATA_300t3", "CATA_300t1", id, "CATA_300t2" })
            && ColossalRules.AppendagesOf(body).All(m => m.AttackDamage == 2 && m.Health == 2), "Lean_C3 three exact 2/2 bodies in physical slots");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.AttackDamage = 0; enemy.BaseHealth = 100;
        p.Hero.Damage = 9;
        var clone = g.CloneExact(history: true); End(clone); End(g);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "heal and forced attacks clone future parity");
        Core2026Proof.Check(p.Hero.Damage == 0 && enemy.Health == 85 && body.Health == 9 && p.Opponent.Hero.Health == 30,
            "three 3-heals each force exhausted body attack at enemy minion only");
        int hp = enemy.Health; p.Hero.Damage = 3; End(g);
        Core2026Proof.Check(p.Hero.Damage == 3 && enemy.Health == hp, "opponent end does not heal");
        p.Hero.TakeHeal(p.Opponent.Hero, 1);
        Core2026Proof.Check(enemy.Health == hp, "opponent-origin healing does not trigger body");
        enemy.Damage = 20; enemy.TakeHeal(p.Hero, 1);
        Core2026Proof.Check(enemy.Health == 76, "friendly-origin heal of enemy character triggers attack");
        p.Hero.Damage = 1; hp = enemy.Health; p.Hero.TakeHeal(p.Hero, 0);
        Core2026Proof.Check(enemy.Health == hp, "zero healing on damaged character does not attack");
        p.Hero.Damage = 0; hp = enemy.Health; p.Hero.TakeHeal(p.Hero, 3);
        Core2026Proof.Check(enemy.Health == hp, "zero effective healing does not attack");
        var child = ColossalRules.AppendagesOf(body).First(); child.Silence(); p.Hero.Damage = 9; End(g);
        Core2026Proof.Check(p.Hero.Damage == 3 && enemy.Health == hp - 10, "silenced child neither heals nor causes attack");
        End(g); body.Silence(); hp = enemy.Health; p.Hero.TakeHeal(p.Hero, 1);
        Core2026Proof.Check(enemy.Health == hp, "body Silence removes heal trigger");
        Kill(g, body); p.Hero.Damage = 9; End(g);
        Core2026Proof.Check(p.Hero.Damage == 3 && enemy.Health == hp, "Lean_C4 children heal after body death; departed body never attacks");
        End(g); ParkBoard(p); ParkBoard(p.Opponent);
        body = Summon(p, id); p.Hero.Damage = 0;
        End(g); Core2026Proof.Check(p.Hero.Health == 30 && p.Opponent.Hero.Health == 30, "undamaged board and empty enemy board are safe no-ops");
        End(g); p.Hero.Damage = 1; p.Hero.TakeHeal(p.Hero, 1);
        Core2026Proof.Check(p.Opponent.Hero.Health == 30, "no enemy minions never redirects attack to hero");
        ParkBoard(p); ParkBoard(p.Opponent); p.Hero.Damage = 0;
        var lone = Summon(p, "CATA_300t2"); lone.Damage = 1; p.Opponent.Hero.Damage = 6;
        End(g);
        Core2026Proof.Check(lone.Health == 2 && p.Opponent.Hero.Damage == 6, "child heals damaged friendly minion, caps to missing Health, excludes damaged enemy hero");
        End(g); Kill(g, lone); p.Hero.Damage = 3; End(g);
        Core2026Proof.Check(p.Hero.Damage == 3, "departed child no longer heals"); End(g);
        TheBlackBloodProof.Lean_FullBoard(g, p, id);
        ForcedAttackAllowance(g, p, id);
        LethalLifesteal(g, p, id);
    });

    private static void ForcedAttackAllowance(Game g, Controller p, string id)
    {
        ParkBoard(p); ParkBoard(p.Opponent);
        var body = Summon(p, id);
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.AttackDamage = 0; enemy.BaseHealth = 100;
        foreach (bool windfury in new[] { false, true })
        {
            body.HasWindfury = windfury; body.IsExhausted = false; body.NumAttacksThisTurn = 0;
            p.Hero.Damage = 1; int health = enemy.Health;
            p.Hero.TakeHeal(p.Hero, 1);
            Core2026Proof.Check(enemy.Health == health - 5 && !body.IsExhausted && body.NumAttacksThisTurn == 0,
                "forced attack preserves ready body's normal attack allowance, including Windfury");
            var manual = p.Options().Single(t => t.Source?.Id == body.Id && t.Target?.Id == enemy.Id && t.PlayerTaskType == PlayerTaskType.MINION_ATTACK);
            Core2026Proof.Check(g.Process(manual) && body.NumAttacksThisTurn == 1, "ready body can manually attack after forced combat");
            p.Hero.Damage = 1; p.Hero.TakeHeal(p.Hero, 1);
            Core2026Proof.Check(body.NumAttacksThisTurn == 1 && body.IsExhausted == !windfury,
                "forced combat preserves already-used attack and remaining Windfury attack");
            if (windfury)
            {
                manual = p.Options().Single(t => t.Source?.Id == body.Id && t.Target?.Id == enemy.Id && t.PlayerTaskType == PlayerTaskType.MINION_ATTACK);
                Core2026Proof.Check(g.Process(manual) && body.NumAttacksThisTurn == 2 && body.IsExhausted, "second manual Windfury attack remains usable");
            }
        }
        body.IsFrozen = true; body.IsExhausted = true;
        int before = enemy.Health, attacks = body.NumAttacksThisTurn;
        p.Hero.Damage = 1; p.Hero.TakeHeal(p.Hero, 1);
        Core2026Proof.Check(enemy.Health == before - 5 && body.IsFrozen && body.IsExhausted && body.NumAttacksThisTurn == attacks,
            "forced-attack helper bypasses Frozen/exhaustion without granting a manual attack");
    }

    private static void LethalLifesteal(Game g, Controller p, string id)
    {
        ParkBoard(p); ParkBoard(p.Opponent);
        var body = Summon(p, id);
        var healer = Summon(p, "CS2_182"); healer.HasLifeSteal = true;
        var victim = Summon(p.Opponent, "CS2_182");
        var living = Summon(p.Opponent, "CS2_182"); living.AttackDamage = 0; living.BaseHealth = 100;
        p.Hero.Damage = 1;
        victim.TakeDamage(healer, 5);
        Core2026Proof.Check(p.Hero.Damage == 0 && living.Health == 95 && victim.ToBeDestroyed,
            "lethal Lifesteal heal skips victim pending destruction and attacks living enemy");
        ParkBoard(p.Opponent);
        victim = Summon(p.Opponent, "CS2_182");
        var rng = g.Random.CaptureState(); int attacks = body.NumAttacksThisTurn;
        p.Hero.Damage = 1; victim.TakeDamage(healer, 5);
        Core2026Proof.Check(body.NumAttacksThisTurn == attacks && g.Random.CaptureState().Equals(rng),
            "only dying enemy is a no-op without RNG or attack allowance changes");
        g.DeathProcessingAndAuraUpdate();
    }

    internal static void Lean_Appendages(Minion body, string[] ids, int attack, int health, int number = 0)
    {
        var p = body.Controller;
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { ids[0], body.Card.Id, ids[1] }), "Lean_C3 exact appendage layout");
        foreach (var m in ColossalRules.AppendagesOf(body))
            Core2026Proof.Check(m.Card.AssetId > 0 && m.AttackDamage == attack && m.Health == health
                && m.ZonePosition == (m.Card.Id == ids[0] ? 0 : 2) && m[GameTag.DISPLAYED_CREATOR] == body.Id
                && m[GameTag.TAG_SCRIPT_DATA_NUM_1] == number, "exact appendage stats, magnitude, physical slot and link");
    }

    internal static void Lean_FullBoard(Game g, Controller p, string id)
    {
        foreach (var h in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(h));
        ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.IsFull && !ColossalRules.AppendagesOf(body).Any(), "Lean_C3 full physical board drops appendages");
        ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.SlotCount < 5) Summon(p, "CS2_182");
        body = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.IsFull && ColossalRules.AppendagesOf(body).Count() == 1
            && ColossalRules.AppendagesOf(body).Single().Card.Id == body.Card.ColossalAppendageIds[0], "Lean_C3 partial board retains first appendage only");
    }
}
