using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class BroodqueenShakarProof
{
    private static int Left(Game g, IPlayable card) { g.AuraUpdate(); return card[GameTag.TAG_SCRIPT_DATA_NUM_1]; }
    private static bool Active(Controller p) => p.AppliedEnchantments?.Any(e => e.Card.Id == "BE_565e") ?? false;
    private static bool CanAttack(Controller p, Minion m, ICharacter target) =>
        p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target?.Id == target.Id);
    private static void KillFriendly(Game g, Controller p, int count)
    {
        for (int i = 0; i < count; i++) Kill(g, Summon(p, "CS2_182"));
    }

    internal static void Run() => StandardSetsProof.Case("BE_565", (g, p, id) => {
        var queen = (Minion)Add(p, id);
        Core2026Proof.Check(queen.Card.ConsumesHistory && Left(g, queen) == 5, "history display: 5 left");
        // Enemy deaths and location deaths do not count; four friendly minions leave 1.
        KillFriendly(g, p.Opponent, 3);
        var location = LocationProofs.Location(p); location.Destroy(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(location.Zone == p.GraveyardZone && Left(g, queen) == 5, "enemy and location deaths do not count");
        KillFriendly(g, p, 4);
        Core2026Proof.Check(Left(g, queen) == 1, "4 friendly deaths: 1 left");
        PlayEntity(g, p, queen);
        Core2026Proof.Check(queen.AttackDamage == 3 && queen.Health == 5 && !Active(p), "4 deaths: no effect");
        var plainRush = Summon(p, "BE_873t");
        Core2026Proof.Check(!plainRush.HasCharge && !CanAttack(p, plainRush, p.Opponent.Hero), "no Charge without the effect");

        // Previous-turn deaths do not count: kill five, pass both turns.
        KillFriendly(g, p, 1);
        var second = Add(p, id);
        Core2026Proof.Check(Left(g, second) == 0, "5 friendly deaths this turn: ready");
        End(g); KillFriendly(g, p, 5); End(g);
        Core2026Proof.Check(Left(g, second) == 5, "deaths on earlier turns (both sides' turns) reset the count");
        PlayEntity(g, p, second);
        Core2026Proof.Check(!Active(p), "earlier-turn deaths: no effect");

        // Five friendly deaths this turn: Rush minions have Charge for the rest of the game.
        Kill(g, (Minion)second); KillFriendly(g, p, 4);
        var third = Add(p, id);
        Core2026Proof.Check(Left(g, third) == 0, "ready");
        Resume(g, other => PlayEntity(other, other.Player1, other.IdEntityDic[third.Id]));
        Core2026Proof.Check(Active(p) && !Active(p.Opponent), "controller enchantment applied to the player only");
        var fresh = Summon(p, "BE_873t"); var vanilla = Summon(p, "CS2_182"); var enemyRush = Summon(p.Opponent, "BE_873t");
        Core2026Proof.Check(fresh.HasCharge && CanAttack(p, fresh, p.Opponent.Hero), "new Rush minion has Charge: can attack heroes");
        Core2026Proof.Check(plainRush.HasCharge && !vanilla.HasCharge && !CanAttack(p, vanilla, p.Opponent.Hero) && !enemyRush.HasCharge,
            "existing Rush minion gains Charge; non-Rush and enemy Rush minions do not");
        // Granted Rush counts too (Overdrive's enchantment), and leaves with it.
        var mech = Summon(p, "GVG_044");
        PlayEntity(g, p, Add(p, "BE_553t"), mech);
        Core2026Proof.Check(mech.HasCharge && CanAttack(p, mech, p.Opponent.Hero), "enchantment Rush also gets Charge");
        // Silence: Rush and the aura Charge leave together; the fresh minion is exhausted again.
        fresh.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!fresh.IsRush && !fresh.HasCharge && !p.Options().Any(t => t.Source?.Id == fresh.Id), "silenced fresh minion: no Rush, no Charge, no attack");
        // The effect survives the queen leaving play and persists into later turns.
        var queenOnBoard = (Minion)third; queenOnBoard.Silence(); Kill(g, queenOnBoard);
        End(g); End(g);
        var later = Summon(p, "BE_873t");
        Core2026Proof.Check(Active(p) && later.HasCharge && CanAttack(p, later, p.Opponent.Hero), "this game: survives source death and turn changes");
        var cloned = g.CloneExact(history: true); var clonedRush = Summon(cloned.Player1, "BE_873t");
        Core2026Proof.Check(clonedRush.HasCharge && Active(cloned.Player1), "clone keeps the aura");
        // Summoned queen: no Battlecry.
        KillFriendly(g, p, 5); var summonedQueen = Summon(p, id);
        Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "BE_565e") == 1, "summon does not trigger the Battlecry");
    });
}
