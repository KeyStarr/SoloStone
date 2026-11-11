using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ArchwitchWillowProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SCH_181", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var handDemon = (Minion)Add(p, "CS2_065"); handDemon.AttackDamage = 9;
        var deckDemon = (Minion)Entity.FromCard(p, Cards.FromId("EX1_310"), zone: p.DeckZone);
        var ordinary = Add(p, "CS2_182"); var source = (Minion)Add(p, id);
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(clone.Process(PlayCardTask.Any(clone.Player1, (IPlayable)clone.IdEntityDic[source.Id], zonePosition: 1)), "clone Battlecry accepted");
        Core2026Proof.Check(handDemon.Zone == p.HandZone && deckDemon.Zone == p.DeckZone && p.BoardZone.Count == 2, "recruitment isolated in clone");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 5
            && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, handDemon.Id, deckDemon.Id, right.Id }),
            "hand then deck Demons summoned beside actual source position");
        Core2026Proof.Check(handDemon.AttackDamage == 9 && handDemon.HasTaunt && deckDemon.AttackDamage == 5 && deckDemon.Health == 7
            && ordinary.Zone == p.HandZone && !p.DeckZone.Contains(deckDemon), "actual entities retain stats; recruited Doomguard does not discard");
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "seeded clone recruitment parity");
        ClearBoard(g, p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1 && ordinary.Zone == p.HandZone, "no eligible Demons: no summons, non-Demons untouched");
        ClearBoard(g, p);
        var randomHand = new[] { Add(p, "CS2_065"), Add(p, "EX1_310") };
        var randomDeck = new[] { Entity.FromCard(p, Cards.FromId("CS2_065"), zone: p.DeckZone), Entity.FromCard(p, Cards.FromId("EX1_310"), zone: p.DeckZone) };
        source = (Minion)Add(p, id); p.UsedMana = 0;
        clone = g.CloneExact(history: true);
        Core2026Proof.Check(clone.Process(PlayCardTask.Any(clone.Player1, (IPlayable)clone.IdEntityDic[source.Id])) && g.Process(PlayCardTask.Any(p, source)), "random recruits played in both games");
        Core2026Proof.Check(p.BoardZone.Count == 3 && randomHand.Count(m => m.Zone == p.BoardZone) == 1
            && randomDeck.Count(m => m.Zone == p.BoardZone) == 1 && Canonical.State(g) == Canonical.State(clone), "one seeded random Demon from each zone");
        foreach (int spaces in new[] { 0, 1, 2 })
        {
            ClearBoard(g, p); for (int i = 0; i < 6 - spaces; i++) Summon(p, "CS2_182");
            var h = Add(p, "CS2_065"); var d = Entity.FromCard(p, Cards.FromId("CS2_065"), zone: p.DeckZone);
            int handBefore = p.HandZone.Count, deckBefore = p.DeckZone.Count;
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.HandZone.Count == handBefore - (spaces > 0 ? 1 : 0)
                && p.DeckZone.Count == deckBefore - (spaces > 1 ? 1 : 0), "board capacity preserves unsummoned cards: " + spaces);
            foreach (var m in p.HandZone.Where(c => c is Minion demon && demon.IsRace(Race.DEMON)).ToArray()) p.SetasideZone.Add(p.HandZone.Remove(m));
            foreach (var m in p.DeckZone.Where(c => c is Minion demon && demon.IsRace(Race.DEMON)).ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(m));
        }
        foreach (bool removed in new[] { false, true })
        {
            ClearBoard(g, p);
            left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
            deckDemon = (Minion)Entity.FromCard(p, Cards.FromId("DINO_132"), zone: p.DeckZone);
            if (removed) Kill(g, source); else left[GameTag.DORMANT] = 1;
            source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(deckDemon.Zone == p.BoardZone && deckDemon.IsRace(Race.DEMON) && deckDemon.IsRace(Race.BEAST)
                && p.BoardZone.Select(m => m.Id).SequenceEqual(removed
                    ? new[] { left.Id, right.Id, deckDemon.Id }
                    : new[] { left.Id, source.Id, deckDemon.Id, right.Id }),
                "deck-only dual-type Demon; removed source appends, Dormant neighbour counts: " + removed);
            left[GameTag.DORMANT] = 0;
        }
    });
}
