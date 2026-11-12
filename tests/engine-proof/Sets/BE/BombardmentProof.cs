using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static StandardSetsProof;

internal static class BombardmentProof
{
    // PINNED-RULE-ONLY, tier C: NOTES/rules-evidence/cata_core_be-bombardment.yaml and
    // spell-text-single-phase.yaml: mid-spell Mech check, no death phase between iterations;
    // random destroy ignores mortally wounded/pending-destroy minions.
    internal static void Run() => StandardSetsProof.Case("BE_554", (g, p, id) => {
        Core2026Proof.Check(SabberStoneCore.Model.Cards.FromId(id)[GameTag.SPELL_SCHOOL] == 2,
            "printed Fire spell");
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "empty enemy board still takes face damage");
        var friendly = Summon(p, "CS2_182");
        Summon(p.Opponent, "BE_730t");
        Summon(p.Opponent, "CS2_182");
        Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6 && p.Opponent.GraveyardZone.Count(m => m is Minion) == 1
            && friendly.Damage == 0, "opponent Mech and friendly non-Mech do not repeat; exactly one random enemy dies");
        ParkBoard(p.Opponent);
        Summon(p, "BE_730t");
        var location = LocationProofs.Location(p.Opponent);
        var golem1 = Summon(p.Opponent, "EX1_556");
        var golem2 = Summon(p.Opponent, "EX1_556");
        Resume(g, next => Play(next, next.Player1, id));
        var tokens = p.Opponent.BoardZone.ToArray();
        Core2026Proof.Check(p.Opponent.Hero.Damage == 12 && golem1.Zone.Type == Zone.GRAVEYARD && golem2.Zone.Type == Zone.GRAVEYARD,
            "dual-type friendly Mech repeats once and skips first pending-destroy target");
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.Card.Id == "skele21" && m.Card.AssetId == 471
            && m.AttackDamage == 2 && m.Health == 1 && m.Damage == 0 && m.IsRace(Race.MECHANICAL))
            && location.Zone == p.Opponent.BoardZone && tokens.Select(m => m.ZonePosition).SequenceEqual(new[] { 1, 2 }),
            "both Deathrattles wait until spell ends; exact tokens occupy dying slots after location");
        ParkBoard(p.Opponent);
        var single = Summon(p.Opponent, "EX1_556");
        Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(p.Opponent.Hero.Damage == 18 && single.Zone.Type == Zone.GRAVEYARD
            && p.Opponent.BoardZone.Single().Card.Id == "skele21", "one enemy: second iteration hits face, never later Deathrattle token");
        ParkBoard(p.Opponent);
        var sylvanas = Summon(p.Opponent, "EX1_016");
        ParkBoard(p); var mech = Summon(p, "BE_730t");
        Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(p.Opponent.Hero.Damage == 24 && sylvanas.Zone.Type == Zone.GRAVEYARD && mech.Controller == p.Opponent,
            "Mech check precedes Sylvanas Deathrattle stealing sole friendly Mech");
        ParkBoard(p); ParkBoard(p.Opponent);
        Summon(p, "EX1_012");
        p.Opponent.Hero.Armor = 100;
        var pending = Summon(p.Opponent, "CS2_182");
        var living = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id); pending.Damage = 5;
        Resolve(g, spell);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 96 && living.Zone.Type == Zone.GRAVEYARD,
            "mortally wounded enemy excluded before direct recipe's single destroy; Spell Damage applies");
        ParkBoard(p.Opponent);
        Summon(p, "BE_730t");
        LocationProofs.Location(p.Opponent);
        while (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.SlotCount == 5 && p.Opponent.BoardZone.LocationCount == 1
            && p.Opponent.Hero.Armor == 88, "full mixed board loses exactly two minions, never location; both hits scale");
        p.Hero.Armor = 100; End(g);
        Play(g, p.Opponent, id);
        Core2026Proof.Check(p.Hero.Armor == 97 && p.BoardZone.Count == 1, "opponent cast resolves against its enemy; enemy Mech grants no repeat");
        ParkBoard(p); ParkBoard(p.Opponent);
        // Advanced rulebook BE2 (tier C): Dormant entities do not count as minions.
        var dormantMech = Summon(p.Opponent, "BE_730t"); dormantMech[GameTag.DORMANT] = 1;
        Summon(p, "CS2_182"); Summon(p, "CS2_182");
        p.Hero.Armor = 100;
        Play(g, p.Opponent, id);
        Core2026Proof.Check(p.Hero.Armor == 97 && p.BoardZone.Count == 1
            && dormantMech.Zone == p.Opponent.BoardZone && dormantMech.IsDormant,
            "sole friendly Mech is Dormant: no repeat, exactly one enemy destroyed and one face hit");
    });
}
