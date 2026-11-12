using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class FlightManeuversProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_479", (g, p, id) => {
        ShatterSpellProof.Family(g, p, id, "CATA_479t", "CATA_479t2", "CATA_479t3", "CATA_479e");
        var location = LocationProofs.Location(p); var friendly = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); var sleeper = Summon(p, "TIME_046");
        var source = ShatterRules.GiveCombined(p, id, p.Hero);
        ShatterSpellProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        var drakes = p.BoardZone.Where(m => m.Card.Id == "CATA_479t3").ToArray();
        Core2026Proof.Check(drakes.Length == 2 && drakes.All(m => m.AttackDamage == 5 && m.Health == 2 && m.HasDivineShield
            && m.Card.Cost == 3 && m.Card.AssetId == 123140 && m.Card.IsRace(Race.DRAGON)
            && m.AppliedEnchantments.Single().Card.Id == "CATA_479e") && drakes[0].Id < drakes[1].Id
            && p.BoardZone.GetSlot(3) == drakes[0] && p.BoardZone.GetSlot(4) == drakes[1], "exact Drakes append, then gain +1 Attack/Shield");
        Core2026Proof.Check(friendly.AttackDamage == 5 && friendly.Health == 5 && friendly.HasDivineShield
            && enemy.AttackDamage == 4 && !enemy.HasDivineShield && sleeper.AttackDamage == 3
            && !sleeper.HasDivineShield && location.AppliedEnchantments?.Any() != true, "Unpinned_DormantExclusions: only active friendly minions receive permanent grant");
        drakes[0].TakeDamage(enemy, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!drakes[0].HasDivineShield && drakes[0].Damage == 0, "granted Shield absorbs one hit");
        drakes[1].Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(drakes[1].AttackDamage == 4 && !drakes[1].HasDivineShield && drakes[1].Health == 2, "Silence removes attack and Shield");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "CATA_479t");
        Core2026Proof.Check(p.BoardZone.SlotCount == 2 && p.BoardZone.All(m => m.AttackDamage == 4 && !m.HasDivineShield), "first half has no buff");
        Play(g, p, "CATA_479t2"); Play(g, p, "CATA_479t2");
        Core2026Proof.Check(p.BoardZone.All(m => m.AttackDamage == 6 && m.Health == 2 && m.HasDivineShield), "second half independently stacks attack");
        var future = Summon(p, "CS2_182");
        End(g); End(g);
        Core2026Proof.Check(future.AttackDamage == 4 && !future.HasDivineShield && p.BoardZone[0].AttackDamage == 6,
            "spell departure/end turn retains grant; future minions receive none");
        StandardSetsProof.ParkBoard(p);
        LocationProofs.Location(p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        PlayEntity(g, p, ShatterRules.GiveCombined(p, id, p.Hero));
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_479t3") == 1
            && p.BoardZone.All(m => m.AttackDamage == 5 && m.HasDivineShield), "one remaining physical slot, then buff even on full board");
        Play(g, p, "CATA_479t"); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board blocks first half");
        StandardSetsProof.ParkBoard(p); Play(g, p, "CATA_479t2");
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && !enemy.HasDivineShield, "empty friendly board never buffs opponent");
    });
}
