using System.Xml.Serialization;

internal static class AchievementEvaluatorContractChecks
{
    internal static void Run()
    {
        var pending = new LocalAchievementPendingContext {
            MatchId = "match-proof",
            EligibleWorldMatch = true,
            PersonalDeck = true,
            StartingHero = "HERO_08",
            StartingHeroClass = "MAGE",
            QueuedNpcId = "npc-proof",
            QueuedNpcBand = "Diamond",
            MainDeck = new List<LocalAchievementDeckCard> {
                new LocalAchievementDeckCard { BaseCardId = "CS2_029", Copies = 2, CardType = "SPELL",
                    CardClass = "MAGE", BaseCost = 4, Rarity = "FREE", CardSet = "CORE" }
            }
        };
        LocalAchievementPendingContext copy;
        using (var buffer = new MemoryStream())
        {
            new XmlSerializer(typeof(LocalAchievementPendingContext)).Serialize(buffer, pending);
            buffer.Position = 0;
            copy = (LocalAchievementPendingContext)new XmlSerializer(typeof(LocalAchievementPendingContext)).Deserialize(buffer);
        }
        Check(copy.MatchId == pending.MatchId && copy.EligibleWorldMatch && copy.StartingHeroClass == "MAGE"
            && copy.MainDeck.Single().Copies == 2 && copy.MainDeck.Single().CardClass == "MAGE",
            "achievement pending context lost frozen match/deck semantics");

        var unavailable = new LocalAchievementMatchEvidence(pending.MatchId, "Won", pending.StartingHero,
            pending.QueuedNpcId, pending.QueuedNpcBand, false, false, false);
        var result = LocalAchievementEvaluator.Evaluate(pending, unavailable, new LocalAchievementCareer(), Array.Empty<string>());
        Check(result.Earned.Count == 0 && result.CareerDelta == LocalAchievementCareerDelta.Empty,
            "unavailable authoritative evidence must fail closed without blocking ordinary settlement");
        Console.WriteLine("PASS achievement evaluator contract: pending XML + unavailable fail-closed");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
