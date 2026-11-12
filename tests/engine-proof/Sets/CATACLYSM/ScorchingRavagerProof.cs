using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScorchingRavagerProof
{
    // Tier D: pinned 37.0.0.254477 CATA_160/CATA_580t; existing Warrior Herald contract, no new profile.
    // Rush/Silence uses docs/roadmap/2026-10-19-silence-readiness-fix.md (tier C keyword rules).
    private static bool CanAttack(Controller p, Minion m, ICharacter target) =>
        p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target?.Id == target.Id);

    internal static void Run() => StandardSetsProof.Case("CATA_160", (g, p, id) => {
        p[HeraldRules.Profile] = (int)CardClass.WARRIOR;
        var enemy = Summon(p.Opponent, "CS2_182");
        var location = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.BoardZone);
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)PlayAt(g, p, id, 2);
        var soldier = p.BoardZone.Single(m => HeraldRules.IsSoldier(m.Card));
        Core2026Proof.Check(soldier.Card.Id == "CATA_580t" && soldier.Card.AssetId == 127082 && soldier.Card.Cost == 1
            && soldier.IsRace(Race.ELEMENTAL) && soldier.HasDeathrattle && soldier.AttackDamage == 2 && soldier.Health == 1
            && soldier[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && HeraldRules.Count(p) == 1,
            "Warrior Herald summons exact Soldier of Ragnaros once");
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == location && soldier.ZonePosition == 3 && p.BoardZone.GetSlot(4) == right,
            "Soldier right of the Battlecry source in physical slots including a location");
        Core2026Proof.Check(soldier.IsRush && soldier[GameTag.RUSH] == 1 && soldier.AttackableByRush && !soldier.IsExhausted,
            "Soldier gains Rush and minion-only readiness this turn");
        Core2026Proof.Check(CanAttack(p, soldier, enemy) && !CanAttack(p, soldier, p.Opponent.Hero), "Rush: enemy minion only");
        Core2026Proof.Check(!source.IsRush && source.IsExhausted && !CanAttack(p, source, enemy) && !left.IsRush && !right.IsRush,
            "only the Soldier is given Rush");

        var clone = g.CloneExact(history: true);
        var copy = (Minion)clone.IdEntityDic[soldier.Id];
        copy.Silence(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!copy.IsRush && copy.IsExhausted && !CanAttack(clone.Player1, copy, (ICharacter)clone.IdEntityDic[enemy.Id]),
            "Silence removes granted Rush and restores summoning sickness");
        Core2026Proof.Check(soldier.IsRush && CanAttack(p, soldier, enemy), "clone Silence isolated from original");

        var persistent = g.CloneExact(history: true);
        var persistentSoldier = (Minion)persistent.IdEntityDic[soldier.Id];
        var persistentSource = (Minion)persistent.IdEntityDic[source.Id];
        persistentSource.Silence(); Kill(persistent, persistentSource);
        Core2026Proof.Check(persistentSoldier.IsRush && CanAttack(persistent.Player1, persistentSoldier,
            (ICharacter)persistent.IdEntityDic[enemy.Id]), "grant survives source Silence and removal");
        End(persistent);
        Core2026Proof.Check(persistentSoldier.IsRush && !persistentSoldier.AttackableByRush,
            "Rush grant persists; minion-only entry restriction ends with the turn");

        Attack(g, soldier, enemy);
        Core2026Proof.Check(soldier.Zone.Type == Zone.GRAVEYARD, "Rush attack resolves");
        End(g);
        Core2026Proof.Check(!p.BoardZone.Any(m => m.AttackableByRush), "Rush readiness expires at end of turn");
        End(g);

        var second = (Minion)Play(g, p, id);
        var tiered = p.BoardZone.Single(m => HeraldRules.IsSoldier(m.Card));
        Core2026Proof.Check(HeraldRules.Count(p) == 2 && tiered.ZonePosition == second.ZonePosition + 1 && tiered.IsRush && tiered.AttackableByRush,
            "repeat Herald Soldier also gains Rush");
        Core2026Proof.Check(p.Opponent.BoardZone.All(m => !m.IsRush), "opponent minions untouched");

        var upgraded = g.CloneExact(history: true);
        Kill(upgraded, (Minion)upgraded.IdEntityDic[source.Id]);
        var third = Play(upgraded, upgraded.Player1, id);
        var stronger = upgraded.Player1.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == third.Id);
        Core2026Proof.Check(stronger.Card.Id == "CATA_580t" && stronger.AttackDamage == 4 && stronger.Health == 2
            && stronger[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && stronger.IsRush && stronger.AttackableByRush,
            "upgraded Soldier retains exact doubled stats and receives Rush");

        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        int count = HeraldRules.Count(p), soldiers = p.BoardZone.Count(m => HeraldRules.IsSoldier(m.Card));
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && HeraldRules.Count(p) == count + 1
            && p.BoardZone.Count(m => HeraldRules.IsSoldier(m.Card)) == soldiers && p.BoardZone.All(m => !m.IsRush || HeraldRules.IsSoldier(m.Card)),
            "full board: Herald advances, no Soldier, no Rush granted elsewhere");
        var nonplay = g.CloneExact(history: true);
        Kill(nonplay, (Minion)nonplay.IdEntityDic[source.Id]);
        int progress = HeraldRules.Count(nonplay.Player1), slots = nonplay.Player1.BoardZone.SlotCount;
        Summon(nonplay.Player1, id);
        Core2026Proof.Check(HeraldRules.Count(nonplay.Player1) == progress && nonplay.Player1.BoardZone.SlotCount == slots + 1,
            "summoning Ravager does not execute Battlecry");
    });
}
