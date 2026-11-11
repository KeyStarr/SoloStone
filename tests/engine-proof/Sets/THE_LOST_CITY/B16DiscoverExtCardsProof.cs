using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static partial class B16DiscoverExtCardsProof
{
    [GroupProof("b16-discover-ext-cards", ProofKind.Cards)]
    internal static void Run()
    {
        VaultCustomDiscover();
        StandardSetsProof.Case("JAIL_319", Unpinned_SkeletonKey);
        StandardSetsProof.Case("TIME_861", Unpinned_TokiTrio);
        StandardSetsProof.Case("TLC_464", Unpinned_MountainMap);
        StandardSetsProof.Case("TLC_483", VaultBreaker);
        StandardSetsProof.Case("TLC_515", Unpinned_CultistMap);
    }

    private static void Unpinned_MountainMap(Game g, Controller p, string id)
    {
        Console.WriteLine("Mountain Map SHAMAN minion bucket=" + OfflineCardPools.Candidates(g, CardClass.SHAMAN,
            OfflinePoolFilter.Discover(type: CardType.MINION), 118225).Count);
        var exhausted = DiscoverRealProof.New();
        Play(exhausted, exhausted.Player1, "GIL_681");
        Play(exhausted, exhausted.Player1, id);
        Core2026Proof.Check(exhausted.Player1.Choice == null && exhausted.Player1.HandZone.Count == 0,
            "played ALL types exhaust exact Mountain bucket; no fallback or stalled choice");
        Play(g, p, id);
        var offers = DiscoverRealProof.Offers(g);
        Core2026Proof.Check(offers.Length == 3 && offers.Select(c => c.Card.Id).Distinct().Count() == 3
            && offers.All(c => c is Minion && c.Card.Races.Count > 0), "three distinct typed offers");
        var selected = offers.First();
        DiscoverRealProof.Answer(g, selected.Id);
        Core2026Proof.Check(p.HandZone.Single().Id == selected.Id, "Map delivers selected entity");
        var expected = offers.Where(c => c.Id != selected.Id).Select(c => c.Card.Id).ToArray();
        DiscoverRealProof.Saved(g.CloneExact(history: true), next => {
            PlayEntity(next, next.Player1, next.IdEntityDic[selected.Id]);
            while (next.Player1.Choice != null && !DiscoverRealProof.Offers(next).Select(c => c.Card.Id).SequenceEqual(expected))
                DiscoverRealProof.Answer(next, next.Player1.Choice.Choices.First());
            Core2026Proof.Check(DiscoverRealProof.Offers(next).Select(c => c.Card.Id).SequenceEqual(expected), "Mountain retains original rejected order after minion effects");
            DiscoverRealProof.Answer(next, next.Player1.Choice.Choices.First());
        });
        End(g);
        Core2026Proof.Check(!p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_464e"), "Map expires at owner end turn");
    }

    private static void VaultBreaker(Game g, Controller p, string id)
    {
        var vault = Summon(p, id); Summon(p.Opponent, id);
        var silenced = g.CloneExact(history: true);
        ((Minion)silenced.IdEntityDic[vault.Id]).Silence();
        DiscoverRealProof.Deck(silenced.Player1, "CS2_029"); Play(silenced, silenced.Player1, "TLC_515");
        DiscoverRealProof.Answer(silenced, DiscoverRealProof.Offers(silenced).Single().Id);
        Core2026Proof.Check(silenced.Player1.HandZone.Single().Cost == 4, "Silenced Vault and opponent Vault do not discount");
        DiscoverRealProof.Deck(p, "CS2_029");
        Play(g, p, "TLC_515");
        var keepAll = DeathKnightC1Proof.NewGame(1); var dk = keepAll.Player1;
        PoolPilotProof.EmptyHand(dk); Summon(dk, id); dk[GameTag.CORPSES] = 5;
        Play(keepAll, dk, "TLC_434");
        Core2026Proof.Check(dk.HandZone.Count == 3 && dk.HandZone.All(c => c.Cost == Math.Max(0,c.Card.Cost-1)),
            "Vault discounts every kept Discover option exactly once");
        var offered = DiscoverRealProof.Offers(g).Single();
        DiscoverRealProof.Saved(g, next => {
            DiscoverRealProof.Answer(next, offered.Id);
            Core2026Proof.Check(next.Player1.HandZone.Single().Cost == 3, "only friendly Vault reduces actual drawn card by one");
        });
    }

    private static void VaultCustomDiscover()
    {
        foreach (string id in new[] { "TIME_016", "TIME_704", "TIME_857", "CATA_614", "DINO_430" })
        foreach (bool opponent in new[] { false, true }) {
            var g = GiftedDiscoverProof.New(1, CardClass.MAGE);
            if (opponent) End(g);
            var p = g.CurrentPlayer;
            PoolPilotProof.EmptyHand(p);
            Summon(p, "TLC_483"); Summon(p.Opponent, "TLC_483");
            Play(g, p, id);
            for (int round = 0; round < (id == "TIME_857" ? 2 : 1); round++) {
                var offers = DiscoverRealProof.Offers(g);
                Core2026Proof.Check(offers.Length > 0 && offers.Select(c => c.Card.Id).Distinct().Count() == offers.Length,
                    id + " has distinct custom Discover offers");
                var selected = offers.OrderByDescending(c => c.Cost).First();
                int expected = Math.Max(0, selected.Cost - 1 - (id == "TIME_857" ? 2 : 0));
                int completed = g.HistoryFacts.Query.Events<DiscoverCompletedFact>().Count();
                DiscoverRealProof.Saved(g, next => {
                    DiscoverRealProof.Answer(next, selected.Id);
                    Core2026Proof.Check(next.IdEntityDic[selected.Id].Cost == expected,
                        id + " Vault discounts the selected custom offer exactly once");
                    Core2026Proof.Check(next.HistoryFacts.Query.Events<DiscoverCompletedFact>().Count() == completed + 1,
                        id + " records exactly one semantic Discover completion");
                });
            }
            Core2026Proof.Check(p.Choice == null, id + " completes every custom decision");
        }
        foreach (string id in new[] { "TLC_521", "TIME_860", "MEND_301" }) {
            var g = GiftedDiscoverProof.New(1, CardClass.MAGE); var p = g.Player1;
            Summon(p, "TLC_483");
            Play(g, p, id);
            var selected = DiscoverRealProof.Offers(g).First();
            int cost = selected.Cost;
            DiscoverRealProof.Answer(g, selected.Id);
            Core2026Proof.Check(g.IdEntityDic[selected.Id].Cost == cost
                && !g.HistoryFacts.Query.Events<DiscoverCompletedFact>().Any(), id + " non-Discover pick gets no completion or Vault discount");
        }
    }

    private static void Unpinned_CultistMap(Game g, Controller p, string id)
    {
        var originals = DiscoverRealProof.Deck(p, "CS2_182", "CS2_200", "CS2_231");
        Play(g, p, id);
        var offers = DiscoverRealProof.Offers(g);
        int selected = DiscoverRealProof.Bound(offers[0]);
        DiscoverRealProof.Answer(g, offers[0].Id);
        var bounced = g.CloneExact(history: true);
        var held = bounced.IdEntityDic[selected];
        bounced.Player1.SetasideZone.Add(bounced.Player1.HandZone.Remove(held));
        Generic.AddHandPhase(bounced.Player1, bounced.Player1.SetasideZone.Remove(held));
        PlayEntity(bounced, bounced.Player1, held);
        Core2026Proof.Check(bounced.Player1.Choice == null, "interrupted first hand residence cannot qualify");
        var transformed = g.CloneExact(history: true);
        var original = transformed.IdEntityDic[selected].Card;
        var changed = Generic.ChangeEntityBlock(transformed.Player1, transformed.IdEntityDic[selected], Cards.FromId("GAME_005"), true);
        changed = Generic.ChangeEntityBlock(transformed.Player1, changed, original, true);
        PlayEntity(transformed, transformed.Player1, changed);
        Core2026Proof.Check(transformed.Player1.Choice == null, "transform and back invalidates original watch");
        var missing = g.CloneExact(history: true);
        foreach (var card in missing.Player1.DeckZone.ToArray()) missing.Player1.SetasideZone.Add(missing.Player1.DeckZone.Remove(card));
        PlayEntity(missing, missing.Player1, missing.IdEntityDic[selected]);
        Core2026Proof.Check(missing.Player1.Choice == null, "no rejected survivors means no second choice");
        DiscoverRealProof.Saved(g, next => {
            PlayEntity(next, next.Player1, next.IdEntityDic[selected]);
            var second = DiscoverRealProof.Offers(next);
            Core2026Proof.Check(second.Length == 2 && second.All(c => DiscoverRealProof.Bound(c) != selected), "only retained rejected originals offered");
            int drawn = DiscoverRealProof.Bound(second[0]);
            var full = next.CloneExact(history: true);
            while (!full.Player1.HandZone.IsFull) Add(full.Player1, "CS2_231");
            DiscoverRealProof.Answer(full, second[0].Id);
            Core2026Proof.Check(full.Player1.HandZone.Count == 10 && full.Player1.DeckZone.Count == 1 && full.Player1.Choice == null,
                "full-hand follow-up burns original without stall");
            DiscoverRealProof.Answer(next, second[0].Id);
            Core2026Proof.Check(next.Player1.HandZone.Single().Id == drawn && next.Player1.DeckZone.Count == 1, "follow-up draws original");
        });
        Unpinned_CultistNestedChoice("CORE_WON_096");
        Unpinned_CultistNestedChoice("KAR_057");
        Unpinned_MapRewind();
    }

    private static void Unpinned_CultistNestedChoice(string discoverer)
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        var originals = DiscoverRealProof.Deck(p, discoverer, "CS2_182", "CS2_200");
        Play(g, p, "TLC_515");
        var offer = DiscoverRealProof.Offers(g).Single(c => c.Card.Id == discoverer);
        DiscoverRealProof.Answer(g, offer.Id);
        PlayEntity(g, p, originals[0]);
        Core2026Proof.Check(p.Choice != null && DiscoverRealProof.Offers(g).All(c => DiscoverRealProof.Bound(c) == 0), "played Battlecry opens its own choice first");
        DiscoverRealProof.Saved(g, next => {
            DiscoverRealProof.Answer(next, next.Player1.Choice.Choices.First());
            Core2026Proof.Check(DiscoverRealProof.Offers(next).Select(DiscoverRealProof.Bound).SequenceEqual(originals.Skip(1).Select(c => c.Id)),
                "Map opens only after nested Battlecry finishes");
            DiscoverRealProof.Answer(next, next.Player1.Choice.Choices.First());
        });
    }
    private static void Unpinned_MapRewind()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        Play(g, p, "TIME_861");
        int[] tracked = p.HandZone.Select(c => c.Id).ToArray();
        DiscoverRealProof.Deck(p, "TIME_000", "CS2_182", "CS2_200");
        Play(g, p, "TLC_515");
        var offer = DiscoverRealProof.Offers(g).Single(c => c.Card.Id == "TIME_000");
        int original = DiscoverRealProof.Bound(offer);
        DiscoverRealProof.Answer(g, offer.Id);
        PlayEntity(g, p, g.IdEntityDic[original]);
        Core2026Proof.Check(p.Choice != null && !RewindTestHelpers.IsRewindOffered(g), "Map bonus settles before Rewind prompt");
        DiscoverRealProof.Answer(g, p.Choice.Choices.First());
        Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(g), "Rewind waits for complete Map resolution");
        DiscoverRealProof.Saved(g, next => {
            RewindTestHelpers.AnswerRewind(next);
            Core2026Proof.Check(next.Player1.DeckZone.Count == 2 && next.Player1.Choice != null,
                "Rewind restores rejected originals and independent Map watch before retry");
            DiscoverRealProof.Answer(next, next.Player1.Choice.Choices.First());
            RewindTestHelpers.KeepIfOffered(next);
            Core2026Proof.Check(next.Player1.DeckZone.Count == 1 && tracked.All(id => next.Player1.HandZone.Any(c => c.Id == id))
                && next.Player1.AppliedEnchantments.Count(e => e.Card.Id == "TIME_861e1") == 1,
                "rewound bonus draws once; unrelated Toki tracker and generated identities survive");
        });
    }

}
