using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EmergencySurgeryProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_454", (g, p, id) => {
        var friend = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        var taunt = Summon(p.Opponent, "CS1_042");
        TargetRules(g, p, id, enemy, friend, true);
        p.Hero.Damage = 20;
        Play(g, p, id, enemy);
        var nurses = p.BoardZone.Where(m => m.Card.Id == "JAIL_454t").ToArray();
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && taunt.Damage == 0 && friend.Damage == 0
            && p.Hero.Damage == 14 && nurses.Length == 2,
            "two nurses kill chosen target and heal six; remaining nurses stop, ignoring other taunt");
        Core2026Proof.Check(nurses.All(m => m.AttackDamage == 3 && m.Health == 1 && m.Cost == 2
            && m.IsRace(Race.UNDEAD) && m.HasLifeSteal && m.IsExhausted && !m.HasCharge && !m.IsRush)
            && nurses[0].ZonePosition == friend.ZonePosition + 1 && nurses[1].ZonePosition == friend.ZonePosition + 2,
            "exact Necronurses append in creation order and gain no attack keyword");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        enemy = Summon(p.Opponent, "CS2_168"); p.Hero.Damage = 20;
        Play(g, p, id, enemy);
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.Card.Id == "JAIL_454t")
            && p.Hero.Damage == 17 && p.Opponent.Hero.Damage == 0,
            "one-health target dies to first attack; three nurses remain without attacking graveyard or hero");
        ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        enemy = Summon(p.Opponent, "CS2_182"); p.Hero.Damage = 20;
        var source = Add(p, id);
        var clone = g.CloneExact(history: true);
        PlayEntity(clone, clone.Player1, clone.IdEntityDic[source.Id], (ICharacter)clone.IdEntityDic[enemy.Id]);
        Core2026Proof.Check(enemy.Damage == 0 && p.Hero.Damage == 20 && p.BoardZone.Count == 6,
            "forced summon/attack sequence in clone does not mutate original");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.Hero.Damage == 17 && p.BoardZone.Count == 6
            && Canonical.State(g) == Canonical.State(clone),
            "one free slot summons only one nurse before attacks; death does not refill slot");
        Summon(p, "CS2_182"); source = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id),
            "full board has no legal Surgery play");
        source.Card.Power.PowerTask.Process(g, p, source, enemy, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && enemy.Damage == 3 && p.Hero.Damage == 17,
            "full board effect summons nothing and never attacks with existing minions");
        ClearBoard(g, p.Opponent); ClearBoard(g, p);
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id),
            "no enemy minion has no legal Surgery target");
    });
}
