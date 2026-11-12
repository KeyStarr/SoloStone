using HsSim;
using SabberStoneCore.CardSets.Client;

internal static class PoolPilotDeckProof
{
    internal static void Run()
    {
        var decks = Simulation.DeckIds("offline-standard-pools-v1").Select(Simulation.Deck).ToArray();
        Core2026Proof.Check(decks.Length == 2 && decks.All(d => d.Cards.Length == 30 && Simulation.Missing(d).Length == 0), "two complete fixed pilot decks");
        Core2026Proof.Check(OfflinePoolDeclarations.Pilots.All(c => decks.SelectMany(d => d.Cards).Contains(c.CardId)), "fixed decks cover nine pilots");
        string[] pilotIds = "CATA_484 CATA_723 BE_854 JAIL_125 BE_220 EDR_530 TIME_040 JAIL_328 EDR_848".Split(' ');
        Core2026Proof.Check(pilotIds.All(id => OfflinePoolDeclarations.Cards.Any(c => c.CardId == id)
            && decks.SelectMany(d => d.Cards).Contains(id)), "fixed decks cover original nine pilots");
        Core2026Proof.Check(OfflinePoolDeclarations.DeathKnight.All(c => StandardSets.CollectibleIds.Contains(c.CardId)
            && LocalDecks.TestLoaners.Any(d => d.CardIds.Contains(c.CardId))), "every Death Knight recipe has registered player-loaner coverage");
        Core2026Proof.Check(decks.Single(d => d.Hero == "HERO_04").Cards.All(id => SabberStoneCore.Model.Cards.FromId(id).Class != SabberStoneCore.Enums.CardClass.NEUTRAL), "Paladin fixture permits Bruiser deathrattle");
        Core2026Proof.Check(Simulation.DeckIds("standard-batch-1").Select(Simulation.Deck).All(d => d.Hero is "HERO_01" or "HERO_07"), "growing stress corpus keeps Herald-capable heroes");
        Console.WriteLine("PASS offline pool fixed decks: two explicit 30-card decks; nine pilots; no-neutral Paladin");
    }
}
