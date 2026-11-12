using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EternalToilProof
{
    // PINNED-RULE-ONLY: NOTES/rules-evidence/spell-effects-before-death-phase.yaml
    // Tier B Frost Strike trace (175913), tier C rulebook/Mortal Coil: follow-up before deaths.
    internal static void Run() => StandardSetsProof.Case("END_020", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, false); PoolPilotProof.EmptyHand(p);
        int deck = p.DeckZone.Count;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 4 && p.DeckZone.Count == deck - 1 && p.HandZone.Single().Card.Id == "CS2_182" && p.BoardZone.Count == 1,
            "survivor takes one and draws exactly one, without summon");
        enemy.HasDivineShield = true; deck = p.DeckZone.Count;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 4 && !enemy.HasDivineShield && p.DeckZone.Count == deck - 1, "prevented damage still survives and draws");
        Summon(p, "TIME_856"); var victim = Summon(p.Opponent, "CS2_182"); victim.Damage = 2;
        int before = g.NextId; deck = p.DeckZone.Count;
        var spell = Play(g, p, id, victim);
        var output = p.BoardZone.Where(m => m.Id >= before).ToArray();
        Core2026Proof.Check(victim.Zone == p.Opponent.GraveyardZone && p.DeckZone.Count == deck, "spell damage makes three-health target die; no draw");
        PoolPilotProof.Generated(g, spell, output, 1);
        Core2026Proof.Check(output[0].Card.Type == CardType.MINION && output[0].Card.Cost == 1 && output[0].ZonePosition == 2,
            "random printed 1-Cost minion appends after existing minions");
        TimeTravelPoolProof.ClearQuietly(g, p); TimeTravelPoolProof.ClearQuietly(g, p.Opponent);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        var egg = Summon(p, "TLC_237"); egg.Damage = 1; var right = Summon(p, "CS2_182");
        spell = Play(g, p, id, egg);
        output = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == spell.Id).ToArray();
        var hatchlings = p.BoardZone.Where(m => m.Card.Id == "TLC_237t").ToArray();
        PoolPilotProof.Generated(g, spell, output, 1);
        Core2026Proof.Check(egg.Zone == p.GraveyardZone && hatchlings.Length == 3 && output[0].Id < hatchlings.Min(m => m.Id) &&
            p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id }.Concat(hatchlings.Select(m => m.Id)).Append(right.Id).Append(output[0].Id)),
            $"summon before death phase: hatchlings={hatchlings.Length}, output={output[0].Card.Id}/{output[0].Id}, slots={string.Join(",",p.BoardZone.Slots.Select(e=>e.Card.Id+"/"+e.Id))}");
        TimeTravelPoolProof.ClearQuietly(g, p);
        LocationProofs.Location(p); var dying = Summon(p, "CS2_172"); dying.Damage = 1;
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        spell = Play(g, p, id, dying);
        Core2026Proof.Check(dying.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 6 &&
            p.BoardZone.All(m => m[GameTag.DISPLAYED_CREATOR] != spell.Id), "full board keeps dying slot occupied during summon attempt");
        TimeTravelPoolProof.ClearQuietly(g, p);
        var pending = Add(p, id); victim = Summon(p.Opponent, "CS2_172"); victim.Damage = 1;
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[pending.Id], (Minion)next.IdEntityDic[victim.Id]));
        PoolPilotProof.Generated(g, pending, p.BoardZone, 1);
        TimeTravelPoolProof.ClearQuietly(g, p);
        foreach (int count in new[] { 0, 1 }) TimeTravelPoolProof.WithPool(g, id, count, () => {
            var target = Summon(p.Opponent, "CS2_172"); target.Damage = 1;
            var source = Play(g, p, id, target);
            Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && p.BoardZone.Count == count, "empty/singleton pool preserves lethal effect");
            if (count == 1) PoolPilotProof.Generated(g, source, p.BoardZone, 1);
            TimeTravelPoolProof.ClearQuietly(g, p);
        });
        PoolPilotProof.EmptyHand(p); var held = Add(p, id); PoolPilotProof.FillHand(p);
        enemy = Summon(p.Opponent, "CS2_182"); deck = p.DeckZone.Count;
        PlayEntity(g, p, held, enemy);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "survivor draw uses slot freed by spell");
    });
}
