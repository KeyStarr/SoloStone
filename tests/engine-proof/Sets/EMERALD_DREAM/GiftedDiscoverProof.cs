using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static PoolPilotProof;

// Shared assertions catch wrong filters, premature grants, lost saved choices and wrong continuations.
internal static class GiftedDiscoverProof
{
    // No Death Knight hero primitive; admission still checks its candidate domain.
    internal static readonly CardClass[] AllClasses = { CardClass.DEMONHUNTER,
        CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST,
        CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR };

    internal static Game New(long seed, CardClass hero)
    {
        var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, Player1HeroClass = hero,
            Player2HeroClass = CardClass.MAGE, FormatType = FormatType.FT_STANDARD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame(); EmptyHand(g.Player1); EmptyHand(g.Player2); return g;
    }

    internal static IPlayable Open(Game g, string id)
    {
        if (id == "EDR_456") Add(g.Player1, "TIME_056");
        return Play(g, g.Player1, id, id == "FIR_939" ? g.Player2.Hero : null);
    }

    internal static IPlayable[] Offers(Game g, string id, OfflinePoolFilter expected)
    {
        var p = g.Player1;
        Core2026Proof.Check(p.Choice != null, id + " opens Discover");
        if (Cards.FromId(id).Type == CardType.MINION)
            Core2026Proof.Check(g.Resolution.HasSuspendedEffect && Generic.HasDecisionPlay(p), "Battlecry waits for chosen offer before finishing outer play");
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Length > 0 && offers.Length <= 3 && offers.Select(o => o.Card.Id).Distinct().Count() == offers.Length,
            "one to three distinct offers");
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, expected, Cards.FromId(id).AssetId);
        foreach (var offer in offers)
        {
            var member = g.OfflinePools.Standard.Single(m => m.CardId == offer.Card.Id);
            Core2026Proof.Check(offer is Minion && offer.Card.Id != id && candidates.Any(c => c.Id == offer.Card.Id), "filter and source exclusion");
            Core2026Proof.Check(expected.ClassMode == PoolClassMode.Exact ? member.Classes.Contains(expected.ExactClass.Value.ToString()) :
                member.Classes.Contains(p.HeroClass.ToString()) || member.Classes.Contains("NEUTRAL"), "printed class membership");
            Core2026Proof.Check(!expected.MinCost.HasValue || offer.Card.Cost >= expected.MinCost.Value, "minimum cost");
            Core2026Proof.Check(!expected.MaxCost.HasValue || offer.Card.Cost <= expected.MaxCost.Value, "maximum cost");
            Core2026Proof.Check(!expected.Race.HasValue || ((Minion)offer).IsRace(expected.Race.Value), "required race");
            Core2026Proof.Check(!expected.Rarity.HasValue || offer.Card.Rarity == expected.Rarity.Value, "required rarity");
            Core2026Proof.Check(!expected.Keyword.HasValue || offer.Card[expected.Keyword.Value] > 0, "required printed keyword");
            Core2026Proof.Check(!DarkGifts.Has(offer) && offer.Zone == p.SetasideZone, "offers have no gift attachment before choice");
            var spell = g.IdEntityDic[offer[GameTag.DARK_GIFT_ENTITY]];
            Core2026Proof.Check(DarkGifts.Registry.Any(gift => gift.SpellId == spell.Card.Id && gift.IsEligible(offer)), "eligible preassigned gift");
            Core2026Proof.Check(spell[GameTag.CREATOR] == p.Choice.SourceId && g.IdEntityDic[p.Choice.SourceId].Card.Id == "EDR_102t", "native executor and spell links");
        }
        Core2026Proof.Check(offers.Select(o => g.IdEntityDic[o[GameTag.DARK_GIFT_ENTITY]].Card.Id).Distinct().Count() == offers.Length, "distinct gift per offer");
        return offers;
    }

    internal static string Vector(Game g) => string.Join(",", g.Player1.Choice.Choices.Select(i =>
        g.IdEntityDic[i].Card.Id + "/" + g.IdEntityDic[g.IdEntityDic[i][GameTag.DARK_GIFT_ENTITY]].Card.Id));

    // Like DarkGiftBehaviourProof: replace a saved pairing to exercise Sweet Dreams at every seed.
    internal static void Pair(Game g, IPlayable offer, string spellId)
    {
        var spell = Entity.FromCard(g.Player1, Cards.FromId(spellId),
            new EntityData { [GameTag.CREATOR] = g.Player1.Choice.SourceId }, g.Player1.SetasideZone);
        offer[GameTag.DARK_GIFT_ENTITY] = spell.Id;
    }

    internal static void Choose(Game g, IPlayable[] offers, int index = 0, bool complete = true)
    {
        int chosenId = offers[index].Id;
        var view = g.GetPendingDecisions().Single();
        var answer = new DecisionAnswer(view.Id, view.ControllerId, new[] { chosenId });
        Core2026Proof.Check(g.TryAnswer(answer, out var error), "answer selected offer: " + error);
        if (complete) RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(complete ? g.Player1.Choice == null && !g.Resolution.HasSuspendedEffect :
            g.Player1.Choice != null && g.Resolution.HasSuspendedEffect, "choice and continuation advance exactly once");
        Core2026Proof.Check(offers.Where(o => o.Id != chosenId).All(o => !DarkGifts.Has(g.IdEntityDic[o.Id])), "unchosen have no gift attachments");
    }

    internal static void Exercise(long seed, string id, CardClass[] classes, OfflinePoolFilter expected,
        bool discount = false, bool copy = false, bool shuffle = false)
    {
        SmallPools(seed, id, classes[0], expected);
        if (Cards.FromId(id).Type == CardType.MINION)
        {
            ForeignBattlecry(seed, id, classes[0], expected, copy);
            RepeatBattlecry(seed, id, classes[0], expected);
        }
        foreach (var hero in classes)
        foreach (var mode in new[] { "normal", "full-hand", "full-board", "sweet" })
        {
            var g = New(seed, hero); var p = g.Player1;
            if (mode == "full-board")
                while (p.BoardZone.FreeSpace > (Cards.FromId(id).Type == CardType.MINION ? 1 : 0)) Summon(p, "CS2_182");
            var source = Open(g, id);
            var offers = Offers(g, id, expected);
            Core2026Proof.Check(offers.Length == 3, "full admitted pool offers three");
            if (mode == "normal")
            {
                var again = New(seed, hero); Open(again, id); Offers(again, id, expected);
                Core2026Proof.Check(Vector(g) == Vector(again) && g.Random.CaptureState().Equals(again.Random.CaptureState()), "same seed reproduces offers and gifts");
            }
            if (mode == "sweet") Pair(g, offers[0], "EDR_100t8");
            if (mode == "full-hand") FillHand(p);
            int hand = p.HandZone.Count, deck = p.DeckZone.Count, slots = p.BoardZone.SlotCount;
            string giftId = g.IdEntityDic[offers[0][GameTag.DARK_GIFT_ENTITY]].Card.Id;
            bool sweet = giftId == "EDR_100t8";
            int chosenId = offers[0].Id;
            int cost = offers[0].Cost;
            // D7/dg11b-sweet-dreams-sequence: gift relocation precedes card continuation.
            // dg9-copy-retention: Stalker copies the selected entity from its resolved zone.
            Resume(g, next => {
                var owner = next.Player1; var chosen = next.IdEntityDic[chosenId];
                Choose(next, offers);
                Core2026Proof.Check(owner.BoardZone.SlotCount == slots && slots <= 7, "Discover never summons; full board respected");
                if (mode == "full-hand")
                {
                    Core2026Proof.Check(chosen.Zone == owner.GraveyardZone && owner.HandZone.Count == 10 && !DarkGifts.Has(chosen), "full hand burns chosen and clears gift");
                }
                else
                {
                    Core2026Proof.Check(DarkGifts.GiftsOf(chosen).Single().Card.Id == DarkGifts.Registry.Single(x => x.SpellId == giftId).EnchantmentId,
                        "chosen gets its saved gift only");
                    Core2026Proof.Check(chosen.Zone.Type == (sweet ? Zone.DECK : Zone.HAND), "selected card reaches correct zone");
                    if (sweet && !shuffle) Core2026Proof.Check(owner.DeckZone.TopCard.Id == chosen.Id, "Sweet Dreams on deck top");
                    int wantedCost = Math.Max(0, cost - (giftId == "EDR_100t2" ? 2 : 0) - (discount ? 2 : 0));
                    Core2026Proof.Check(!discount || chosen.Cost == wantedCost && chosen[GameTag.COST] == wantedCost, "Cremate visible cost discount and zero floor");
                    Core2026Proof.Check(owner.HandZone.Count == hand + (sweet ? 0 : 1) + (copy ? 1 : 0), "chosen/copy hand count");
                    if (copy)
                    {
                        var duplicate = owner.HandZone.Single(x => x.Id != chosen.Id && x[GameTag.DISPLAYED_CREATOR] == source.Id);
                        Core2026Proof.Check(duplicate.Card.Id == chosen.Card.Id && DarkGifts.Has(duplicate) && duplicate.Cost == chosen.Cost &&
                            ((Minion)duplicate).AttackDamage == ((Minion)chosen).AttackDamage && ((Minion)duplicate).BaseHealth == ((Minion)chosen).BaseHealth,
                            "copy retains chosen gift and modified stats/cost");
                        Core2026Proof.Check(DarkGifts.GiftsOf(duplicate).Single().Id != DarkGifts.GiftsOf(chosen).Single().Id, "copy has fresh attachment");
                    }
                }
                int shuffled = shuffle ? offers.Length - 1 : 0;
                Core2026Proof.Check(owner.DeckZone.Count == deck + shuffled + (sweet && mode != "full-hand" ? 1 : 0), "deck change count");
                foreach (var unchosen in offers.Skip(1).Select(x => next.IdEntityDic[x.Id]))
                    Core2026Proof.Check(unchosen.Zone.Type == (shuffle ? Zone.DECK : Zone.SETASIDE), "unchosen offer destination");
            });
        }
    }
    internal static void SmallPools(long seed, string id, CardClass hero, OfflinePoolFilter filter)
    {
        for (int count = 0; count <= 2; count++)
        {
            var g = New(seed, hero);
            var candidates = OfflineCardPools.Candidates(g, hero, filter, Cards.FromId(id).AssetId);
            var xml = OfflinePoolFixtures.Catalog(); var rows = xml.Root.Element("Members");
            var selected = rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
            if (count == 0)
            {
                selected = rows.Elements("Card").Take(1).ToArray();
                selected[0].SetAttributeValue("scope", "Standard");
                selected[0].SetAttributeValue("set", "CORE");
                selected[0].SetAttributeValue("type", "SPELL");
            }
            rows.ReplaceNodes(selected);
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, OfflinePoolFixtures.Manifest(xml));
            var rng = g.Random.CaptureState();
            Open(g, id);
            if (count == 0)
            {
                // Check the card effect's RNG before answering Keep, which advances the decision path.
                Core2026Proof.Check(g.Random.CaptureState().Equals(rng), "empty pool effect consumes no RNG");
                RewindTestHelpers.KeepIfOffered(g);
                Core2026Proof.Check(g.Player1.Choice == null && !g.Resolution.HasSuspendedEffect,
                    "empty pool finishes without choices after Keep");
                Core2026Proof.Check(!g.IdEntityDic.Values.Any(x => x.Card.Id == "EDR_102t"), "empty pool creates no executor");
            }
            else
            {
                var offers = Offers(g, id, filter);
                Core2026Proof.Check(offers.Length == count && offers.Select(x => x.Card.Id).SequenceEqual(candidates.Take(count).Select(x => x.Id)),
                    "small pool exposes every candidate in order");
                // Synthetic manifests cannot load as installed snapshots; full-pool cases cover load.
                var clone = g.CloneExact(history: true);
                Choose(g, offers, count - 1); Choose(clone, offers, count - 1);
                Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && g.Random.CaptureState().Equals(clone.Random.CaptureState()), "small-pool clone answer");
            }
        }
    }

    private static void ForeignBattlecry(long seed, string id, CardClass hero, OfflinePoolFilter filter, bool copy)
    {
        var g = New(seed, hero); var p = g.Player1;
        var source = Entity.FromCard(p, Cards.FromId("GIL_820"), zone: p.SetasideZone);
        if (id == "EDR_456") Add(p, "TIME_056");
        var recipe = (DecisionBattlecryTask)Cards.FromId(id).Power.PowerTask;
        Core2026Proof.Check(recipe.Recipe.Process(g, p, source, null, new TaskStack()) == TaskState.SUSPENDED,
            id + " foreign Battlecry opens declared Discover");
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        var candidates = OfflineCardPools.Candidates(g, hero, filter, source.Card.AssetId);
        Core2026Proof.Check(offers.Length == 3 && offers.All(o => candidates.Any(c => c.Id == o.Card.Id)
            && o.Card.Id != source.Card.Id && o[GameTag.CREATOR] == source.Id), "foreign Battlecry samples declared pool with runtime creator/exclusion");
        GiftedDiscoverProof.Pair(g, offers[0], "EDR_100t5");
        int hand = p.HandZone.Count;
        Resume(g, next => {
            Choose(next, offers);
            var chosen = next.IdEntityDic[offers[0].Id];
            Core2026Proof.Check(chosen.Zone == next.Player1.HandZone && DarkGifts.Has(chosen)
                && next.Player1.HandZone.Count == hand + (copy ? 2 : 1), "foreign Battlecry completes chosen grant and card continuation");
            var executor = next.IdEntityDic[DarkGifts.GiftsOf(chosen).Single().Creator[GameTag.CREATOR]];
            Core2026Proof.Check(executor.Card.Id == "EDR_102t" && executor[GameTag.CREATOR] == source.Id, "foreign Battlecry keeps runtime executor creator");
            Core2026Proof.Check(recipe.Process(next, next.Player1, next.IdEntityDic[source.Id], null, new TaskStack()) == TaskState.COMPLETE
                && next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect, "foreign Battlecry automatic replay completes without throwing");
        });
    }

    private static void RepeatBattlecry(long seed, string id, CardClass hero, OfflinePoolFilter filter)
    {
        foreach (bool brann in new[] { false, true })
        {
            var g = New(seed, hero); var p = g.Player1;
            if (brann) Summon(p, "LOE_077");
            if (id == "EDR_456") Add(p, "TIME_056");
            var source = Add(p, id);
            Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_100t7e"), p.Hero, source);
            int played = p.PlayHistory.Count;
            PlayEntity(g, p, source);
            // D7 / dg14b-rude-awakening-brann: two separate saved choices, twice total with Brann.
            for (int repetition = 0; repetition < 2; repetition++)
            {
                var offers = Offers(g, id, filter);
                Core2026Proof.Check(p.Choice.NextChoice == null, "second Battlecry waits for first answer");
                bool complete = repetition == 1;
                Resume(g, next => Choose(next, offers, complete: complete));
            }
            Core2026Proof.Check(p.PlayHistory.Count == played + 1 && p.NumMinionsPlayedThisTurn == 1,
                "repeated Battlecry finishes outer play once");
        }
    }

}
