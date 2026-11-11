using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DethroneProof
{
    // PINNED-RULE-ONLY: NOTES/rules-evidence/spell-effects-before-death-phase.yaml
    // Tier B Frost Strike trace (175913), tier C rulebook: pending destroy occupies slot.
    // data/rules-evidence/locations/loc-summon-position.yaml (tier C Summon): spells summon far right.
    internal static void Run() => StandardSetsProof.Case("TIME_712", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, false); PoolPilotProof.EmptyHand(p);
        long draws = g.Random.DrawCount;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.BoardZone.Single() == friendly && g.Random.DrawCount == draws,
            "without Combo destroys but never samples or summons");
        TimeTravelPoolProof.ClearQuietly(g, p);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        var egg = Summon(p, "TLC_237"); var right = Summon(p, "CS2_182");
        var spell = Play(g, p, id, egg);
        var output = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == spell.Id).ToArray();
        var hatchlings = p.BoardZone.Where(m => m.Card.Id == "TLC_237t").ToArray();
        TimeTravelPoolProof.RandomGenerated(g, spell, output, 1);
        Core2026Proof.Check(output[0].Card.Type == CardType.MINION && output[0].Card.Cost == 8 && egg.Zone == p.GraveyardZone &&
            hatchlings.Length == 3 && output[0].Id < hatchlings.Min(m => m.Id) &&
            p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id }.Concat(hatchlings.Select(m => m.Id)).Append(right.Id).Append(output[0].Id)),
            "Combo summons far right before Deathrattle; three Hatchlings fit in dying slot");
        TimeTravelPoolProof.ClearQuietly(g, p);
        LocationProofs.Location(p); var dying = Summon(p, "CS2_182");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        spell = Play(g, p, id, dying);
        Core2026Proof.Check(dying.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 6 &&
            p.BoardZone.All(m => m[GameTag.DISPLAYED_CREATOR] != spell.Id), "full board cannot use pending-destroy minion slot");
        TimeTravelPoolProof.ClearQuietly(g, p);
        var pending = Add(p, id); enemy = Summon(p.Opponent, "CS2_182");
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[pending.Id], (Minion)next.IdEntityDic[enemy.Id]));
        TimeTravelPoolProof.RandomGenerated(g, pending, p.BoardZone, 1);
        TimeTravelPoolProof.ClearQuietly(g, p);
        foreach (int count in new[] { 0, 1 }) TimeTravelPoolProof.WithPool(g, id, count, () => {
            var target = Summon(p.Opponent, "CS2_182");
            var source = Play(g, p, id, target);
            Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && p.BoardZone.Count == count, "empty/singleton pool never prevents destroy");
            if (count == 1) TimeTravelPoolProof.RandomGenerated(g, source, p.BoardZone, 1);
            TimeTravelPoolProof.ClearQuietly(g, p);
        });
    });
}
