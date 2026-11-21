using SabberStoneCore.Enums;
using static Core2026Proof;

// Godfrey the Betrayer (JAIL_509) is Playerbound: its deck-portal visual (hero tag 4717) follows the player's current hero
// on every hero swap, a hero card play (PlayHeroCore) and a hero replacement (Lord Jaraxxus through ReplaceHeroTask).
internal static class GodfreyPortalHeroReplaceProof
{
    [GroupProof("godfrey-portal")]
    internal static void Run()
    {
        foreach (var (cardId, heroId, portal) in new[] {
            ("EX1_323", "EX1_323h", 1),
            ("ICC_827", "ICC_827", 1),
            ("EX1_323", "EX1_323h", 0) })
        {
            var game = HistoryFactsProof.Fixture();
            var player = game.Player1;
            var oldHero = player.Hero;
            oldHero[(GameTag)4717] = portal;

            Play(game, player, cardId);

            Core2026Proof.Check(player.Hero != oldHero && player.Hero.Card.Id == heroId,
                cardId + ": played from hand and replaced hero");
            Core2026Proof.Check(player.Hero[(GameTag)4717] == portal,
                cardId + $": new hero portal expected {portal}, got {player.Hero[(GameTag)4717]}");
            Core2026Proof.Check(oldHero[(GameTag)4717] == 0,
                cardId + ": old hero portal cleared");
            Console.WriteLine($"PASS Godfrey portal: {cardId}, portal={portal}, new={player.Hero[(GameTag)4717]}, old={oldHero[(GameTag)4717]}");
        }
    }
}
