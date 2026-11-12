using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BronzeKeeperProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_476", (g, p, id) => {
        var held = Add(p, id);
        End(g); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 0, "hand copy cannot trigger on either turn end");
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, held, zonePosition: 1)), "middle Keeper play accepted");
        var source = (Minion)held;
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 7 && source.IsRace(Race.DRAGON), "Keeper printed stats and Dragon type");
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "CATA_476t"), "no battlecry summon");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.BoardZone.Count(m => m.Card.Id == "CATA_476t") == 1
            && !p.BoardZone.Any(m => m.Card.Id == "CATA_476t"), "clone end-turn trigger isolated");
        End(g);
        var token = p.BoardZone.Single(m => m.Card.Id == "CATA_476t");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, token.Id, right.Id }),
            "Dragon summoned right of actual source position");
        Core2026Proof.Check(token.Card.AssetId == 123116 && token.Card.Implemented && token.AttackDamage == 6
            && token.Health == 6 && token.HasDivineShield && token.IsRace(Race.ELEMENTAL) && token.IsRace(Race.DRAGON)
            && !token.IsRace(Race.BEAST), "exact 6/6 Elemental Dragon with Divine Shield");
        Play(g, p.Opponent, "CS2_008", token);
        Core2026Proof.Check(!token.HasDivineShield && token.Health == 6, "token shield absorbs next damage then drops");
        Play(g, p.Opponent, "CS2_008", token);
        Core2026Proof.Check(token.Health == 5, "next damage reaches token Health");
        token.Silence();
        Core2026Proof.Check(token.Health == 5 && token.IsRace(Race.ELEMENTAL) && token.IsRace(Race.DRAGON),
            "Silence retains token damage and both printed types");
        End(g);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CATA_476t") == 1, "opponent turn end does not trigger");
        End(g);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "CATA_476t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(tokens.Length == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(
            new[] { left.Id, source.Id, tokens[1].Id, tokens[0].Id, right.Id }), "own end-turn trigger repeats next turn");
        End(g); Play(g, p, "BE_228", source); End(g);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CATA_476t") == 2, "Silence removes Keeper trigger");
        End(g); ClearBoard(g, p);
        source = Summon(p, id); Kill(g, source); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 0, "dead Keeper no longer triggers");
        End(g);
        left = Summon(p, "CS2_182"); right = Summon(p, "CS2_182");
        source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CS2_182", "CS2_182", "CATA_476t" }),
            "already queued task appends when source is gone");
        ClearBoard(g, p);
        source = Summon(p, id); Generic.ReturnToHandBlock(p, source); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 0, "bounced Keeper no longer triggers");
        End(g);
        left = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1;
        source = Summon(p, id); right = Summon(p, "CS2_182"); End(g);
        token = p.BoardZone.Single(m => m.Card.Id == "CATA_476t");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, token.Id, right.Id }),
            "Dormant neighbor counts for source position");
        left[GameTag.DORMANT] = 0;
        End(g); ClearBoard(g, p);
        source = Summon(p, id);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && !p.BoardZone.Any(m => m.Card.Id == "CATA_476t"), "full board blocks summon");
        End(g); Kill(g, p.BoardZone.Last()); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_476t") == 1,
            "one free slot summons one token, no queued full-board summon");
    });
}
