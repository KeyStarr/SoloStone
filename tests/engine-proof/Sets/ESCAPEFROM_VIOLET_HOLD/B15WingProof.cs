using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B15WingProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_987", LowSecurityWing);

    private static void LowSecurityWing(Game game, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id);
        var filter = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.SHAMAN, type: CardType.MINION);
        var pool = OfflineCardPools.Candidates(game.OfflinePools, p.HeroClass, filter, 126938);
        Core2026Proof.Check(pool.Count >= 3 && pool.All(c => c.Type == CardType.MINION), "Wing declared exact Shaman minion bucket");
        Console.WriteLine("POOL JAIL_987 exact-Shaman-minion=" + pool.Count);
        var results = new List<string>();
        foreach (var next in new[] { game.CloneExact(true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var owner = next.Player1;
            LocationProofs.Use((BoardObject)next.IdEntityDic[location.Id]);
            var card = owner.HandZone.Single(); results.Add(card.Card.Id);
            Core2026Proof.Check(pool.Contains(card.Card) && card[GameTag.CANT_PLAY] == 1 && !card.IsPlayableByPlayer,
                "Wing generates admitted minion into locked hand");
            End(next);
            Play(next, next.Player2, "CS2_182");
            Core2026Proof.Check(card[GameTag.CANT_PLAY] == 1, "opponent play cannot unlock Wing card");
            End(next);
            Core2026Proof.Check(card[GameTag.CANT_PLAY] == 1, "turn boundaries alone cannot unlock Wing card");
            Play(next, owner, "CS2_182");
            Core2026Proof.Check(card[GameTag.CANT_PLAY] == 0, "own next committed card unlocks Wing card");
        }
        Core2026Proof.Check(results.Distinct().Count() == 1, "Wing clone/snapshot preserves seeded generation");
        var full = PlayLockRulesProof.Fixture();
        var wing = LocationProofs.Play(full.Player1, id);
        while (!full.Player1.HandZone.IsFull) Add(full.Player1, "CS2_182");
        while (!full.Player1.BoardZone.IsFull) Summon(full.Player1, "CS2_182");
        LocationProofs.Use(wing);
        Core2026Proof.Check(full.Player1.HandZone.All(c => c[GameTag.CANT_PLAY] == 0)
            && full.Player1.GraveyardZone.Any(c => pool.Contains(c.Card)) && wing.Durability == 2,
            "full board does not block Wing; full hand burns output without lock; charge spent");
        var countered = PlayLockRulesProof.Fixture();
        wing = LocationProofs.Play(countered.Player1, id); LocationProofs.Use(wing);
        var locked = countered.Player1.HandZone.Single();
        End(countered); Play(countered, countered.Player2, "EX1_287"); End(countered);
        var spell = (Spell)Play(countered, countered.Player1, "CS2_024", countered.Player2.Hero);
        Core2026Proof.Check(spell.IsCountered && locked[GameTag.CANT_PLAY] == 0,
            "countered spell is still another played card and unlocks Wing output");
    }
}
