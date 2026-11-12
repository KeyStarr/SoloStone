using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HighCultistHerennProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_810", Unpinned_HighCultistHerenn);
    private static void Unpinned_HighCultistHerenn(Game g, Controller p, string id)
    {
        PoolPilotProof.ClassDeck(p);
        var first = (Minion)Entity.FromCard(p, Cards.FromId("EX1_556"), zone: p.DeckZone);
        var second = (Minion)Entity.FromCard(p, Cards.FromId("FP1_002"), zone: p.DeckZone);
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
        var attack = g.HistoryFacts.Query.AttacksStarted().Single();
        Core2026Proof.Check(new[] {first.Id, second.Id}.Contains(attack.AttackerId) && new[] {first.Id, second.Id}.Contains(attack.TargetId),
            "Unpinned_HerennFightOrder: one friendly-vs-friendly combat with retaliation");
        var recruited = new[] {first, second}.OrderBy(m => m.OrderOfPlay).ToArray();
        Core2026Proof.Check(attack.AttackerId == recruited[0].Id && attack.TargetId == recruited[1].Id && p.DeckZone.IsEmpty
            && recruited.All(m => m.NumAttacksThisTurn == 0), "Unpinned_HerennFightOrder: first recruit initiates after both summons");
        ForcedCardProof.Clean(g); PoolPilotProof.ClassDeck(p);
        first = (Minion)Entity.FromCard(p, Cards.FromId("EX1_556"), zone: p.DeckZone);
        int cursor = g.HistoryFacts.Count; Play(g, p, id);
        Core2026Proof.Check(first.Zone == p.BoardZone && first.Damage == 0 && !g.HistoryFacts.Query.AttacksStarted().Any(a => a.Sequence > cursor),
            "Unpinned_HerennShortRecruit: lone recruit does not fight Herenn");
        ForcedCardProof.Clean(g); PoolPilotProof.ClassDeck(p);
        first = (Minion)Entity.FromCard(p, Cards.FromId("EX1_556"), zone: p.DeckZone);
        second = (Minion)Entity.FromCard(p, Cards.FromId("FP1_002"), zone: p.DeckZone);
        while (p.BoardZone.SlotCount < 5) Summon(p, "CS2_182");
        cursor = g.HistoryFacts.Count; Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == 1 && p.BoardZone.SlotCount == 7 && !g.HistoryFacts.Query.AttacksStarted().Any(a => a.Sequence > cursor),
            "Unpinned_HerennShortRecruit: capacity-blocked recruit stays in deck; no fight");
        Unpinned_RecruitAfterSummonDeaths(g, p, id);
    }

    private static void Unpinned_RecruitAfterSummonDeaths(Game g, Controller p, string id)
    {
        ForcedCardProof.Clean(g); PoolPilotProof.ClassDeck(p);
        var location = LocationProofs.Location(p);
        var juggler = Summon(p, "NEW1_019");
        var abomination = Summon(p.Opponent, "EX1_097"); abomination.Damage = 3;
        var first = Entity.FromCard(p, Cards.FromId("EX1_029"), zone: p.DeckZone);
        var second = Entity.FromCard(p, Cards.FromId("EX1_029"), zone: p.DeckZone);
        var source = Add(p, id); p.UsedMana = 0; g.SetRandomSeed(1);
        int cursor = g.HistoryFacts.Count;
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1;
            Core2026Proof.Check(next.Process(PlayCardTask.Any(owner, next.IdEntityDic[source.Id], zonePosition: owner.BoardZone.SlotCount)),
                "Unpinned_HerennShortRecruit: play completes after first recruit's summon deaths");
            Core2026Proof.Check(next.IdEntityDic[abomination.Id].Zone == owner.Opponent.GraveyardZone
                && next.IdEntityDic[juggler.Id].Zone == owner.GraveyardZone,
                "fixture: knife kills Abomination and its Deathrattle kills Juggler");
            var recruits = new[] { next.IdEntityDic[first.Id], next.IdEntityDic[second.Id] };
            Core2026Proof.Check(owner.DeckZone.IsEmpty && recruits.Count(m => m.Zone == owner.GraveyardZone) == 1
                && recruits.Count(m => m.Zone == owner.BoardZone) == 1
                && owner.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, source.Id,
                    recruits.Single(m => m.Zone == owner.BoardZone).Id }),
                "Unpinned_HerennShortRecruit: second deck entity enters valid physical slot after first dies");
            Core2026Proof.Check(!next.HistoryFacts.Query.AttacksStarted().Any(a => a.Sequence > cursor),
                "Unpinned_HerennFightOrder: departed first recruit prevents fight");
        }
    }
}
