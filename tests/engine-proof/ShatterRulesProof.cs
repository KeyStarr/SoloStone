using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class ShatterRulesProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, "Shatter: " + message);
    internal const string Leans = "Unpinned_ShatterEntry Unpinned_ShatterCapacity Unpinned_ShatterPosition Unpinned_ShatterJoin Unpinned_ShatterEnchantments Unpinned_ShatterPermanent Unpinned_ShatterEvent Unpinned_ShatterProjection";
    internal static void Run()
    {
        GeneratedParents(); CombinedCrystalTuskShuffle(); RedrawnHalfCostOrdering(); LayoutCapacity(); ReactionTakesLastSlot(); Unpinned_OneSlotBurn(); Opening(); DrawObservers(); SplitListener(); Modifiers(); CopiesAndOrphans(); QuestReplacement(); LegacyChangeHistory(); HistoryOffZoneTags();
        Console.WriteLine("PASS ShatterRules: " + Leans);
    }
    private static void GeneratedParents()
    {
        foreach (string id in new[] { "CATA_489", "CATA_134", "CATA_306", "CATA_479", "CATA_820" })
        {
            var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
            var installed = g.OfflinePools;
            var xml = OfflinePoolFixtures.Catalog();
            var rows = xml.Root.Element("Members");
            rows.ReplaceNodes(rows.Elements("Card").Single(r => (string)r.Attribute("cardId") == id));
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, OfflinePoolFixtures.Manifest(xml));
            var source = Summon(p, "CS2_231");
            var divider = Add(p, "CS2_182");
            var picked = OfflineCardPools.Pick(g, OfflinePoolFilter.Random(type: CardType.SPELL), p.HeroClass, source.Card.AssetId);
            Check(picked.Id == id && picked.Collectible, "admitted parent sampled, never a noncollectible half");
            // Same hand grant used by EffectContext.AddToHand after a random pool pick.
            new AddCardTo(picked.Id, EntityType.HAND).Process(g, p, source, null);
            var family = picked.Shatter;
            int parentId = p.HandZone[0][GameTag.DISPLAYED_CREATOR];
            Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(new[] { family.FirstHalfId, divider.Card.Id, family.SecondHalfId }) &&
                p.HandZone.Where(c => c.Card.Shatter != null).All(c => c[GameTag.DISPLAYED_CREATOR] == parentId) &&
                g.IdEntityDic[parentId].Card.Id == id && g.IdEntityDic[parentId][GameTag.DISPLAYED_CREATOR] == source.Id,
                "generated " + id + " splits through ShatterRules with creator and placement");
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, installed);
            foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var owner = next.Player1;
                owner.SetasideZone.Add(owner.HandZone.Remove(next.IdEntityDic[divider.Id]));
                Check(owner.HandZone.Single().Card.Id == id && owner.HandZone.Single()[ShatterRules.AlreadyCombined] == 1,
                    "generated " + id + " halves recombine after clone/restore");
            }
        }
        Console.WriteLine("PASS generated Shatter parents: five admitted parents split and recombine through normal hand grant, clone and snapshot");
    }
    private static void CombinedCrystalTuskShuffle()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var combined = ShatterRules.GiveCombined(p, "CATA_489", p.Hero);
        Play(g, p, "DINO_408");
        Check(combined.Zone == p.DeckZone, "Crystal Tusk actually shuffled the combined card");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var card = next.IdEntityDic[combined.Id];
            Generic.Draw(next.Player1, card);
            Check(card.Zone == next.Player1.HandZone && card[ShatterRules.AlreadyCombined] == 1 &&
                card[(GameTag)4239] == 0 && next.Player1.HandZone.Single().Id == combined.Id,
                "Crystal Tusk reset/redraw preserves permanent combined identity");
        }
        Console.WriteLine("PASS CombinedCrystalTuskShuffle: Unpinned_ShatterPermanent");
    }
    private static void RedrawnHalfCostOrdering()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var left = (Playable)Add(p, "CATA_489t");
        var divider = Add(p, "CS2_182");
        var right = Add(p, "CATA_489t2");
        new MoveToDeck(EntityType.TARGET).Process(g, p, p.Hero, right);
        Generic.Draw(p, right);
        Effects.SetCost(3).ApplyTo(left);
        new Effect(GameTag.COST, EffectOperator.MUL, 2).ApplyTo(right);
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var c = next.Player1;
            c.SetasideZone.Add(c.HandZone.Remove(next.IdEntityDic[divider.Id]));
            Check(c.HandZone.Single().Card.Id == "CATA_489" && c.HandZone.Single().Cost == 6,
                "redrawn half replays MUL after left SET, without scalar ADD fallback");
        }
        Console.WriteLine("PASS RedrawnHalfCostOrdering: Unpinned_ShatterEnchantments");
    }
    private static void LegacyChangeHistory()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var minion = Summon(p, "CS2_182");
        g.PowerHistory.Last.Clear();
        Generic.ChangeEntityBlock(p, minion, Cards.FromId("BOT_236"), false);
        var change = g.PowerHistory.Last.OfType<PowerHistoryChangeEntity>().Single();
        Check(!minion.HasTaunt && !change.Entity.Tags.ContainsKey(GameTag.TAUNT),
            "legacy CHANGE without explicit Taunt retains its pre-card-state history");
        string legacyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Canonical.Value(change))));
        // Pin only CHANGE: audited identical at 17f25307 and after the draw fix; opening SHOWs are unrelated.
        Check(legacyHash ==
            "4303ED687D79886AD17C3312C772C840B2D5F5B3517436E72A96E4491E712CAC",
            "legacy CHANGE history remains byte-identical to the pre-card-state path");
        Console.WriteLine("PASS LegacyChangeHistory: undeclared CHANGE history unchanged");
    }
    private static void LayoutCapacity()
    {
        foreach (int count in new[] { 0, 3, 8, 9, 10 })
        {
            var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
            for (int i = 0; i < count; i++) Add(p, "CS2_182");
            var events = new List<ShatterEvent>();
            g.TriggerManager.Events.Trace = (e, _, _) => { if (e is ShatterEvent shatter) events.Add(shatter); };
            // A real bus subscriber makes trace observable; external diagnostics are deliberately not cloned.
            g.TriggerManager.ShatterTrigger += _ => { };
            int discards = p.DiscardedEntities.Count;
            var parent = Add(p, "CATA_489");
            // Space is counted before the parent entered: 8 held fits both halves, 9 held admits only the left.
            int expected = count == 0 ? 1 : count <= 8 ? count + 2 : 10;
            Check(p.HandZone.Count == expected && events.Count == (count < 10 ? 1 : 0), "capacity/event count at " + count);
            if (count is > 0 and <= 8)
                Check(p.HandZone[0].Card.Id == "CATA_489t" && p.HandZone[count + 1].Card.Id == "CATA_489t2" &&
                    p.HandZone.Where(c => c.Card.Shatter != null).All(c => c.Id != parent.Id && c.Cost == 4), "opposite edges; new full-cost entities");
            if (count == 9) Check(p.HandZone[0].Card.Id == "CATA_489t" && events.Single().SecondHalfId != 0 && g.IdEntityDic[events.Single().SecondHalfId].Zone == p.GraveyardZone, "one slot admits left and burns created right");
            if (count == 0) Check(p.HandZone.Single()[ShatterRules.AlreadyCombined] == 1 && p.HandZone.Single()[(GameTag)4239] == 0, "split event precedes immediate join");
            if (count == 10) Check(parent.Zone == p.GraveyardZone, "burn is not successful entry");
            Check(p.DiscardedEntities.Count == discards, "split/join/burn never discards");
            var before = Canonical.State(g); var history = Canonical.History(g); var rng = g.Random.CaptureState();
            ShatterRules.AfterHandEntry(parent); ShatterRules.StabilizeHand(p);
            Check(Canonical.State(g) == before && Canonical.History(g) == history && g.Random.CaptureState().Equals(rng), "repeated/stale entry is no-op");
        }
        var game = HeraldProof.New(CardClass.MAGE); var owner = game.Player1;
        Add(owner, "CS2_182"); var middle = Entity.FromCard(owner, Cards.FromId("CATA_489"));
        // Ordinary zone observers see parent at its actual middle slot before split.
        bool seen = false;
        game.TriggerManager.ZoneTrigger += c => { if (c.Id == middle.Id) seen = c.Card.Id == "CATA_489" && ((IPlayable)c).ZonePosition == 1; };
        Add(owner, "CS2_231"); owner.HandZone.Add(middle, 1);
        Check(seen && owner.HandZone.Select(c => c.Card.Id).SequenceEqual(new[] { "CATA_489t", "CS2_182", "CS2_231", "CATA_489t2" }), "middle placement after entry observers");
    }
    // Owner 2026-11-03: 8 held gives both halves, 9 the left half only. Real Proto Pandaren (transport
    // standard-batch-1-37/40-vs-5 s9) reacts after the layout, so its spells cannot change that.
    private static void ReactionTakesLastSlot()
    {
        foreach (bool history in new[] { true, false })
        foreach (int held in new[] { 8, 9 })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            foreach (var old in p.HandZone.Concat(p.Opponent.HandZone).ToArray()) old.Controller.SetasideZone.Add(old.Zone.Remove(old));
            for (int i = 0; i < held; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.HandZone);
            Entity.FromCard(p, Cards.FromId("BE_997"), zone: p.BoardZone);
            int gifted = p.Opponent.HandZone.Count;
            var parent = Generic.DrawCard(p, Cards.FromId("CATA_489"));
            Check(parent.Zone != p.HandZone && p.HandZone.Count == 10 && p.HandZone[0].Card.Id == "CATA_489t", "reaction leaves the split admitted by the entry count");
            Check(held == 8 ? p.HandZone[held + 1].Card.Id == "CATA_489t2" && !p.GraveyardZone.Any(c => c.Card.Id == "CATA_489t2")
                : p.GraveyardZone.Any(c => c.Card.Id == "CATA_489t2") && !p.HandZone.Any(c => c.Card.Id == "CATA_489t2"),
                "both halves from 8 held, left only from 9 held, whatever the reaction generates");
            // The retired parent is not in hand when its event fires, so only admitted halves count as gained.
            Check(p.Opponent.HandZone.Count == gifted + (held == 8 ? 2 : 1), "Pandaren reacts once per admitted half");
        }
        ObserverFill(true); ObserverFill(false);
    }
    // Unpinned_ShatterObserverFill: a draw observer runs before the split (card-state.md 3.1) and can fill the hand.
    private static void ObserverFill(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        foreach (var old in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(old));
        for (int i = 0; i < 8; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.HandZone);
        bool filled = false;
        g.TriggerManager.DrawTrigger += _ => { if (!filled) { filled = true; Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.HandZone); } };
        // A real deck draw: Generic.DrawCard raises no draw observer.
        var parent = Entity.FromCard(p, Cards.FromId("CATA_489"), zone: p.DeckZone);
        Generic.Draw(p, parent);
        Check(filled && parent.Zone != p.HandZone && p.HandZone.Count == 10 && p.HandZone[0].Card.Id == "CATA_489t"
            && p.GraveyardZone.Any(c => c.Card.Id == "CATA_489t2") && !p.HandZone.Any(c => c.Card.Id == "CATA_489t2"),
            "an observer taking the last slot before the split admits the left half and burns the right");
    }
    private static void Unpinned_OneSlotBurn()
    {
        foreach (string mode in new[] { "overdraw", "draw", "generated" })
        {
            var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
            for (int i = 0; i < (mode == "overdraw" ? 10 : 9); i++) Add(p, "CS2_182");
            int draws = 0, deaths = 0, discards = 0;
            g.TriggerManager.DrawTrigger += _ => draws++;
            g.TriggerManager.DeathTrigger += _ => deaths++;
            g.TriggerManager.DiscardTrigger += _ => discards++;
            int discarded = p.DiscardedEntities.Count, deathFacts = g.HistoryFacts.Query.Deaths().Count();
            int drawn = p.NumCardsDrawnThisTurn;
            var source = Entity.FromCard(p, Cards.FromId(mode == "overdraw" ? "CATA_489t2" : "CATA_489"),
                zone: mode == "generated" ? null : p.DeckZone);
            g.PowerHistory.Last.Clear();
            if (mode == "generated") Generic.AddHandPhase(p, source); else Generic.Draw(p, source);
            var burned = p.GraveyardZone.Single(c => c.Card.Id == "CATA_489t2");
            Check(mode == "overdraw" || p.HandZone[0].Card.Id == "CATA_489t" &&
                p.HandZone.Count == 10 && source.Zone.Type == Zone.SETASIDE &&
                burned[GameTag.DISPLAYED_CREATOR] == source.Id, "Unpinned_OneSlotBurn: left retained, parent retired, right created: " + mode);
            Check(!g.HistoryFacts.Query.HandEntries().Any(f => f.EntityId == burned.Id) &&
                g.HistoryFacts.Query.Deaths().Count() == deathFacts && deaths == 0 && discards == 0 &&
                p.DiscardedEntities.Count == discarded && draws == (mode == "generated" ? 0 : 1) &&
                p.NumCardsDrawnThisTurn == drawn + (mode == "generated" ? 0 : 1),
                "Unpinned_OneSlotBurn: ordinary overdraw has no hand/death/discard facts; one DRAW per deck draw, none for generated halves: " + mode);
            g.PowerHistory.FlushBurnedCards();
            var history = g.PowerHistory.Last;
            Check(history.OfType<PowerHistoryMetaData>().Single(m => m.Type == MetaDataType.BURNED_CARD).Info.SequenceEqual(new[] { burned.Id }),
                "Shatter/ordinary overflow has one burned-card range: " + mode);
            int reveal = history.FindIndex(e => e is PowerHistoryShowEntity show && show.Entity.Id == burned.Id);
            Check(reveal >= 0 && history[reveal] is PowerHistoryShowEntity shown &&
                shown.Entity.Name == burned.Card.Id && shown.Entity.Tags[GameTag.ZONE] == (int)Zone.DECK &&
                history[reveal + 1] is PowerHistoryTagChange exit && exit.EntityId == burned.Id &&
                exit.Tag == GameTag.ZONE && exit.Value == (int)Zone.GRAVEYARD,
                "Unpinned_OneSlotBurn: ordinary overdraw reveal immediately precedes graveyard: " + mode);
        }
        Console.WriteLine("PASS Unpinned_OneSlotBurn: normal overdraw, drawn and generated Shatter");
    }
    private static void Opening()
    {
        foreach (string id in new[] { "CATA_489", "CATA_134", "CATA_306", "CATA_479", "CATA_820" })
        foreach (int first in new[] { 1, 2 })
        foreach (bool shuffle in new[] { false, true })
        foreach (string mode in new[] { "keep", "replace", "skip" })
        {
            // One parent among separators: all-parent decks can recombine and hide a failed split.
            var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
            deck[shuffle ? 29 : 0] = Cards.FromId(id);
            var g = new Game(new GameConfig { RandomSeed = 183, StartPlayer = first, Shuffle = shuffle,
                SkipMulligan = mode == "skip", History = true, Logging = false,
                Player1Deck = deck, Player2Deck = deck.ToList() });
            // Seed 183 deals the lone parent to both seats after the real shuffle.
            g.StartGame();
            var parents = new[] { g.Player1, g.Player2 }.SelectMany(p => p.HandZone).Where(c => c.Card.Id == id).ToArray();
            if (mode != "skip")
            {
                Check(parents.Length == 2 && parents.All(c => c[ShatterRules.AlreadyCombined] == 0), "ordinary parent mulligan offers");
                foreach (var owner in new[] { g.Player2, g.Player1 })
                {
                    var kept = owner.Choice.Choices.ToList();
                    if (mode == "replace")
                    {
                        // Replace a separator with an actual deck parent through the normal mulligan path.
                        var replacement = Entity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
                        owner.DeckZone.PutOnTop(replacement);
                        kept.Remove(owner.HandZone.First(c => c.Card.Shatter == null && c.Card.Id != "GAME_005").Id);
                    }
                    g.Process(ChooseTask.Mulligan(owner, kept));
                }
                Check(parents.All(c => c.Zone == c.Controller.HandZone), "no early split during either mulligan");
                g.MainBegin();
            }
            foreach (var owner in new[] { g.Player1, g.Player2 })
            {
                var family = Cards.FromId(id).Shatter;
                int expected = mode == "replace" ? 2 : 1;
                Check(owner.HandZone.Count(c => c.Card.Id == family.FirstHalfId) == expected &&
                    owner.HandZone.Count(c => c.Card.Id == family.SecondHalfId) == expected &&
                    !owner.HandZone.Any(c => c.Card.Id == id),
                    $"opening halves survive for {id}, first={first}, shuffle={shuffle}, mode={mode}, owner={owner.PlayerId}");
                Check(owner.HandZone.Count(c => c.Card.Id == "GAME_005") == (owner == g.FirstPlayer ? 0 : 1), "opening Coin retained");
            }
            Check(parents.Length == (mode == "skip" ? 0 : 2), "opening parent cohort");
        }
        Console.WriteLine("PASS Shatter opening: five families, both seats/start players, shuffle, kept-all, replacements and SkipMulligan");
    }
    private static void DrawObservers()
    {
        foreach (bool indexed in new[] { false, true })
        {
            var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
            Add(p, "CATA_489t");
            var half = Entity.FromCard(p, Cards.FromId("CATA_489t2"), zone: p.DeckZone);
            bool seen = false;
            g.TriggerManager.DrawTrigger += e => { if (e.Id == half.Id) seen = e.Zone == p.HandZone && e.Card.Id == "CATA_489t2"; };
            if (indexed) Generic.Draw(p, p.DeckZone.Count - 1); else Generic.Draw(p, half);
            Check(seen, "redrawn half survives OnDraw indexed=" + indexed);
            Check(p.HandZone.Single().Card.Id == "CATA_489" && p.HandZone.Single()[ShatterRules.AlreadyCombined] == 1, "committed removal joins either hand order");
        }
    }
    private static void Modifiers()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var separator = Add(p, "CS2_182");
        var source = (Playable)Entity.FromCard(p, Cards.FromId("CATA_489"));
        Effects.ReduceCost(1).ApplyTo(source); Generic.AddHandPhase(p, source);
        var left = (Playable)p.HandZone[0]; var right = (Playable)p.HandZone[2];
        Check(left.Cost == 3 && right.Cost == 3, "pre-split reduction copied once to each");
        var snapshot = Game.LoadSnapshot(g.SaveSnapshot()); var clone = g.CloneExact(history: true);
        foreach (var next in new[] { g, clone, snapshot })
        {
            var c = next.Player1; var divider = c.HandZone.Single(x => x.Id == separator.Id);
            c.SetasideZone.Add(c.HandZone.Remove(divider));
            Check(c.HandZone.Single().Cost == 2, "duplicated reductions stack on printed parent");
        }
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(snapshot), "half histories clone/load independently");
        g = HeraldProof.New(CardClass.MAGE); p = g.Player1;
        separator = Add(p, "CS2_182"); Add(p, "CATA_489");
        left = (Playable)p.HandZone[0]; right = (Playable)p.HandZone[2];
        Effects.SetCost(3).ApplyTo(left); new Effect(GameTag.COST, EffectOperator.MUL, 2).ApplyTo(right);
        CostRules.AddModifier(left, EffectOperator.SUB, 1, g.Turn);
        CostRules.AddModifier(right, EffectOperator.SUB, 2, g.Turn);
        CostRules.AddPaymentOverride(left, CostPaymentKind.Health, g.Turn);
        CostRules.AddPaymentOverride(right, CostPaymentKind.Armor, g.Turn);
        p.SetasideZone.Add(p.HandZone.Remove(separator));
        var combined = p.HandZone.Single();
        Check(combined.Cost == 3 && CostRules.PaymentKind(combined) == CostPaymentKind.Armor, "left SET / right MUL then independent overlays; last payment wins");
        CostRules.ExpireTurn(g, g.Turn);
        Check(combined.Cost == 6 && CostRules.PaymentKind(combined) == CostPaymentKind.Mana, "copied expiry preserved");
        // A continuous aura must be reapplied once, never become a copied permanent reduction.
        g = HeraldProof.New(CardClass.MAGE); p = g.Player1; Summon(p, "EX1_608");
        separator = Add(p, "CS2_182"); Add(p, "CATA_489"); g.DeathProcessingAndAuraUpdate();
        Check(p.HandZone.Where(c => c.Card.Shatter != null).All(c => c.Cost == 3), "hand aura applies once per half");
        p.SetasideZone.Add(p.HandZone.Remove(separator)); g.DeathProcessingAndAuraUpdate();
        Check(p.HandZone.Single().Cost == 3, "continuous aura not duplicated at join");
    }
    private static void SplitListener()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var listener = new SabberStoneCore.Triggers.Trigger(TriggerType.SHATTER) {
            TriggerSource = TriggerSource.FRIENDLY,
            SingleTask = new SabberStoneCore.Tasks.SimpleTasks.DamageTask(2, EntityType.OP_HERO)
        }.Activate(Summon(p, "CS2_231"));
        var separator = Add(p, "CS2_182"); Add(p, "CATA_489");
        Check(p.Opponent.Hero.Damage == 2, "ordinary card listener observes split once for both halves");
        p.SetasideZone.Add(p.HandZone.Remove(separator));
        Check(p.Opponent.Hero.Damage == 2, "join emits no split event");
        Add(p.Opponent, "CATA_489");
        Check(p.Opponent.Hero.Damage == 2, "friendly listener ignores opponent split");
        listener.Remove(); Add(p, "CATA_489");
        Check(p.Opponent.Hero.Damage == 2, "removed listener cannot observe later splits");
    }
    private static void CopiesAndOrphans()
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var parent = ShatterRules.GiveCombined(p, "CATA_489", p.Hero);
        var copy = Generic.Copy(p, p.Hero, parent, Zone.HAND);
        Check(copy[ShatterRules.AlreadyCombined] == 1 && p.HandZone.Count == 2, "combined copy stays permanent");
        p.DeckZone.Add(p.HandZone.Remove(copy)); Generic.Draw(p, copy);
        Check(copy[ShatterRules.AlreadyCombined] == 1 && p.HandZone.Count == 2, "combined shuffle/redraw stays permanent");
        Generic.ChangeEntityBlock(p, copy, Cards.FromId("GAME_005"), true);
        Generic.ChangeEntityBlock(p, copy, Cards.FromId("CATA_489"), true);
        Check(p.HandZone.Any(c => c.Card.Id == "CATA_489t"), "unrelated transform clears permanent marker");
        g = HeraldProof.New(CardClass.MAGE); p = g.Player1;
        var half = Add(p, "CATA_489t2"); var same = Generic.Copy(p, p.Hero, half, Zone.HAND);
        Check(p.HandZone.Count == 2, "identical halves do not join");
        Generic.DiscardBlock(p, half);
        Check(same.Zone == p.HandZone && same.Card.Id == "CATA_489t2", "discard removes only selected half");
        Add(p, "CATA_489t");
        Check(p.HandZone.Single().Card.Id == "CATA_489", "reverse-order unrelated complementary halves join");
    }
    private static void QuestReplacement() => QuestLifecycleProof.WithSyntheticQuest(() =>
    {
        var g = HeraldProof.New(CardClass.MAGE); var p = g.Player1;
        var quest = (Spell)Play(g, p, QuestLifecycleProof.SyntheticQuestId);
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        QuestRules.Advance(quest, 2);
        Check(p.PendingQuestRewards.Count == 1, "reward waits for genuine free slot");
        // Replace two adjacent halves in a full hand: only the committed join releases one reward slot.
        Generic.ChangeEntityBlock(p, p.HandZone[0], Cards.FromId("CATA_489t"), false);
        Generic.ChangeEntityBlock(p, p.HandZone[1], Cards.FromId("CATA_489t2"), false);
        Check(p.HandZone.Count == 10 && p.PendingQuestRewards.Count == 0 &&
            p.HandZone.Count(c => c[GameTag.DISPLAYED_CREATOR] == quest.Id) == 1 &&
            p.HandZone.Count(c => c.Card.Id == "CATA_489") == 1, "reward delivered after coherent join, no stolen reserved slot");
    });
    // FX staging allocates replacements in SETASIDE only when History is on; History-off games must keep ZONE=HAND.
    private static void HistoryOffZoneTags()
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var g = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = false, Logging = false, Player1Deck = deck, Player2Deck = deck.ToList() });
        g.StartGame();
        var p = g.Player1;
        // Split: draw a parent into a hand with separators so the halves stay apart.
        p.DeckZone.PutOnTop(Entity.FromCard(p, Cards.FromId("CATA_489"), zone: p.DeckZone));
        Generic.Draw(p);
        Check(p.HandZone.Any(c => c.Card.Id == "CATA_489t") && p.HandZone.Any(c => c.Card.Id == "CATA_489t2"), "History-off split happened");
        // Combine: a granted already-combined parent.
        var granted = ShatterRules.GiveCombined(p, "CATA_489", p.Hero);
        Check(granted != null && granted.Zone == p.HandZone, "History-off grant entered hand");
        // History-off zones do not maintain ZONE for ordinary entries; Shatter allocations keep their pre-FX HAND tag.
        foreach (var card in p.HandZone.Where(c => c.Card.Shatter != null))
            Check(card[GameTag.ZONE] == (int)Zone.HAND, "History-off Shatter card keeps ZONE=HAND: " + card.Card.Id);
    }
}
