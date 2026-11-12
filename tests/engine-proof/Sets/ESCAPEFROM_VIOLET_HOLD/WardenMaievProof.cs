using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WardenMaievProof
{
    // Unpinned_MaievAfterPlay Unpinned_DormantReplace Unpinned_DormantClock
    // Unpinned_DormantExclusions Unpinned_DormantLifecycle Unpinned_DormantCounterProjection Unpinned_DormantFrozenThaw.
    internal static void Run() => StandardSetsProof.Case("JAIL_850", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("JAIL_850e", 127601);
        EmeraldDreamCardStateProof.Token("JAIL_850e2", 126910);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(!source.IsDormant && source.AttackDamage == 1 && source.Health == 3, "Maiev never triggers on herself");
        var summoned = Summon(p, "CS2_182");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(!summoned.IsDormant && summoned.AttackDamage == 4, "summons and spells excluded");
        var enemy = Summon(p.Opponent, "CS2_182");
        var played = (Minion)Play(g, p, "CS2_141", enemy);
        Core2026Proof.Check(enemy.Damage == 1 && played.AttackDamage == 5 && played.BaseHealth == 5 && played.IsDormant,
            "Unpinned_MaievAfterPlay Rifleman Battlecry resolves before sleep, then +3/+3");
        Core2026Proof.Check(played.AppliedEnchantments.Select(e => e.Card.Id).ToHashSet().SetEquals(new[] { "JAIL_850e2", "JAIL_850e" })
            && DormantRules.TurnsRemaining(played) == 1, "exact buff plus one-start condition");
        var timeline = g.PowerHistory.Full.ToArray();
        int battlecryDamage = Array.FindIndex(timeline, e => e is PowerHistoryTagChange change && change.EntityId == enemy.Id && change.Tag == GameTag.DAMAGE && change.Value == 1);
        int buff = Array.FindIndex(timeline, e => e is PowerHistoryTagChange change && change.EntityId == played.Id && change.Tag == GameTag.ATK && change.Value == 5);
        int sleep = Array.FindIndex(timeline, e => e is PowerHistoryTagChange change && change.EntityId == played.Id && change.Tag == GameTag.DORMANT && change.Value == 1);
        Core2026Proof.Check(battlecryDamage >= 0 && buff > battlecryDamage && sleep > buff, "Unpinned_MaievAfterPlay history orders Battlecry, +3/+3, then sleep");
        var natural = (Minion)Play(g, p, "TIME_046");
        Core2026Proof.Check(natural.IsDormant && natural.AttackDamage == 6 && natural.BaseHealth == 15 && DormantRules.TurnsRemaining(natural) == 1
            && natural.AppliedEnchantments.All(e => e.Card.Id != "TIME_046e"), "Unpinned_DormantReplace natural sleep replaced and buff retained");
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; var m = (Minion)next.IdEntityDic[played.Id];
            End(next);
            Core2026Proof.Check(m.IsDormant && DormantRules.TurnsRemaining(m) == 1, "opponent start does not tick");
            var opposing = (Minion)Play(next, owner.Opponent, "CS2_182");
            Core2026Proof.Check(!opposing.IsDormant && opposing.AttackDamage == 4, "opponent play excluded");
            End(next);
            Core2026Proof.Check(!m.IsDormant && m.IsExhausted && m.AttackDamage == 5 && m.BaseHealth == 5
                && m.AppliedEnchantments.Single().Card.Id == "JAIL_850e2", "future own start wakes; only condition retires");
            Core2026Proof.Check(!((Minion)next.IdEntityDic[natural.Id]).IsDormant, "replaced natural timer wakes after one start");
        });
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        source = Summon(p, id); source.Silence(); g.DeathProcessingAndAuraUpdate();
        played = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(!played.IsDormant && played.AttackDamage == 4, "Silence removes Maiev trigger");
        Kill(g, source); source = Summon(p, id); Generic.ReturnToHandBlock(p, source);
        played = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(!played.IsDormant && played.AttackDamage == 4, "source leaves: no Maiev trigger");
        ParkBoard(p); source = Summon(p, id); Kill(g, source);
        played = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(!played.IsDormant && played.AttackDamage == 4, "dead Maiev cannot trigger");
        ParkBoard(p); source = Summon(p, id); DormantRules.Make(source, 1, "JAIL_850e", p.Hero);
        played = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(!played.IsDormant && played.AttackDamage == 4, "sleeping Maiev ordinary trigger suppressed");
        ParkBoard(p); source = Summon(p, id); LocationProofs.Location(p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        played = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && played.IsDormant && played.AttackDamage == 7, "full physical board still buffs last played minion");
    });
}
