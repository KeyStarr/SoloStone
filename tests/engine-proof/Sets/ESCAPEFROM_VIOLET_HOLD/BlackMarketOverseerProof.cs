using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class BlackMarketOverseerProof
{
    private static bool Overseen(Minion m) => m.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_880e") == true;

    internal static void Run() => StandardSetsProof.Case("JAIL_880", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_120");
        var held = Add(p, id);
        var before = (Minion)Play(g, p, "EX1_096");
        Core2026Proof.Check(!before.IsRush && !Overseen(before), "Overseer in hand does not trigger");
        PlayEntity(g, p, held);
        var source = (Minion)held;
        Core2026Proof.Check(!Overseen(source) && source.AttackDamage == 1 && source.Health == 3, "no self trigger");
        var fresh = g.CloneExact(history: true);

        var hoarder = (Minion)Play(g, p, "EX1_096");
        Core2026Proof.Check(hoarder.IsRush && Overseen(hoarder) && CanAttack(hoarder, enemy) && !CanAttack(hoarder, p.Opponent.Hero),
            "played Deathrattle minion gets pinned JAIL_880e Rush");
        AttackOptionsProof.Lifecycle(g, hoarder, enemy, rush: true);
        var vanilla = (Minion)Play(g, p, "CS2_120");
        Core2026Proof.Check(!vanilla.IsRush && !Overseen(vanilla), "non-Deathrattle minion ignored");
        var summoned = Summon(p, "EX1_096");
        Core2026Proof.Check(!summoned.IsRush && !Overseen(summoned), "summoned Deathrattle minion is not played");
        var copy = g.CloneExact(history: true);
        var copyHoarder = (Minion)Play(copy, copy.Player1, "EX1_096");
        Core2026Proof.Check(Overseen(copyHoarder) && p.BoardZone.Count(m => Overseen(m)) == 1, "clone trigger independent");

        // The on-play grant must precede a Battlecry that kills the Overseer (AFTER_PLAY_MINION is too late).
        var timing = fresh.CloneExact(history: true);
        var devastator = (Minion)Play(timing, timing.Player1, "EDR_459");
        Core2026Proof.Check(timing.IdEntityDic[source.Id].Zone == timing.Player1.GraveyardZone
            && devastator.IsRush && Overseen(devastator), "Rush granted before the Battlecry kills the Overseer");

        // Opponent plays do not trigger it.
        End(g);
        var enemyHoarder = (Minion)Play(g, p.Opponent, "EX1_096");
        Core2026Proof.Check(!enemyHoarder.IsRush && !Overseen(enemyHoarder), "opponent's Deathrattle play ignored");
        End(g);

        // Silence and leaving play remove the trigger.
        var silenced = fresh.CloneExact(history: true); var sp = silenced.Player1;
        Play(silenced, sp, "BE_228", (Minion)silenced.IdEntityDic[source.Id]);
        Core2026Proof.Check(!Overseen((Minion)Play(silenced, sp, "EX1_096")), "silenced Overseer does not trigger");
        var dead = fresh.CloneExact(history: true);
        Kill(dead, (Minion)dead.IdEntityDic[source.Id]);
        Core2026Proof.Check(!Overseen((Minion)Play(dead, dead.Player1, "EX1_096")), "dead Overseer does not trigger");
    });
}
