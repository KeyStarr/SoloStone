using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class PMMInfinitizerProof
{
    private static bool CanHit(Controller owner, Minion attacker, ICharacter target) =>
        owner.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id && t.Target?.Id == target.Id);

    private static bool Has(Minion minion, string enchantment) => minion.AppliedEnchantments?.Any(e => e.Card.Id == enchantment) ?? false;

    internal static void Run() => StandardSetsProof.Case("TIME_043", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        // No friendly minion: untargeted play, enemy untouched, no self target.
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.AttackDamage == 4 && first.Health == 4 && enemy.AttackDamage == 4 && enemy.Health == 5 &&
            !Has(first, "TIME_043e") && !Has(enemy, "TIME_043e"), "enemy-only board permits untargeted play; no self target");
        var probe = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == probe.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target?.Id == first.Id), "available friendly minion is required; enemies and heroes excluded");
        EmptyHand(p); ClearBoard(g, p);

        // Older damaged small minion and older big minion: both become exactly 8/8 with full Health.
        var small = Summon(p, "CS2_182"); var big = Summon(p, "CS2_201");
        End(g); End(g);
        small.TakeDamage(p.Opponent.Hero, 3);
        var source = (Minion)Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (ICharacter)next.IdEntityDic[small.Id]));
        Core2026Proof.Check(small.AttackDamage == 8 && small.Health == 8 && small.BaseHealth == 8 && small[GameTag.ATK] == 8 &&
            small[GameTag.HEALTH] == 8 && Has(small, "TIME_043e") && Has(small, "TIME_043e2") && small.CantAttackHeroes,
            "damaged 4/5 set to exactly 8/8 with both pinned enchantments");
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 4 && big.AttackDamage == 9, "only the target changes");
        Core2026Proof.Check(CanHit(p, small, enemy) && !CanHit(p, small, p.Opponent.Hero) && CanHit(p, big, p.Opponent.Hero),
            "target attacks minions but not heroes this turn; other minions unaffected");
        PlayEntity(g, p, Add(p, id), big);
        Core2026Proof.Check(big.AttackDamage == 8 && big.Health == 8 && !CanHit(p, big, p.Opponent.Hero), "9/5 lowered/raised to 8/8");

        // End-of-turn expiry: the hero restriction ends, the 8/8 stays; opponent's turn never re-applies it.
        Resume(g, End);
        Core2026Proof.Check(!small.CantAttackHeroes && !Has(small, "TIME_043e2") && small[GameTag.CANNOT_ATTACK_HEROES] == 0 &&
            Has(small, "TIME_043e") && small.AttackDamage == 8 && small.Health == 8, "Pacifitized expires at end of turn; stats persist");
        End(g);
        Core2026Proof.Check(CanHit(p, small, p.Opponent.Hero) && CanHit(p, big, p.Opponent.Hero), "next own turn may attack heroes");

        // Silence removes both enchantments in the same turn.
        var silenced = Summon(p, "CS2_182");
        End(g); End(g);
        PlayEntity(g, p, Add(p, id), silenced);
        Core2026Proof.Check(silenced.CantAttackHeroes, "fixture restricted");
        silenced.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(silenced.AttackDamage == 4 && silenced.Health == 5 && !silenced.CantAttackHeroes &&
            (silenced.AppliedEnchantments?.Count ?? 0) == 0 && CanHit(p, silenced, p.Opponent.Hero), "Silence restores 4/5 and hero attacks");

        // A fresh Charge minion may attack minions only this turn.
        ClearBoard(g, p);
        var wolf = (Minion)Play(g, p, "CS2_124");
        PlayEntity(g, p, Add(p, id), wolf);
        Core2026Proof.Check(wolf.AttackDamage == 8 && wolf.Health == 8 && CanHit(p, wolf, enemy) && !CanHit(p, wolf, p.Opponent.Hero),
            "Charge target is limited to minions this turn");

        // Summoning skips the Battlecry.
        var summoned = Summon(p, id);
        Core2026Proof.Check(wolf.AttackDamage == 8 && summoned.AttackDamage == 4 && wolf.AppliedEnchantments?.Count == 2, "summon skips Battlecry");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); EmptyHand(p);
    });
}
