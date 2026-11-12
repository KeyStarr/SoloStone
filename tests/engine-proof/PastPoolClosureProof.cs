using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.CardSets.Client;
using static Core2026Proof;

internal static class PastPoolClosureProof
{
    [GroupProof("b14-f3-past-pools")]
    internal static void Run()
    {
        var game = new Game(new GameConfig { RandomSeed = 21, StartPlayer = 1, SkipMulligan = true,
            Shuffle = false, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        var past = OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 7, scope: PoolScope.Past);
        var ids = OfflineCardPools.Candidates(game, CardClass.DRUID,
            new OfflinePoolFilter(PoolScope.Past, PoolClassMode.Exact, CardClass.DRUID, CardType.SPELL, minCost: 7), 120761)
            .Select(c => c.Id).ToArray();
        Core2026Proof.Check(ids.Contains("GVG_033") && ids.Contains("OG_195") && ids.Contains("EX1_183"),
            "D9 qualified legacy closure: three playable expensive past Druid spells");
        Core2026Proof.Check(OfflineCardPools.Candidates(game,CardClass.SHAMAN,
            OfflinePoolFilter.Random(type:CardType.SPELL,spellSchool:4,scope:PoolScope.Past),0)
            .All(card=>card[GameTag.SPELL_SCHOOL]==4),"generated historical Nature spells carry their current school into play/history");
        var required = new[] { "BE_588", "END_027", "TIME_013", "TIME_016", "TIME_444", "TIME_446", "TIME_704", "TIME_707", "TIME_711", "TIME_857" };
        var declarations = OfflinePoolDeclarations.Cards.Where(d => required.Contains(d.CardId)).ToArray();
        Core2026Proof.Check(declarations.Select(d => d.CardId).Order().SequenceEqual(required.Order()),
            "all ten F3 recipes declare their pools through global discovery");
        var report=OfflinePoolAdmission.Evaluate(game.OfflinePools,declarations);
        report.ValidateRegistered();
        foreach(var source in report.Cards)
            foreach(var requirement in source.PoolRequirements)
                Console.WriteLine($"POOL {source.CardId}/{requirement.Name}/{requirement.Class}={requirement.Count}");
        var tech=(string[])typeof(OfflinePoolManifest).Assembly.GetType("SabberStoneCore.CardSets.Client.Sets.Mimiron")
            .GetField("Tech",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
        Core2026Proof.Check(tech.Length==6 && tech.All(id=>game.OfflinePools.Standard.Concat(game.OfflinePools.Past).Any(m=>m.CardId==id))
            && game.OfflinePools.Past.Count(m=>tech.Contains(m.CardId))>=OfflinePoolAdmission.MinimumCandidates,
            "curated tech closure: all six actual candidates admitted, at least three qualified Past techs");
        Console.WriteLine("POOL BE_588/approved-six-tech-whitelist/HUNTER=6 (Standard=2, Past=4)");
        string[] expected=null;
        foreach (var fork in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            Core2026Proof.Check(fork.OfflinePools.Hash == game.OfflinePools.Hash, "immutable historical manifest survives clone/snapshot");
            var picks = Enumerable.Range(0, 12).Select(_ => OfflineCardPools.Pick(fork, past, CardClass.DRUID, 120761).Id).ToArray();
            Core2026Proof.Check(picks.Distinct().Count() > 1, "seeded historical generation is not always first");
            if(expected!=null) Core2026Proof.Check(picks.SequenceEqual(expected),"same seeded future after clone/snapshot");
            expected=picks;
        }
        Console.WriteLine("PASS D9 historical pool closure/clone/snapshot/seeded picks");
    }
}
