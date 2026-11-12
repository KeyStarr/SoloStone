using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class TemporaryRulesProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);
    internal static IPlayable Grant(Controller p, string id = "CS2_182")
    {
        var card = Entity.FromCard(p, Cards.FromId(id)); TemporaryRules.Make(card); Generic.AddHandPhase(p, card); return card;
    }
    internal static void End(Game g) => Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "end accepted");
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            var card = Grant(p);
            int packets = g.PowerHistory?.Last.Count ?? 0;
            TemporaryRules.Make(card);
            Check(TemporaryRules.IsTemporary(card) && card.AppliedEnchantments.Count(e => e.Card.Id == "GBL_999e") == 1
                && (g.PowerHistory?.Last.Count ?? 0) == packets, "Unpinned_TemporaryProjection idempotent mark/helper");
            foreach (var resumed in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                End(resumed); var expired = resumed.IdEntityDic[card.Id];
                Check(expired.Zone == resumed.Player1.GraveyardZone && resumed.Player1.DiscardedEntities.Contains(card.Id)
                    && resumed.Player1.LastCardDiscarded == card.Id && !TemporaryRules.IsTemporary(expired), "Unpinned_TemporaryExpiry clone/restore real discard");
            }
            Check(card.Zone == p.HandZone && TemporaryRules.IsTemporary(card), "clone isolation");
            var copy = Generic.Copy(p, p.Hero, card, Zone.HAND);
            Check(TemporaryRules.IsTemporary(copy) && copy.AppliedEnchantments.Single().Id != card.AppliedEnchantments.Single().Id, "Unpinned_TemporaryResidence same-zone copy retains independent mark");
            var fresh = Generic.DrawCard(p, card.Card); Check(!TemporaryRules.IsTemporary(fresh), "definition-only grant fresh");
            var transformed = Generic.ChangeEntityBlock(p, card, Cards.FromId("CS2_120"), true);
            Check(TemporaryRules.IsTemporary(transformed) && transformed.AppliedEnchantments.Single().Card.Id == "GBL_999e", "same residence transform retains one helper");
            var discards = new List<int>(); TriggerManager.TriggerHandler discard = e => discards.Add(e.Id);
            g.TriggerManager.DiscardTrigger += discard; End(g); g.TriggerManager.DiscardTrigger -= discard;
            Check(discards.SequenceEqual(new[] { card.Id, copy.Id }) && !TemporaryRules.IsTemporary(card), "real discard events left-to-right");
            var otherTurn = Grant(p); End(g); Check(otherTurn.Zone == p.HandZone, "opponent receipt survives opponent end");
            End(g); Check(otherTurn.Zone == p.GraveyardZone, "expires on next owner end");
            MixedCardStates(history); ExpiryCohort(history); Costs(history); BurnAndPlay(history); CrossType(history); HandTransfer(history);
        }
        Console.WriteLine("PASS TemporaryRules: Unpinned_TemporaryExpiry/Residence/EndOrder/Projection/SpelunkerNext; real discard/generations/clone/restore/cost commit");
    }
    private static void MixedCardStates(bool history)
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        var held = Core2026Proof.Add(p, "JAIL_801"); var temporary = Grant(p);
        Core2026Proof.Play(g, p, "GAME_005");
        int atDiscard = -1;
        TriggerManager.TriggerHandler discard = e => {
            if (e.Id == temporary.Id) atDiscard = held[GameTag.TAG_SCRIPT_DATA_NUM_1];
        };
        g.TriggerManager.DiscardTrigger += discard;
        End(g); g.TriggerManager.DiscardTrigger -= discard;
        Check(atDiscard == 2 && held.Zone == p.HandZone && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2
            && temporary.Zone == p.GraveyardZone, "Unpinned_TemporaryEndOrder Molten Gold cast progress survives same-turn Temporary discard");

        // Exercise the held-end primitive without registering Reforestation.
        var definition = Cards.FromId("JAIL_801"); var original = definition.HandProgress;
        try
        {
            definition.HandProgress = new HandProgressDefinition("JAIL_801t", 3, HandProgressMode.OwnerTurnEnds);
            g = HeraldProof.New(CardClass.SHAMAN); p = g.Player1;
            var marked = Grant(p, "JAIL_801"); marked[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            string discardedForm = null;
            discard = e => { if (e.Id == marked.Id) discardedForm = e.Card.Id; };
            g.TriggerManager.DiscardTrigger += discard;
            End(g); g.TriggerManager.DiscardTrigger -= discard;
            var expired = g.IdEntityDic[marked.Id];
            Check(discardedForm == "JAIL_801t" && expired.Zone == p.GraveyardZone && !TemporaryRules.IsTemporary(expired)
                && p.DiscardedEntities.Count(id => id == marked.Id) == 1,
                "Unpinned_TemporaryEndOrder Unpinned_ReforestationEnd held-end upgrade precedes Temporary expiry across CLR replacement");
        }
        finally { definition.HandProgress = original; }

        g = HeraldProof.New(CardClass.MAGE); p = g.Player1;
        Core2026Proof.Add(p, "CS2_182"); Core2026Proof.Add(p, "CATA_489");
        var half = p.HandZone.Single(c => c.Card.Id == "CATA_489t");
        var other = p.HandZone.Single(c => c.Card.Id == "CATA_489t2");
        TemporaryRules.Make(half); End(g);
        Check(half.Zone == p.GraveyardZone && p.DiscardedEntities.Count(id => id == half.Id) == 1
            && other.Zone == p.HandZone && other.Card.Id == "CATA_489t2" && !TemporaryRules.IsTemporary(other),
            "Unpinned_TemporaryEndOrder Unpinned_ShatterPermanent marked half discards alone at own end");

        g = HeraldProof.New(CardClass.MAGE); p = g.Player1;
        Core2026Proof.Add(p, "CS2_182"); Minion sleeper = null;
        TriggerManager.TriggerHandler split = _ => {
            sleeper = (Minion)Entity.FromCard(p, Cards.FromId("TIME_046"));
            Generic.SummonBlock(g, sleeper, -1, p.Hero);
        };
        g.TriggerManager.ShatterTrigger += split;
        Core2026Proof.Add(p, "CATA_489"); g.TriggerManager.ShatterTrigger -= split;
        Check(sleeper != null && sleeper.Zone == p.BoardZone && sleeper.IsDormant
            && sleeper[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && sleeper.AppliedEnchantments.Single().Card.Id == "TIME_046e"
            && p.HandZone.Count(c => c.Card.Shatter != null) == 2,
            "Unpinned_DormantClock Unpinned_ShatterEvent split-listener summon initializes independent Dormant state");
        End(g);
        Check(sleeper.IsDormant && sleeper[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "Unpinned_DormantClock Shatter and own end do not tick new sleeper");
        End(g);
        Check(sleeper.IsDormant && sleeper[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "Unpinned_DormantClock next owner start ticks sleeper once");
        Console.WriteLine("PASS CardStateInteractions: Molten Gold progress + Temporary discard; held-end upgrade before expiry; Temporary Shatter half; Dormant summon during split; Unpinned_TemporaryEndOrder");
    }
    private static void HandTransfer(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        HeroDivineShieldProof.Summon(g, "JAIL_205");
        CostRules.AddNextTemporaryRule(p, 2);
        var stolen = Grant(p.Opponent); int helper = stolen.AppliedEnchantments.Single().Id;
        End(g);
        Check(stolen.Zone == p.HandZone && stolen.Controller == p && TemporaryRules.IsTemporary(stolen)
            && stolen.AppliedEnchantments.Single().Id == helper && stolen.AppliedEnchantments.Single().Controller == p
            && stolen.Cost == 2, "HandTransfer Unpinned_TemporaryResidence theft preserves mark/helper and rebinds cost/controller");
        CheckTransferFacts(g, stolen, p.Opponent.Id, p.Id, Zone.HAND, HistoryHandEntryKind.Other, "Rat Burglar");
        End(g);
        Check(stolen.Zone == p.HandZone && !p.Opponent.DiscardedEntities.Contains(stolen.Id), "former owner end cannot expire stolen card");
        foreach (var resumed in new[] { g, g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var card = resumed.IdEntityDic[stolen.Id]; End(resumed);
            Check(card.Zone == resumed.Player1.GraveyardZone && resumed.Player1.DiscardedEntities.Count(id => id == card.Id) == 1
                && !TemporaryRules.IsTemporary(card), "HandTransfer Unpinned_TemporaryExpiry recipient end discards exactly once after clone/restore");
        }
        g = HeroDivineShieldProof.NewGame(history); p = g.Player1;
        Core2026Proof.Play(g, p, "JAIL_206");
        var given = g.IdEntityDic[p.Choice.Choices.First()]; TemporaryRules.Make(given);
        helper = given.AppliedEnchantments.Single().Id;
        PlayerDecisionProof.Answer(g, given.Id);
        Check(given.Zone == p.Opponent.HandZone && TemporaryRules.IsTemporary(given)
            && given.AppliedEnchantments.Single().Id == helper && given.AppliedEnchantments.Single().Controller == p.Opponent,
            "HandTransfer gifting preserves exact helper and recipient");
        CheckTransferFacts(g, given, p.Id, p.Opponent.Id, Zone.HAND, HistoryHandEntryKind.Other, "Dark Bribe");
        End(g); Check(given.Zone == p.Opponent.HandZone, "gift survives giver end");
        End(g); Check(given.Zone == p.Opponent.GraveyardZone, "gift expires at recipient end");

        g = HeroDivineShieldProof.NewGame(history); p = g.Player1;
        var traded = Grant(p, "CORE_EX1_005"); var tradeHelper = traded.AppliedEnchantments.Single();
        CostRules.AddNextTemporaryRule(p, 2);
        Check(g.Process(TradeCardTask.Any(p, traded)) && !TemporaryRules.IsTemporary(traded)
            && traded.AppliedEnchantments.Count == 0 && tradeHelper[GameTag.ATTACHED] == 0 && traded.Zone == p.DeckZone,
            "HandTransfer actual Trade still clears mark/helper");
        Check(Grant(p).Cost == 2, "Trade does not consume next-Temporary discount");

        g = HeroDivineShieldProof.NewGame(history); p = g.Player1;
        HeroDivineShieldProof.Summon(g, "JAIL_205");
        while (!p.HandZone.IsFull) Generic.DrawCard(p, Cards.FromId("CS2_231"));
        var burned = Grant(p.Opponent); var burnHelper = burned.AppliedEnchantments.Single(); End(g);
        Check(burned.Zone == p.GraveyardZone && !TemporaryRules.IsTemporary(burned)
            && burned.AppliedEnchantments.Count == 0 && burnHelper[GameTag.ATTACHED] == 0
            && !p.DiscardedEntities.Contains(burned.Id), "HandTransfer overflow clears mark/helper without discard");

        g = HeroDivineShieldProof.NewGame(history); p = g.Player1;
        var foreign = p.Opponent.DeckZone[0]; var drawn = Generic.DrawFrom(p, p.Opponent, foreign);
        Check(drawn == foreign && drawn.Zone == p.HandZone && drawn.Controller == p, "HandTransfer cross-owner deck draw enters recipient hand");
        CheckTransferFacts(g, drawn, p.Opponent.Id, p.Id, Zone.DECK, HistoryHandEntryKind.Draw, "cross-owner draw");
        foreach (var resumed in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var resumedCard = resumed.IdEntityDic[drawn.Id];
            Check(resumedCard.Zone == resumed.Player1.HandZone && resumedCard.Controller == resumed.Player1,
                "HandTransfer cross-owner draw survives clone/restore");
            CheckTransferFacts(resumed, resumedCard, resumed.Player2.Id, resumed.Player1.Id,
                Zone.DECK, HistoryHandEntryKind.Draw, "restored cross-owner draw");
        }
        Console.WriteLine("PASS HandTransfer: Rat Burglar/Dark Bribe; recipient expiry; clone/restore; actual Trade/overflow clear");
    }

    private static void CheckTransferFacts(Game game, IPlayable card, int fromController, int toController,
        Zone fromZone, HistoryHandEntryKind entryKind, string label)
    {
        var transfer = game.HistoryFacts.Query.Transfers().Single(f => f.EntityId == card.Id);
        var entered = game.HistoryFacts.Query.HandEntries().Single(f => f.Sequence + 1 == transfer.Sequence);
        Check(transfer.TransferId > 0 && game.HistoryFacts.Query.Transfers().Select(f => f.TransferId).Distinct().Count()
                == game.HistoryFacts.Query.Transfers().Count()
            && transfer.FromControllerId == fromController && transfer.ControllerId == toController && transfer.FromZone == fromZone
            && entered.EntityId == card.Id && entered.ControllerId == toController && entered.FromZone == fromZone
            && entered.EntryKind == entryKind,
            "HandTransfer " + label + " records adjacent matching hand-entry/transfer facts with a unique transfer id");
    }
    private static void ExpiryCohort(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var first = Grant(p); var second = Grant(p); IPlayable created = null;
        TriggerManager.TriggerHandler end = e => { if (e == p && created == null) created = Grant(p); };
        TriggerManager.TriggerHandler discard = e => {
            if (e.Id != first.Id) return;
            p.SetasideZone.Add(p.HandZone.Remove(second));
            Generic.AddHandPhase(p, second); TemporaryRules.Make(second);
        };
        g.TriggerManager.EndTurnTrigger += end; g.TriggerManager.DiscardTrigger += discard;
        End(g); g.TriggerManager.EndTurnTrigger -= end; g.TriggerManager.DiscardTrigger -= discard;
        Check(first.Zone == p.GraveyardZone && second.Zone == p.HandZone && created.Zone == p.HandZone,
            "Unpinned_TemporaryEndOrder end-created waits; exited/re-marked generation exempt");
        int count = p.DiscardedEntities.Count;
        TemporaryRules.CaptureControllerEnd(p); Check(p.DiscardedEntities.Count == count, "repeated boundary no duplicate expiry");
        End(g); End(g); Check(second.Zone == p.GraveyardZone && created.Zone == p.GraveyardZone, "deferred cards expire next owner end");
    }
    private static void Costs(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        var spelunker = Generic.DrawCard(p, Cards.FromId("TLC_450"));
        Check(g.Process(PlayCardTask.Any(p, spelunker)), "Spelunker Battlecry");
        var card = Grant(p); Check(card.Cost == 2, "Unpinned_SpelunkerNext pre-grant marker evaluated after admission");
        if (history) Check(g.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == card.Id && t.Tag == GameTag.COST && t.Value == 2), "discounted COST published after admission");
        CostRules.AddNextTemporaryRule(p, 2); Check(card.Cost == 0, "two rules stack and clamp");
        p.BaseMana = 0; var expensive = Grant(p, "CS2_200");
        var quote = CostRules.Quote(p, expensive); Check(!CostRules.CommitPayment(p, expensive, quote) && card.Cost == 0, "rejected payment retains both rules");
        foreach (var resumed in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var temp = resumed.IdEntityDic[card.Id];
            Check(temp.Cost == 0 && Generic.PayPhase(resumed.Player1, temp) && resumed.IdEntityDic[expensive.Id].Cost == expensive.Card.Cost,
                "cloned/restored discount consumed only by committed marked hand play");
        }
        End(g); End(g);
        card = Grant(p); Check(card.Cost == 0, "expiry did not consume persistent discounts");
        p.BaseMana = 10; p.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(p, card)) && !TemporaryRules.IsTemporary(card), "play clears mark");
        var next = Grant(p); Check(next.Cost == 4, "all matched next rules consumed on committed play");
    }
    private static void BurnAndPlay(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var card = Grant(p); p.BaseMana = 10;
        Check(g.Process(PlayCardTask.Any(p, card)) && card.Zone == p.BoardZone && !TemporaryRules.IsTemporary(card), "played permanent loses hand mark");
        int discarded = p.DiscardedEntities.Count;
        while (!p.HandZone.IsFull) Generic.DrawCard(p, Cards.FromId("CS2_231"));
        var burnt = Grant(p);
        Check(burnt.Zone == p.GraveyardZone && p.DiscardedEntities.Count == discarded, "overflow burns, no discard event");
        End(g); Check(card.Zone == p.BoardZone, "played minion not expired");
    }
    private static void CrossType(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var spell = Grant(p, "CS2_029"); int helper = spell.AppliedEnchantments.Single().Id;
        var minion = Generic.ChangeEntityBlock(p, spell, Cards.FromId("CS2_182"), true);
        Check(minion is Minion && TemporaryRules.IsTemporary(minion) && minion.AppliedEnchantments.Single().Id == helper
            && minion.AppliedEnchantments.Single().Target == minion && g.IdEntityDic[minion.Id] == minion,
            "Unpinned_TemporaryResidence cross-type replacement transfers exact helper and live target");
        End(g); Check(minion.Zone == p.GraveyardZone && p.DiscardedEntities.Contains(minion.Id), "transformed Temporary expires once");
    }

}
