using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class BloodfiendProof
{
    // PINNED-RULE-ONLY, tier C: NOTES/rules-evidence/cata_core_be-bloodfiend.yaml and
    // spell-text-single-phase.yaml; automatic spend: corpse-spend-automatic.yaml (tiers C/E).
    // Separate one-damage waves include mortally wounded minions; one death phase after Battlecry.
    internal static void Run() => StandardSetsProof.Case("BE_283", (seeded, _, id) => {
        foreach (int corpses in new[] { 0, 1, 2, 4, 6 })
        {
            var g = DeathKnightC1Proof.NewGame(seeded.Seed); var p = g.Player1;
            var location = LocationProofs.Location(p);
            var friendly = Summon(p, "CS2_182");
            var acolyte = Summon(p, "EX1_007"); acolyte.Damage = acolyte.Health - 1;
            int acolyteDamage = acolyte.Damage;
            var thalnos = Summon(p, "EX1_012"); thalnos.IsImmune = true;
            var enemy = Summon(p.Opponent, "CS2_182");
            var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
            var golem = Summon(p.Opponent, "EX1_556"); golem.Damage = 2;
            CorpseRules.Gain(p, corpses);
            int waves = Math.Min(corpses, 4), deck = p.DeckZone.Count;
            Resume(g, next => Play(next, next.Player1, id));
            var source = p.BoardZone.Single(m => m.Card.Id == id);
            Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 4 && source.Card[GameTag.BATTLECRY] == 1
                && source.Card[(GameTag)2196] == 3, "printed triple-Blood Battlecry body excludes itself");
            Core2026Proof.Check(friendly.Damage == waves && enemy.Damage == waves && thalnos.Damage == 0,
                "one damage per corpse on both sides, unaffected by Spell Damage");
            Core2026Proof.Check(shield.Damage == Math.Max(0, waves - 1) && shield.HasDivineShield == (waves == 0),
                "separate waves break Divine Shield before later damage");
            Core2026Proof.Check(p.DeckZone.Count == deck - waves && acolyte.Damage == acolyteDamage + waves,
                "even mortally wounded Acolyte is hit and draws once per wave");
            Core2026Proof.Check(p[GameTag.CORPSES_SPENT_THIS_GAME] == waves
                && CorpseRules.Available(p) == corpses - waves + (waves > 0 ? 1 : 0),
                "spend available corpses automatically, cap at four, deaths earn only afterward");
            Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0 && location.Zone == p.BoardZone
                && location[GameTag.DAMAGE] == 0, "heroes and location unaffected");
            if (waves == 0)
                Core2026Proof.Check(golem.Zone == p.Opponent.BoardZone && golem.Damage == 2, "zero corpses do nothing");
            else
            {
                var token = p.Opponent.BoardZone.Single(m => m.Card.Id == "skele21");
                Core2026Proof.Check(golem.Zone.Type == Zone.GRAVEYARD && token.Card.AssetId == 471
                    && token.AttackDamage == 2 && token.Health == 1 && token.Damage == 0
                    && token.IsRace(Race.MECHANICAL) && token.ZonePosition == 2,
                    "Golem dies after all waves; exact undamaged Deathrattle token in dying slot");
            }
        }
        var full = DeathKnightC1Proof.NewGame(seeded.Seed);
        DeathKnightC1Proof.FullBoard(full.Player1); CorpseRules.Gain(full.Player1, 6);
        Core2026Proof.Check(!DeathKnightC1Proof.HasPlay(full.Player1, Add(full.Player1, id))
            && CorpseRules.Available(full.Player1) == 6, "seven physical slots reject play before corpse spend");
    });
}
