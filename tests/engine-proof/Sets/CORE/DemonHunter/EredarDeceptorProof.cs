using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EredarDeceptorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TTN_843", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "Deceptor middle play accepted");
        Core2026Proof.Check(source.Cost == 4 && source.AttackDamage == 3 && source.Health == 5
            && source.IsRace(Race.DEMON) && !source.IsRush, "printed 4 mana 3/5 Demon");
        Add(p, id);
        Core2026Proof.Check(p.BoardZone.Count == 3, "creating a hand card does not trigger DRAW");

        var clone = g.CloneExact();
        Generic.Draw(clone.Player1);
        Core2026Proof.Check(clone.Player1.BoardZone.Count(m => m.Card.Id == "TTN_843t1") == 1
            && p.BoardZone.Count == 3 && clone.Player1.DeckZone.Count == p.DeckZone.Count - 1,
            "cloned draw listener summons only in clone");
        Generic.Draw(p);
        var token = p.BoardZone.Single(m => m.Card.Id == "TTN_843t1");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, token.Id, right.Id }),
            "normal draw summons immediately right of actual source position");
        Core2026Proof.Check(token.Card.AssetId == 101499 && token.Card.Implemented && !token.Card.Collectible
            && token.Card.ATK == 1 && token.Card.Health == 1 && token.Card[GameTag.RUSH] == 1
            && token.AttackDamage == 1 && token.Health == 1 && token.IsRace(Race.DEMON) && token.IsRush,
            "exact TTN_843t1 token: 1/1 Demon with printed Rush");
        Generic.Draw(p.Opponent);
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.Opponent.BoardZone.Count == 0, "opponent draw does not summon");

        var egg = Entity.FromCard(p, Cards.FromId("EX1_557")); p.DeckZone.Add(egg);
        Play(g, p, "DINO_411");
        var filtered = p.BoardZone.Single(m => m.Card.Id == "TTN_843t1" && m.Id != token.Id);
        Core2026Proof.Check(egg.Zone == p.HandZone && p.BoardZone.Count == 6
            && filtered.ZonePosition == source.ZonePosition + 1 && token.ZonePosition == filtered.ZonePosition + 1,
            "filtered draw summons once beside Deceptor");

        End(g);
        Core2026Proof.Check(p.BoardZone.Count == 6, "opponent turn draw does not trigger");
        End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TTN_843t1") == 3,
            "listener survives turn boundary and fills final board slot");
        Generic.Draw(p);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TTN_843t1") == 3,
            "full board prevents extra token");
        foreach (var minion in p.BoardZone.Where(m => m != source).ToArray()) Kill(g, minion);
        source.Silence();
        Generic.Draw(p);
        Core2026Proof.Check(p.BoardZone.Count == 1, "Silence removes DRAW listener; hand Deceptor stays inactive");
        Kill(g, source);
        var dead = Summon(p, id); Kill(g, dead);
        Generic.Draw(p);
        Core2026Proof.Check(p.BoardZone.Count == 0, "source leaving play removes DRAW listener");
    });
}
