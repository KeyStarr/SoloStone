using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

internal static class DarkGiftBehaviourProof
{
    private static void Check(bool value, string label)
    { if (!value) throw new Exception("dark gift behaviour: " + label); }
    private static Game New(bool history = true) {
        var g = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = history, Logging = false, Player1HeroClass = CardClass.MAGE,
            Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        g.StartGame(); g.Player1.BaseMana = 10; return g;
    }
    private static Minion Minion(Game g, string id = "CS2_182", Zone zone = Zone.HAND) =>
        (Minion)Entity.FromCard(g.Player1, Cards.FromId(id), zone: zone == Zone.HAND ? g.Player1.HandZone :
            zone == Zone.PLAY ? g.Player1.BoardZone : zone == Zone.DECK ? g.Player1.DeckZone : g.Player1.SetasideZone);
    private static void Buff(Game g, IPlayable m, string enchantment) =>
        Generic.AddEnchantmentBlock(g, Cards.FromId(enchantment), g.Player1.Hero, m);
    private static void Play(Game g, IPlayable m, int position = -1) =>
        Check(g.Process(PlayCardTask.Any(g.Player1, m, zonePosition: position)), "play " + m.Card.Id);
    private static DecisionAnswer Answer(Game g, int index = 0) {
        var d = g.GetPendingDecisions().Single();
        return new(d.Id, d.ControllerId, new[] { d.Options[index].OptionId });
    }
    private static void Accept(Game g, DecisionAnswer answer) => Check(g.TryAnswer(answer, out var error), error);
    private static void Kill(Game g, Minion m) { m.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate(); }

    private static void Futures(Game g, Action<Game> advance) {
        if (!g.Resolution.HasSuspendedEffect) g.DeathProcessingAndAuraUpdate();
        var bytes = g.SaveSnapshot(); var loaded = Game.LoadSnapshot(bytes); var clone = g.CloneExact(history: g.History);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "snapshot-v2 byte round trip");
        int history = g.History ? g.PowerHistory.Full.Count : 0;
        foreach (var candidate in new[] { g, loaded, clone }) {
            advance(candidate);
            if (!candidate.Resolution.HasSuspendedEffect) candidate.DeathProcessingAndAuraUpdate();
            var after = candidate.SaveSnapshot();
            Check(after.SequenceEqual(Game.LoadSnapshot(after).SaveSnapshot()), "result snapshot-v2 byte round trip");
        }
        Check(Canonical.State(g) == Canonical.State(loaded) && Canonical.State(g) == Canonical.State(clone), "clone/snapshot future state");
        Check(g.Random.CaptureState().Equals(loaded.Random.CaptureState()) && g.Random.CaptureState().Equals(clone.Random.CaptureState()), "future RNG parity");
        if (g.History) Check(Canonical.History(g) == Canonical.History(loaded) &&
            Canonical.Value(g.PowerHistory.Full.Skip(history)) == Canonical.Value(clone.PowerHistory.Full), "future history parity");
    }
    private static void ChooseSweet(Game g, IPlayable m, ChoiceAction action = ChoiceAction.HAND) {
        var context = new EffectContext(g, g.Player1.Id, g.Player1.Hero.Id);
        Check(context.DiscoverEntitiesWithDarkGift(new[] { m }, action), "open gifted choice");
        var spell = Entity.FromCard(g.Player1, Cards.FromId("EDR_100t8"),
            new EntityData { [GameTag.CREATOR] = g.Player1.Choice.SourceId }, g.Player1.SetasideZone);
        m[GameTag.DARK_GIFT_ENTITY] = spell.Id;
    }
    // Lean dg12b-living-nightmare-copy: full copy, to the right, before Battlecry.
    private static void LivingNightmare() {
        foreach (bool history in new[] { false, true }) {
            var g = New(history); var m = Minion(g);
            Buff(g, m, "EDR_100t5e"); Buff(g, m, "EDR_100te"); Buff(g, m, "EDR_100t13e");
            Futures(g, game => {
                var original = game.IdEntityDic[m.Id]; Play(game, original);
                Check(game.Player1.BoardZone.Count == 2, "Living Nightmare survives hand removal and fires once");
                var copy = game.Player1.BoardZone[1];
                Check(copy != original && copy.AttackDamage == 2 && copy.Health == 2 && copy.HasLifeSteal && copy.HasDivineShield && copy.HasWindfury &&
                    DarkGifts.GiftsOf(copy).Select(e => e.Card.Id).SequenceEqual(new[] { "EDR_100t5e", "EDR_100te", "EDR_100t13e" }), "2/2 full gifted copy");
                if (history) Check(copy.AppliedEnchantments.Any(e => e.Card.Id == "EDR_100t5e5"), "2/2 attachment 120020");
                int count = game.Player1.BoardZone.Count;
                var extra = Generic.Copy(game.Player1, original, original, Zone.PLAY);
                game.DeathProcessingAndAuraUpdate();
                Check(game.Player1.BoardZone.Count == count + 1 && DarkGifts.Has(extra), "copy summons never play Living Nightmare");
            });
        }
    }
    private static void LivingNightmareBoundaries() {
        {
            var g = New(); var m = Minion(g); Buff(g, m, "EDR_100t5e");
            g.Player1.HandZone.Remove(m); Generic.SummonBlock(g, m, -1, g.Player1.Hero); g.DeathProcessingAndAuraUpdate();
            Check(g.Player1.BoardZone.Count == 1, "recruited/summoned from hand never plays");
        }
        {
            var g = New(); var m = Minion(g, zone: Zone.SETASIDE); Buff(g, m, "EDR_100t5e");
            Check(g.Process(PlayCardTask.Any(g.Player1, m, skipPrePhase: true)), "forced non-hand play fixture");
            Check(g.Player1.BoardZone.Count == 1, "Living Nightmare requires played from hand even on forced play");
        }
        {
            var g = New(); var left = (BoardObject)Entity.FromCard(g.Player1, Cards.FromId("CORE_REV_990"), zone: g.Player1.BoardZone);
            var right = Minion(g, zone: Zone.PLAY); var m = Minion(g);
            Buff(g, m, "EDR_100t5e"); Buff(g, m, "EDR_100t5e");
            Play(g, m, 1);
            var board = g.Player1.BoardZone;
            Check(board.SlotCount == 5 && board.GetSlot(0) == left && board.GetSlot(1) == m && board.GetSlot(4) == right &&
                board.GetSlot(2).Id < board.GetSlot(3).Id && ((Minion)board.GetSlot(2)).Health == 2 &&
                DarkGifts.GiftsOf(board.GetSlot(3)).Count == 2, "two attachments, physical right positions and creation order");
        }
        foreach (int filled in new[] { 5, 6 }) {
            var g = New(); Entity.FromCard(g.Player1, Cards.FromId("CORE_REV_990"), zone: g.Player1.BoardZone);
            for (int i = 1; i < filled; i++) Minion(g, zone: Zone.PLAY);
            var m = Minion(g); Buff(g, m, "EDR_100t5e"); Buff(g, m, "EDR_100t5e");
            int entities = g.IdEntityDic.Count; Play(g, m);
            Check(g.Player1.BoardZone.SlotCount == 7 && g.IdEntityDic.Count == entities + (filled == 5 ? 4 : 0),
                "physical full/one-free slot skips copies and their allocations");
        }
    }
    private static void LivingNightmareBeforeBattlecry() {
        var g = New(); var neighbor = Minion(g, zone: Zone.PLAY); var m = Minion(g, "EX1_093");
        Buff(g, m, "EDR_100t5e"); Play(g, m, 0);
        var copy = g.Player1.BoardZone[1];
        Check(copy.Card.Id == "EX1_093" && copy.AttackDamage == 3 && copy.Health == 3 && copy.HasTaunt &&
            neighbor.AttackDamage == 4 && !neighbor.HasTaunt, "Argus Battlecry observes the 2/2 copy before original right neighbor");
    }
    // Lean dg13b-persisting-horror-consumption: full health, exclude gift and consume Reborn.
    private static void PersistingHorror() {
        foreach (bool history in new[] { false, true }) {
            var g = New(history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, "CS2_009e"); // Non-gift +2/+2 may have no attachment with history off.
            Buff(g, m, "EDR_100t9e"); Buff(g, m, "EDR_100t9e");
            Buff(g, m, "EDR_100t3e"); Buff(g, m, "EDR_100te"); Buff(g, m, "EDR_100t5e");
            var oldIds = DarkGifts.GiftsOf(m).Select(e => e.Id).ToHashSet();
            m.Damage = 2;
            Futures(g, game => {
                Kill(game, (Minion)game.IdEntityDic[m.Id]);
                var reborn = game.Player1.BoardZone.Single();
                Check(reborn.Health == 11 && reborn.AttackDamage == 9 && reborn.HasTaunt && reborn.HasLifeSteal &&
                    reborn[GameTag.REBORN] == 0 && reborn[GameTag.HAS_BEEN_REBORN] == 1 &&
                    DarkGifts.GiftsOf(reborn).Count == 3 && DarkGifts.GiftsOf(reborn).All(e => !oldIds.Contains(e.Id) && e.Card.Id != "EDR_100t9e"),
                    "full enchanted rebirth, fresh attachments, duplicate Horror excluded, Living Nightmare not played");
                if (history) Check(reborn.AppliedEnchantments.Any(e => e.Card.Id == "CS2_009e"), "non-gift enchantment retained");
                Kill(game, reborn); Check(game.Player1.BoardZone.Count == 0, "second death stays dead");
            });
        }
    }
    private static void PersistingHorrorRestoresCost() {
        foreach (bool history in new[] { false, true }) {
            var g = New(history); var m = Minion(g);
            Buff(g, m, "EDR_100t2e"); Buff(g, m, "EDR_100t9e");
            Check(m.Cost == 2, "Short Claws discounts hand minion");
            Play(g, m);
            Check(m.Cost == 4, "hand exit resets board cost before rebirth capture");
            Futures(g, game => {
                Kill(game, (Minion)game.IdEntityDic[m.Id]);
                var reborn = game.Player1.BoardZone.Single();
                Check(reborn.Cost == 4 && reborn[GameTag.HAS_BEEN_REBORN] == 1 &&
                    DarkGifts.GiftsOf(reborn).Select(e => e.Card.Id).SequenceEqual(new[] { "EDR_100t2e" }),
                    "rebirth restores saved cost after reapplying cost attachment with History=" + history);
                Futures(game, restored => Check(restored.IdEntityDic[reborn.Id].Cost == 4,
                    "restored rebirth cost survives clone/snapshot"));
            });
        }
    }
    private static void PersistingHorrorReappliesKeywords() {
        foreach (bool history in new[] { false, true }) {
            var g = New(history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, "EDR_100t9e"); Buff(g, m, "EDR_100t13e");
            m.TakeDamage(g.Player2.Hero, 1); Check(!m.HasDivineShield, "shield consumed before death");
            Kill(g, m); var reborn = g.Player1.BoardZone.Single();
            Check(reborn.HasDivineShield && reborn.HasWindfury && reborn[GameTag.REBORN] == 0, "rebirth reapplies enchantment-granted shield");
        }
    }
    private static void PersistingHorrorAdditiveEnchantment() {
        foreach (bool history in new[] { true, false }) {
            var g = New(history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, "EDR_100t9e"); Buff(g, m, "EX1_584e");
            Check(m.SpellPower == 1 && g.Player1.CurrentSpellPower == 1, "additive Spell Damage fixture");
            Futures(g, game => {
                Kill(game, (Minion)game.IdEntityDic[m.Id]);
                var reborn = game.Player1.BoardZone.Single();
                Check(reborn.SpellPower == 1 && game.Player1.CurrentSpellPower == 1,
                    "rebirth retains additive Spell Damage exactly once with History=" + history + ": " + reborn.SpellPower);
            });
        }
    }
    private static void PersistingHorrorTemporaryBuff() {
        foreach (bool history in new[] { false, true }) {
            var g = New(history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, "EDR_100t9e"); Buff(g, m, "CS2_011o");
            Kill(g, m); var reborn = g.Player1.BoardZone.Single();
            Check(reborn.AttackDamage == 6, "rebirth retains this-turn enchantment");
            Futures(g, game => {
                var target = (Minion)game.IdEntityDic[reborn.Id];
                Check(game.Process(EndTurnTask.Any(game.Player1)), "end turn after rebirth");
                Check(target.AttackDamage == 4, "retained temporary enchantment still expires on reborn minion");
            });
        }
    }
    private static void PersistingHorrorBoundaries() {
        foreach (bool history in new[] { false, true }) {
            {
                var g = New(history); var m = Minion(g, zone: Zone.PLAY); Buff(g, m, "EDR_100t9e"); m.Silence();
                Kill(g, m); Check(g.Player1.BoardZone.Count == 0, "silence prevents gift Reborn");
            }
            {
                var g = New(history); var m = Minion(g, "ULD_214", Zone.PLAY);
                Buff(g, m, "EDR_100t3e"); Kill(g, m); var reborn = g.Player1.BoardZone.Single();
                Check(reborn.Health == 1 && reborn.BaseHealth == reborn.Card.Health && !DarkGifts.Has(reborn), "ordinary printed Reborn unchanged");
            }
            {
                var g = New(history); var m = Minion(g, zone: Zone.PLAY); Buff(g, m, "EDR_100t9e"); Buff(g, m, "EDR_100t3e");
                var location = (BoardObject)Entity.FromCard(g.Player1, Cards.FromId("CORE_REV_990"), zone: g.Player1.BoardZone);
                m.ToBeDestroyed = true; location.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
                Check(g.Player1.BoardZone.Single().Health == 9 && g.Player1.BoardZone.SlotCount == 1, "mixed location/minion death captures Horror");
            }
        }
    }
    // Lean dg14b-rude-awakening-brann: Boolean doubling, never multiplication.
    private static void RudeAwakening() {
        foreach (bool history in new[] { false, true })
        foreach (bool brann in new[] { false, true }) {
            var g = New(history); if (brann) Minion(g, "LOE_077", Zone.PLAY);
            var m = Minion(g, "EX1_015"); Buff(g, m, "EDR_100t7e"); Buff(g, m, "EDR_100t7e");
            int draws = g.Player1.NumCardsDrawnThisTurn;
            Futures(g, game => { Play(game, game.IdEntityDic[m.Id]);
                Check(game.Player1.NumCardsDrawnThisTurn == draws + 2 && game.Player1.NumMinionsPlayedThisTurn == 1,
                    "ordinary Battlecry twice despite duplicates/Brann=" + brann); });
        }
        {
            var g = New(); var m = Minion(g); Buff(g, m, "EDR_100t7e"); Play(g, m);
            Check(g.Player1.BoardZone.Count == 1 && g.Player1.NumMinionsPlayedThisTurn == 1, "non-Battlecry gift has no effect");
        }
    }
    private static void RudeAwakeningCombo() {
        // Synthetic Battlecry+Combo fixture verifies the selected branch, not a new gift eligibility rule.
        var card = Cards.FromId("EX1_131"); var original = card.Power;
        try {
            card.Power = new Power { PowerTask = new ArmorTask(10), ComboTask = new ArmorTask(1) };
            foreach (bool brann in new[] { false, true }) {
                var g = New(); if (brann) Minion(g, "LOE_077", Zone.PLAY);
                var m = Minion(g, card.Id); m.HasBattleCry = true; Buff(g, m, "EDR_100t7e"); g.Player1.IsComboActive = true;
                Play(g, m); Check(g.Player1.Hero.Armor == 2, "Rude Awakening repeats the active Combo twice with Brann=" + brann);
            }
        } finally { card.Power = original; }
    }
    private static void RudeAwakeningChooseOne() {
        var g = New(); var m = Minion(g, "EX1_178"); m.HasBattleCry = true; Buff(g, m, "EDR_100t7e");
        Check(g.Process(PlayCardTask.Minion(g.Player1, m, chooseOne: 2)), "play selected Choose One branch");
        Check(m.AttackDamage == 15 && m.Health == 5 && !m.HasTaunt, "Rude Awakening repeats same Choose One branch: " + m.AttackDamage + "/" + m.Health + "/" + m.HasTaunt + "/" + string.Join(",", m.ChooseOnePlayables.Select(p => p.Card.Id)));
    }
    private static readonly DecisionBattlecryTask SelectRecipe = new("proof/dark-gift/select-twice/v1",
        new SelectEntityThenContinueTask(), EffectContext.Task(ctx => ctx.RunLegacy(
            new SetGameTagTask(GameTag.TAG_SCRIPT_DATA_NUM_1, 17, EntityType.TARGET), ctx.LastChosenEntityId)), new ArmorTask(1));
    private static readonly DecisionBattlecryTask DiscoverRecipe = new("proof/dark-gift/discover-twice/v1",
        new DiscoverThenContinueTask("UNG_999t6", "UNG_999t13"), new CustomTask((g, c, s, t, stack) => {
            var chosen = stack.Playables.Single();
            Generic.RemoveFromZone(c, chosen); Generic.AddHandPhase(c, chosen); c.Hero.Armor++;
        }));
    private static void RudeAwakeningDecisions() {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try {
            foreach (var recipe in new[] { SelectRecipe, DiscoverRecipe }) {
                card.Power = new Power { PowerTask = recipe };
                foreach (bool history in new[] { false, true })
                foreach (bool brann in new[] { false, true }) {
                    var g = New(history); if (brann) Minion(g, "LOE_077", Zone.PLAY);
                    var m = Minion(g, card.Id); m.HasBattleCry = true; Buff(g, m, "EDR_100t7e");
                    Play(g, m); Check(g.Resolution.HasSuspendedEffect && g.Player1.Hero.Armor == 0, "first Battlecry waits for answer");
                    Futures(g, first => {
                        var answer = Answer(first, 0); int selected = answer.SelectedOptionIds.Single(); Accept(first, answer);
                        Check(first.Resolution.HasSuspendedEffect && first.Player1.Choice != null && first.Player1.Hero.Armor == 1, "first answer opens a separate second decision");
                        if (recipe == SelectRecipe) Check(first.IdEntityDic[selected][GameTag.TAG_SCRIPT_DATA_NUM_1] == 17, "first selection answer honored");
                        else Check(first.IdEntityDic[selected].Zone == first.Player1.HandZone, "first Discover answer honored");
                        var before = first.SaveSnapshot(); Check(!first.TryAnswer(answer, out _) && before.SequenceEqual(first.SaveSnapshot()), "first answer becomes stale");
                        Futures(first, second => {
                            var next = Answer(second, 1); int id = next.SelectedOptionIds.Single(); Accept(second, next);
                            Check(!second.Resolution.HasSuspendedEffect && second.Player1.Choice == null && second.Player1.Hero.Armor == 2 &&
                                second.Player1.NumMinionsPlayedThisTurn == 1 && second.Player1.PlayHistory.Count == 1, "second answer finishes exactly twice and one outer play");
                            if (recipe == SelectRecipe) Check(second.IdEntityDic[id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 17, "second selection answer honored");
                            else Check(second.IdEntityDic[id].Zone == second.Player1.HandZone, "second Discover answer honored");
                        });
                    });
                }
            }
        } finally { card.Power = original; }
    }
    // Lean dg11b-sweet-dreams-sequence: chosen hand entry precedes deck-top relocation.
    private static void SweetDreams() {
        foreach (bool history in new[] { false, true })
        foreach (bool deck in new[] { false, true }) {
            var g = New(history); var m = Minion(g, zone: deck ? Zone.DECK : Zone.SETASIDE);
            Buff(g, m, "EDR_100te"); Buff(g, m, "CS2_009e"); ChooseSweet(g, m);
            int[] remaining = g.Player1.DeckZone.Where(p => p != m).Select(p => p.Id).ToArray();
            int drawn = g.Player1.NumCardsDrawnThisTurn;
            Futures(g, game => {
                var target = (Minion)game.IdEntityDic[m.Id]; var rng = game.Random.CaptureState();
                Accept(game, Answer(game));
                Check(game.Player1.DeckZone.TopCard == target && target.Zone == game.Player1.DeckZone &&
                    target.AttackDamage == 10 && target.BaseHealth == 12 && !target.HasLifeSteal &&
                    DarkGifts.GiftsOf(target).Single().Card.Id == "EDR_100t8e1", "Sweet Dreams keeps +4/+5, removes other gift per D6, preserves existing ordinary buff");
                Check(game.Player1.DeckZone.Where(p => p != target).Select(p => p.Id).SequenceEqual(remaining) &&
                    game.Random.CaptureState().Equals(rng), "top insertion preserves deck order without RNG");
                Check(game.Player1.NumCardsDrawnThisTurn == drawn + (deck ? 1 : 0) && (!deck || game.Player1.LastCardDrawn == target.Id), "Xavius draws actual chosen deck entity");
                if (history) {
                    var changes = game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                        .Where(p => p.EntityId == target.Id && p.Tag == GameTag.ZONE).Select(p => p.Value).ToArray();
                    Check(changes.TakeLast(2).SequenceEqual(new[] { (int)Zone.HAND, (int)Zone.DECK }), "hand-first wire zone order");
                }
                Generic.Draw(game.Player1); Check(target.Zone == game.Player1.HandZone && DarkGifts.Has(target) && target.AttackDamage == 10,
                    "later forward draw retains gift and does not relocate again");
            });
        }
    }
    private static void SweetDreamsBurnAndStack() {
        foreach (bool deck in new[] { false, true }) {
            var g = New(); while (!g.Player1.HandZone.IsFull) Minion(g);
            var m = Minion(g, zone: deck ? Zone.DECK : Zone.SETASIDE); ChooseSweet(g, m);
            int count = g.Player1.DeckZone.Count; Accept(g, Answer(g));
            Check(m.Zone == g.Player1.GraveyardZone && !DarkGifts.Has(m) && g.Player1.DeckZone.Count == count - (deck ? 1 : 0), "full hand burns; Sweet Dreams moves nothing");
        }
        {
            var g = New(); var m = Minion(g, zone: Zone.SETASIDE); ChooseSweet(g, m, ChoiceAction.STACK);
            Accept(g, Answer(g)); Check(m.Zone == g.Player1.SetasideZone && DarkGifts.Has(m), "STACK continuation owns movement");
            DarkGifts.ResolveSweetDreams(m); Check(m.Zone == g.Player1.SetasideZone, "helper ignores non-hand entity");
            Generic.RemoveFromZone(g.Player1, m); Generic.AddHandPhase(g.Player1, m);
            Check(m.Zone == g.Player1.HandZone, "STACK hand arrival is not automatically relocated");
            Futures(g, game => {
                var target = game.IdEntityDic[m.Id]; DarkGifts.ResolveSweetDreams(target);
                Check(game.Player1.DeckZone.TopCard == target && ((Minion)target).AttackDamage == 8 && ((Minion)target).Health == 10, "STACK explicit helper relocation");
                Generic.Draw(game.Player1, target); DarkGifts.ResolveSweetDreams(target);
                Check(target.Zone == game.Player1.HandZone, "STACK helper consumes grant and cannot relocate again after redraw");
            });
        }
    }
    private static void SweetDreamsFullDeckConsumesGrant() {
        foreach (bool history in new[] { false, true })
        foreach (var action in new[] { ChoiceAction.HAND, ChoiceAction.STACK }) {
            var g = New(history); var m = Minion(g, zone: Zone.SETASIDE);
            while (!g.Player1.DeckZone.IsFull) Minion(g, zone: Zone.DECK);
            ChooseSweet(g, m, action);
            Futures(g, game => {
                var target = game.IdEntityDic[m.Id]; var p = game.Player1;
                Accept(game, Answer(game));
                if (action == ChoiceAction.STACK) {
                    DarkGifts.ResolveSweetDreams(target);
                    Generic.RemoveFromZone(p, target); Generic.AddHandPhase(p, target);
                    DarkGifts.ResolveSweetDreams(target);
                }
                Check(target.Zone == p.HandZone && p.DeckZone.IsFull && DarkGifts.GiftsOf(target).Count == 1,
                    "full deck rejects Sweet Dreams relocation but retains gift");
                Futures(game, future => {
                    var held = future.IdEntityDic[m.Id]; var owner = future.Player1;
                    Generic.Draw(owner);
                    Check(!owner.DeckZone.IsFull, "draw frees deck slot after rejected relocation");
                    int count = owner.DeckZone.Count;
                    DarkGifts.ResolveSweetDreams(held);
                    Check(held.Zone == owner.HandZone && owner.DeckZone.Count == count,
                        "full-deck rejection consumes grant; helper retry cannot relocate after space opens");
                });
            });
        }
    }
    private static void ExistingSweetDreamsHandChoice() {
        foreach (bool history in new[] { false, true })
        foreach (bool deck in new[] { false, true }) {
            var g = New(history); var m = Minion(g, zone: deck ? Zone.DECK : Zone.SETASIDE);
            Buff(g, m, "EDR_100t8e1");
            var context = new EffectContext(g, g.Player1.Id, g.Player1.Hero.Id);
            Check(context.DiscoverEntitiesWithDarkGift(new[] { m }), "open existing Sweet Dreams choice");
            var spell = Entity.FromCard(g.Player1, Cards.FromId("EDR_100t"),
                new EntityData { [GameTag.CREATOR] = g.Player1.Choice.SourceId }, g.Player1.SetasideZone);
            m[GameTag.DARK_GIFT_ENTITY] = spell.Id;
            Futures(g, game => {
                var target = (Minion)game.IdEntityDic[m.Id]; Accept(game, Answer(game));
                Check(target.Zone == game.Player1.HandZone && target.HasLifeSteal &&
                    DarkGifts.GiftsOf(target).Select(e => e.Card.Id).SequenceEqual(new[] { "EDR_100t8e1", "EDR_100te" }),
                    "HAND choice only relocates newly granted Sweet Dreams, preserving older and new gifts");
                DarkGifts.ResolveSweetDreams(target);
                Check(target.Zone == game.Player1.HandZone, "helper ignores pre-existing Sweet Dreams");
            });
        }
    }
    private static void WallowSweetDreamsChoice() {
        foreach (bool history in new[] { false, true })
        foreach (bool deck in new[] { false, true }) {
            var g = New(history); var m = Minion(g, "EDR_487", zone: deck ? Zone.DECK : Zone.SETASIDE);
            Buff(g, m, "EDR_100t2e"); ChooseSweet(g, m);
            Futures(g, game => {
                var target = (Minion)game.IdEntityDic[m.Id];
                int start = game.History ? game.PowerHistory.Full.Count : 0;
                Accept(game, Answer(game));
                Check(target.Zone == game.Player1.DeckZone && game.Player1.DeckZone.TopCard == target && target.Cost == 5 &&
                    target.AttackDamage == (deck ? 12 : 8) && target.Health == (deck ? 16 : 11) &&
                    DarkGifts.GiftsOf(target).Count == (deck ? 3 : 2), "Wallow choice preserves Short Claws discount and all gifts");
                if (history)
                    Check(game.PowerHistory.Full.Skip(start).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                        .Where(change => change.EntityId == target.Id && change.Tag == GameTag.ZONE).Select(change => change.Value)
                        .SequenceEqual(new[] { (int)Zone.HAND, (int)Zone.DECK }), "Wallow choice moves hand then deck exactly once");
                Generic.Draw(game.Player1, target); DarkGifts.ResolveSweetDreams(target);
                Check(target.Zone == game.Player1.HandZone && target.Cost == 5, "Wallow grant consumed; later draw retains discount");
            });
        }
    }
    // dg17b event lean, dg7 pinned Wallow exception; test entity only, no collectible registration.
    private static void WallowMirror() {
        foreach (bool history in new[] { false, true })
        foreach (bool deck in new[] { false, true }) {
            var g = New(history); var source = Minion(g, zone: Zone.SETASIDE); var wallow = Minion(g, zone: deck ? Zone.DECK : Zone.HAND);
            DarkGifts.Mirror(wallow, "EDR_100t", source); DarkGifts.Mirror(wallow, "EDR_100t", source);
            Check(DarkGifts.GiftsOf(wallow).Count == 2 && wallow.AttackDamage == 10 && wallow.HasLifeSteal, "duplicate mirrors are independent gifts");
            if (deck) Minion(g, zone: Zone.DECK); // Wallow starts below top.
            Futures(g, game => {
                var target = game.IdEntityDic[wallow.Id]; var donor = game.IdEntityDic[source.Id]; int events = 0;
                TriggerManager.TriggerHandler handler = _ => events++;
                game.TriggerManager.Events.Subscribe<DarkGiftGivenEvent>(handler);
                var rng = game.Random.CaptureState(); int entities = game.IdEntityDic.Count;
                DarkGifts.Mirror(target, "EDR_100t8", donor);
                game.TriggerManager.Events.Unsubscribe<DarkGiftGivenEvent>(handler);
                var gifts = DarkGifts.GiftsOf(target); var sweet = gifts.Last();
                Check(events == 0 && game.Random.CaptureState().Equals(rng) && game.IdEntityDic.Count == entities + 2 && gifts.Count == 3 &&
                    game.Player1.DeckZone.TopCard == target && ((Minion)target).AttackDamage == 14 && ((Minion)target).Health == 10 &&
                    !((Entity)target).PreserveDarkGiftsOnNextBackwardMove, "Sweet mirror top-decks Wallow, preserves all gifts, no event/RNG, one-use flag consumed");
                Check(sweet.Creator.Card.Id == "EDR_100t8" && sweet.Creator[GameTag.CREATOR] == donor.Id && sweet[GameTag.CREATOR] == sweet.Creator.Id &&
                    gifts.Select(e => e.Creator.Id).Distinct().Count() == 3 && sweet[GameTag.ATTACHED] == target.Id, "fresh spell/enchantment creator chains");
                Generic.Draw(game.Player1); Check(target.Zone == game.Player1.HandZone && DarkGifts.GiftsOf(target).Count == 3, "deck gifts survive forward draw");
                game.Player1.DeckZone.Add(game.Player1.HandZone.Remove(target));
                Check(!DarkGifts.Has(target) && ((Minion)target).AttackDamage == 4 && !((Minion)target).HasLifeSteal, "subsequent ordinary backwards move removes every gift");
            });
        }
    }
    private static void WallowMirrorCost() {
        foreach (bool history in new[] { false, true })
        foreach (bool deck in new[] { false, true }) {
            var g = New(history); var wallow = Minion(g, zone: deck ? Zone.DECK : Zone.HAND);
            DarkGifts.Mirror(wallow, "EDR_100t2", g.Player1.Hero);
            Check(wallow.Cost == 2, "Short Claws lowers printed cost 4 to 2");
            Futures(g, game => {
                var target = game.IdEntityDic[wallow.Id];
                DarkGifts.Mirror(target, "EDR_100t8", game.Player1.Hero);
                Check(target.Zone == game.Player1.DeckZone && target.Cost == 2,
                    "Wallow retains Short Claws cost after Sweet Dreams relocation: " + target.Cost);
                Futures(game, future => {
                    var drawn = future.IdEntityDic[wallow.Id]; Generic.Draw(future.Player1);
                    Check(drawn.Zone == future.Player1.HandZone && drawn.Cost == 2,
                        "Wallow retains Short Claws cost after redraw: " + drawn.Cost);
                });
            });
        }
    }
    // Missing binding, wrong sender/metadata, zone leaks or unregistered callbacks break this proof.
    private static void GiftGivenTrigger() {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try {
            card.Power = new Power { Trigger = new Trigger(TriggerType.DARK_GIFT_GIVEN) {
                TriggerActivation = TriggerActivation.HAND_OR_DECK, TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = StateTaskList.Chain(new GetEventNumberTask(),
                    new SetGameTagNumberTask(GameTag.TAG_SCRIPT_DATA_NUM_1, EntityType.SOURCE),
                    new GetGameTagTask(GameTag.ENTITY_ID, EntityType.TARGET),
                    new SetGameTagNumberTask(GameTag.TAG_SCRIPT_DATA_NUM_2, EntityType.SOURCE),
                    new GetGameTagTask(GameTag.ENTITY_ID, EntityType.EVENT_SOURCE),
                    new SetGameTagNumberTask((GameTag)31003, EntityType.SOURCE),
                    new GetGameTagTask(GameTag.HAS_DARK_GIFT, EntityType.EVENT_TARGET),
                    new SetGameTagNumberTask((GameTag)31004, EntityType.SOURCE), new ArmorTask(1))
            } };
            foreach (bool history in new[] { false, true })
            foreach (var armedZone in new[] { Zone.HAND, Zone.DECK })
            foreach (var zone in new[] { Zone.HAND, Zone.DECK, Zone.PLAY, Zone.GRAVEYARD }) {
                var g = New(history); var listener = Minion(g, card.Id, armedZone);
                Check(listener.ActivatedTrigger?.Type == TriggerType.DARK_GIFT_GIVEN, "listener starts armed in " + armedZone);
                var recipient = Minion(g, zone: Zone.SETASIDE);
                int expected = zone == Zone.HAND || zone == Zone.DECK ? 1 : 0;
                Futures(g, game => {
                    var owner = game.IdEntityDic[listener.Id]; var target = game.IdEntityDic[recipient.Id];
                    Generic.RemoveFromZone(game.Player1, owner);
                    if (zone == Zone.HAND) game.Player1.HandZone.Add(owner);
                    else if (zone == Zone.DECK) game.Player1.DeckZone.Add(owner);
                    else if (zone == Zone.PLAY) game.Player1.BoardZone.Add((Minion)owner);
                    else game.Player1.GraveyardZone.Add(owner);
                    Check(expected != 0 || owner.ActivatedTrigger == null, "armed listener deactivated after move to " + zone);
                    var previous = game.CurrentEventData;
                    Check(DarkGifts.Give(target, game.Player1.Hero), "original gift grant");
                    Check(game.Player1.Hero.Armor == expected && ReferenceEquals(previous, game.CurrentEventData),
                        "one immediate response in " + zone + "; outer event restored");
                    if (expected != 0) {
                        var spell = DarkGifts.GiftsOf(target).Single().Creator;
                        Check(owner[GameTag.TAG_SCRIPT_DATA_NUM_1] == spell.Card.AssetId &&
                            owner[GameTag.TAG_SCRIPT_DATA_NUM_2] == target.Id && owner[(GameTag)31003] == spell.Id &&
                            owner[(GameTag)31004] == 1, "source=listener, target=recipient, gift spell/id after attachment");
                    }
                    DarkGifts.Mirror(target, "EDR_100t", game.Player1.Hero);
                    var enemy = Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.SetasideZone);
                    DarkGifts.Give(enemy, game.Player2.Hero);
                    var offered = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.DeckZone);
                    new EffectContext(game, game.Player1.Id, game.Player1.Hero.Id).DiscoverEntitiesWithDarkGift(new[] { offered });
                    Check(game.Player1.Hero.Armor == expected, "no mirror/opponent/unchosen-offer response");
                    Accept(game, Answer(game));
                    Check(game.Player1.Hero.Armor == expected * 2, "chosen offer grants once");
                });
            }
            var movement = New(); var moving = Minion(movement, card.Id, Zone.DECK);
            Generic.Draw(movement.Player1, moving);
            DarkGifts.Give(moving, movement.Player1.Hero);
            Check(movement.Player1.Hero.Armor == 1, "deck to hand re-arms once, including self-grant");
            movement.Player1.DeckZone.Add(movement.Player1.HandZone.Remove(moving));
            DarkGifts.Give(moving, movement.Player1.Hero);
            Check(movement.Player1.Hero.Armor == 2, "hand to deck re-arms once");
        } finally { card.Power = original; }
    }
    // Replaying a declared pool Battlecry from another card must retain recipe identity and real source.
    private static void ForeignPoolBattlecry() {
        var recipe = new DecisionBattlecryTask("proof/dark-gift/foreign-pool-battlecry/v1",
            new DiscoverWithDarkGiftThenContinueTask(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.PastFiveCostMinion(), recipeDbfId: 120782),
            new CustomTask((game, controller, source, target, stack) => {
                var chosen = stack.Playables.Single();
                Check(chosen.Card.Id != source.Card.Id && DarkGifts.Has(chosen), "foreign replay excludes executor and grants stored gift");
                var spell = DarkGifts.GiftsOf(chosen).Single().Creator;
                var executor = game.IdEntityDic[spell[GameTag.CREATOR]];
                Check(chosen[GameTag.CREATOR] == source.Id &&
                    executor.Card.Id == "EDR_102t" && executor[GameTag.CREATOR] == source.Id,
                    "foreign replay retains runtime creator");
                controller.Hero.Armor++;
            }));
        foreach (bool history in new[] { false, true }) {
            var g = New(history); var source = Minion(g, "EX1_028", Zone.SETASIDE);
            foreach (var task in new[] {
                new DiscoverWithDarkGiftThenContinueTask(OfflinePoolFilter.Discover(minCost: 999), recipeDbfId: 120782),
                new DiscoverWithDarkGiftThenContinueTask(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.PastFiveCostMinion(), recipeDbfId: 1) }) {
                var before = g.SaveSnapshot(); bool rejected = false;
                try { task.Open(g, g.Player1, source, null, new TaskStack()); }
                catch (InvalidOperationException e) { rejected = e.Message.Contains("declared pool"); }
                Check(rejected && before.SequenceEqual(g.SaveSnapshot()), "recipe override still rejects mismatched filter/declaration without mutation");
            }
            Check(recipe.Recipe.Process(g, g.Player1, source, null) == TaskState.SUSPENDED &&
                g.Player1.Choice.Choices.All(id => g.IdEntityDic[id].Card.Id != source.Card.Id),
                "foreign recipe suspends with runtime source excluded");
            Futures(g, game => {
                Accept(game, Answer(game));
                for (int i = 0; i < 2; i++)
                    Check(recipe.Process(game, game.Player1, game.IdEntityDic[source.Id], null) == TaskState.COMPLETE,
                        "foreign source repeats gifted pool Battlecry without throwing");
                Check(game.Player1.Hero.Armor == 3 && !game.Resolution.HasSuspendedEffect, "saved foreign choice and both repetitions complete");
            });
        }
    }
    private static readonly DecisionBattlecryTask DeckRecipe = new("proof/dark-gift/deck-discover/v1",
        new DiscoverEntitiesWithDarkGiftThenContinueTask(c => DarkGifts.SampleDeckOffers(c, _ => true), recipeDbfId: 120782),
        new CustomTask(DeckContinue));
    private static void DeckOfferSampling() {
        foreach (bool history in new[] { false, true }) {
            var g = New(history);
            while (!g.Player1.DeckZone.IsEmpty) g.Player1.DeckZone.Remove(0);
            for (int i = 0; i < 6; i++) Minion(g, "CS2_231", Zone.DECK);
            Minion(g, "CS2_182", Zone.DECK); Minion(g, "EX1_028", Zone.DECK); Minion(g, "EX1_319", Zone.DECK);
            Entity.FromCard(g.Player1, Cards.FromId("CS2_029"), zone: g.Player1.DeckZone);
            g.Random.RestoreState(new RngState(2728464994113939040L, -7184610871789209634L, 0));
            Futures(g, game => {
                var c = game.Player1; var deck = c.DeckZone.Select(p => p.Id).ToArray();
                var offers = DarkGifts.SampleDeckOffers(c, _ => true);
                // Hand-derived xoroshiro indices in the nine-minion pool: 6, 4, 0 (duplicate), 7.
                Check(offers.Select(p => p.Id).SequenceEqual(new[] { deck[6], deck[4], deck[7] }) &&
                    game.Random.DrawCount == 4, "copy-weighted offers reject duplicate card IDs, consuming rejection RNG");
                var rng = game.Random.CaptureState();
                Check(DarkGifts.SampleDeckOffers(c, _ => true, 4).Select(p => p.Id)
                    .SequenceEqual(new[] { deck[0], deck[6], deck[7], deck[8] }), "small pool returns first entity per card ID in deck order");
                Check(DarkGifts.SampleDeckOffers(c, p => p.Card.Id == "EX1_028").Single().Id == deck[7] &&
                    DarkGifts.SampleDeckOffers(c, _ => false).Count == 0 &&
                    DarkGifts.SampleDeckOffers(c, _ => true, 0).Count == 0 && game.Random.CaptureState().Equals(rng),
                    "predicate, empty and zero offers consume no RNG");
                bool rejected = false;
                try { DarkGifts.SampleDeckOffers(c, _ => true, -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected && game.Random.CaptureState().Equals(rng) && deck.SequenceEqual(c.DeckZone.Select(p => p.Id)) &&
                    c.DeckZone.All(p => !DarkGifts.Has(p)), "sampling validates count and leaves deck/entities untouched");
                DeckRecipe.Recipe.Process(game, c, c.Hero, null);
                Check(c.Choice.Choices.Select(id => game.IdEntityDic[id].Card.Id).Distinct().Count() == 3,
                    "duplicate-heavy selector opens three distinct offers");
                Futures(game, future => {
                    Accept(future, Answer(future));
                    Check(!future.Resolution.HasSuspendedEffect && future.Player1.Hero.Armor == 1,
                        "sampled pending choice resumes once across clone/snapshot");
                });
            });
        }
    }
    private static void DeckContinue(Game g, Controller c, IEntity source, IPlayable target, TaskStack stack) {
        var chosen = stack.Playables?.SingleOrDefault();
        if (chosen != null) {
            Check(c.LastCardDrawn == chosen.Id, "continuation follows actual chosen draw");
            if (DarkGifts.GiftsOf(chosen).Any(e => e.Card.Id == "EDR_100t8e1"))
                Check(chosen.Zone == c.DeckZone && c.DeckZone.TopCard == chosen, "continuation follows Sweet Dreams relocation");
            else Check(chosen.Zone == c.HandZone || chosen.Zone == c.GraveyardZone, "continuation follows hand arrival or burn");
            if (c.Hero.ActivatedTrigger?.Type == TriggerType.DRAW)
                Check(c.Hero.Armor % 11 == 10, "Draw trigger precedes continuation exactly once");
        }
        c.Hero.Armor++;
    }
    private static void DeckDiscoverBoundaries() {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try {
            card.Power = new Power { PowerTask = DeckRecipe };
            foreach (bool history in new[] { false, true })
            foreach (bool burn in new[] { false, true }) {
                var g = New(history); var source = Minion(g, card.Id); source.HasBattleCry = true;
                if (burn) while (!g.Player1.HandZone.IsFull) Minion(g);
                Play(g, source);
                // Fill the slot freed by playing the source, after the decision has opened.
                if (burn) Minion(g);
                int id = g.Player1.Choice.Choices[0]; var offered = g.IdEntityDic[id];
                var spell = Entity.FromCard(g.Player1, Cards.FromId("EDR_100t8"),
                    new EntityData { [GameTag.CREATOR] = g.Player1.Choice.SourceId }, g.Player1.SetasideZone);
                offered[GameTag.DARK_GIFT_ENTITY] = spell.Id;
                int count = g.Player1.DeckZone.Count, drawn = g.Player1.NumCardsDrawnThisTurn;
                Futures(g, game => {
                    var rng = game.Random.CaptureState(); Accept(game, Answer(game));
                    var chosen = game.IdEntityDic[id];
                    Check(game.Player1.Hero.Armor == 1 && game.Player1.NumCardsDrawnThisTurn == drawn + 1 &&
                        game.Player1.LastCardDrawn == id && !game.Resolution.HasSuspendedEffect && game.Player1.Choice == null &&
                        game.Random.CaptureState().Equals(rng), "draw/burn completes once without new RNG");
                    Check(burn ? chosen.Zone == game.Player1.GraveyardZone && !DarkGifts.Has(chosen) && game.Player1.DeckZone.Count == count - 1 :
                        chosen.Zone == game.Player1.DeckZone && game.Player1.DeckZone.TopCard == chosen && DarkGifts.Has(chosen) &&
                        game.Player1.DeckZone.Count == count, "Sweet Dreams resolves after draw; burn stays in graveyard");
                    if (history && !burn) {
                        var zones = game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                            .Where(p => p.EntityId == id && p.Tag == GameTag.ZONE).Select(p => p.Value).ToArray();
                        Check(zones.TakeLast(2).SequenceEqual(new[] { (int)Zone.HAND, (int)Zone.DECK }), "Sweet Dreams wire is hand then deck");
                    }
                });
            }
            {
                var g = New(); while (!g.Player1.DeckZone.IsEmpty) g.Player1.DeckZone.Remove(0);
                var source = Minion(g, card.Id); source.HasBattleCry = true; Buff(g, source, "EDR_100t7e");
                var rng = g.Random.CaptureState(); int drawn = g.Player1.NumCardsDrawnThisTurn;
                Play(g, source);
                Check(g.Player1.Choice == null && !g.Resolution.HasSuspendedEffect && g.Player1.Hero.Armor == 2 &&
                    g.Player1.NumCardsDrawnThisTurn == drawn && g.Player1.Hero.Fatigue == 0 && g.Random.CaptureState().Equals(rng),
                    "empty doubled Discover skips both prompts and draws without fatigue/RNG");
            }
        } finally { card.Power = original; }
    }
    // Overlapping choices, stale selectors, generated copies or missed/doubled draws break this proof.
    private static void DeckDiscoverContinuations() {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try {
            card.Power = new Power { PowerTask = DeckRecipe };
            foreach (bool history in new[] { false, true })
            foreach (bool rude in new[] { false, true })
            foreach (bool brann in new[] { false, true }) {
                var g = New(history); if (brann) Minion(g, "LOE_077", Zone.PLAY);
                Minion(g, zone: Zone.DECK); Minion(g, "EX1_028", Zone.DECK);
                new Trigger(TriggerType.DRAW) { TriggerSource = TriggerSource.FRIENDLY, SingleTask = new ArmorTask(10) }.Activate(g.Player1.Hero);
                var m = Minion(g, card.Id); m.HasBattleCry = true;
                if (rude) Buff(g, m, "EDR_100t7e");
                int draws = g.Player1.NumCardsDrawnThisTurn, count = g.Player1.DeckZone.Count;
                int[] deck = g.Player1.DeckZone.Select(p => p.Id).ToArray();
                Play(g, m);
                Check(g.Resolution.HasSuspendedEffect && g.Player1.Hero.Armor == 0 && g.Player1.Choice.NextChoice == null &&
                    deck.SequenceEqual(g.Player1.DeckZone.Select(p => p.Id)), "one pending choice; offers stay in deck before answer");
                var ids = g.Player1.Choice.Choices;
                Check(ids.Select(id => g.IdEntityDic[id].Card.Id).Distinct().Count() == 3 &&
                    ids.All(id => !DarkGifts.Has(g.IdEntityDic[id])) &&
                    ids.Select(id => g.IdEntityDic[g.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id).Distinct().Count() == 3,
                    "distinct card ids and preassigned gifts; no grants at open");
                Futures(g, first => {
                    // Avoid Sweet Dreams here so the chosen entity must be absent from the next deck offers.
                    int index = first.Player1.Choice.Choices.FindIndex(id =>
                        first.IdEntityDic[first.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8");
                    var answer = Answer(first, index); int chosen = answer.SelectedOptionIds.Single();
                    int giftDbf = first.IdEntityDic[first.IdEntityDic[chosen][GameTag.DARK_GIFT_ENTITY]].Card.AssetId;
                    var rng = first.Random.CaptureState();
                    Accept(first, answer);
                    Check(first.IdEntityDic[chosen].Zone == first.Player1.HandZone && first.Player1.LastCardDrawn == chosen &&
                        DarkGifts.GiftsOf(first.IdEntityDic[chosen]).Single()[GameTag.TAG_SCRIPT_DATA_NUM_6] == giftDbf &&
                        first.Player1.NumCardsDrawnThisTurn == draws + 1 && first.Player1.DeckZone.Count == count - 1 && first.Player1.Hero.Armor == 11,
                        "stored gift then exact entity draw/Draw trigger once, then continuation");
                    bool twice = rude || brann;
                    Check(first.Resolution.HasSuspendedEffect == twice, "Boolean Battlecry doubling");
                    if (!twice) {
                        Check(first.Player1.Choice == null && first.Random.CaptureState().Equals(rng), "single answer consumes no RNG");
                        return;
                    }
                    Check(first.Player1.Choice.NextChoice == null && !first.Player1.Choice.Choices.Contains(chosen) &&
                        first.Player1.Choice.Choices.All(id => first.IdEntityDic[id].Zone == first.Player1.DeckZone) &&
                        first.Random.DrawCount == rng.DrawCount + 3, "second selector sees remaining deck; only next offer gifts consume RNG");
                    var before = first.SaveSnapshot();
                    Check(!first.TryAnswer(answer, out _) && before.SequenceEqual(first.SaveSnapshot()), "first answer cannot replay");
                    Futures(first, second => {
                        int nextIndex = second.Player1.Choice.Choices.FindIndex(id =>
                            second.IdEntityDic[second.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8");
                        Accept(second, Answer(second, nextIndex));
                        Check(!second.Resolution.HasSuspendedEffect && second.Player1.Choice == null &&
                            second.Player1.Hero.Armor == 22 && second.Player1.NumCardsDrawnThisTurn == draws + 2 &&
                            second.Player1.DeckZone.Count == count - 2 && second.Player1.NumMinionsPlayedThisTurn == 1 &&
                            second.Player1.PlayHistory.Count == 1, "two sequential draws, one outer play, no third Discover");
                    });
                });
            }
        } finally { card.Power = original; }
    }
    private static void DeckDiscoverValidationAndReplay() {
        var g = New(); var first = Minion(g, zone: Zone.DECK); var duplicate = Minion(g, zone: Zone.DECK);
        var hand = Minion(g); var enemy = Entity.FromCard(g.Player2, first.Card, zone: g.Player2.DeckZone);
        foreach (var offers in new IPlayable[][] { null, new IPlayable[] { null }, new[] { hand }, new[] { enemy },
            new IPlayable[] { g.Player1.Hero }, new IPlayable[] { first, first }, new IPlayable[] { first, duplicate } }) {
            var before = g.SaveSnapshot(); bool rejected = false;
            try {
                new DiscoverEntitiesWithDarkGiftThenContinueTask(_ => offers)
                    .Open(g, g.Player1, g.Player1.Hero, null, new TaskStack());
            } catch (ArgumentException) { rejected = true; }
            Check(rejected && before.SequenceEqual(g.SaveSnapshot()), "invalid own-deck/distinct-card-id selector rejects before mutation");
        }
        var stale = new TaskStack { Playables = new List<IPlayable> { hand } };
        var empty = new DiscoverEntitiesWithDarkGiftThenContinueTask(_ => Array.Empty<IPlayable>());
        var initial = g.SaveSnapshot();
        Check(empty.Open(g, g.Player1, g.Player1.Hero, null, stale) == TaskState.COMPLETE && stale.Playables.Count == 0 &&
            initial.SequenceEqual(g.SaveSnapshot()), "empty selector clears stale stack without entities/choice/RNG");
        var task = new DiscoverEntitiesWithDarkGiftThenContinueTask(c => new[] { c.DeckZone.First() });
        task.Open(g, g.Player1, g.Player1.Hero, null, stale);
        var pending = g.SaveSnapshot(); bool blocked = false;
        try { empty.Open(g, g.Player1, g.Player1.Hero, null, stale); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked && pending.SequenceEqual(g.SaveSnapshot()), "pending choice guard remains atomic");

        var selection = New();
        var recipe = StateTaskList.Suspendable("proof/dark-gift/deck-selection-validation/v1",
            new DiscoverEntitiesWithDarkGiftThenContinueTask(c => new[] { c.DeckZone.First() }));
        recipe.Process(selection, selection.Player1, selection.Player1.Hero, null);
        var answer = Answer(selection); int selected = answer.SelectedOptionIds.Single();
        selection.Player1.SetasideZone.Add(selection.Player1.DeckZone.Remove(selection.IdEntityDic[selected]));
        var moved = selection.SaveSnapshot();
        Check(!selection.TryAnswer(answer, out _) && moved.SequenceEqual(selection.SaveSnapshot()), "answer requires selected entity still in own deck");

        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try {
            card.Power = new Power { PowerTask = DeckRecipe };
            string Replay(List<DecisionAnswer> answers, bool record) {
                var game = New(); Minion(game, zone: Zone.DECK); Minion(game, "EX1_028", Zone.DECK);
                var source = Minion(game, card.Id); source.HasBattleCry = true; Buff(game, source, "EDR_100t7e");
                Play(game, source); int cursor = 0;
                while (game.Resolution.HasSuspendedEffect) {
                    if (record) answers.Add(Answer(game));
                    Accept(game, answers[cursor++]);
                }
                Check(cursor == 2, "replay answers both sequential Discover prompts");
                return Canonical.Sha(Canonical.State(game) + Canonical.History(game) + Canonical.Value(game.Random.CaptureState()));
            }
            var answers = new List<DecisionAnswer>(); var recorded = Replay(answers, true);
            Check(recorded == Replay(answers, false), "recorded answers replay identical deck entities, gifts, state/history/RNG");
        } finally { card.Power = original; }
    }
    private static void Determinism() {
        string Replay() {
            var g = New(); var m = Minion(g); Buff(g, m, "EDR_100t5e"); Buff(g, m, "EDR_100t9e"); Buff(g, m, "EDR_100t7e");
            Play(g, m); Kill(g, m);
            var wallow = Minion(g); DarkGifts.Mirror(wallow, "EDR_100t8", g.Player1.Hero); Generic.Draw(g.Player1);
            return Canonical.Sha(Canonical.State(g) + Canonical.History(g));
        }
        Check(Replay() == Replay(), "same initial seed and action/answer sequence replays identical state/history");
    }
    internal static void Run() {
        var failures = new List<Exception>();
        foreach (var proof in new Action[] { LivingNightmare, LivingNightmareBoundaries, LivingNightmareBeforeBattlecry,
            PersistingHorror, PersistingHorrorRestoresCost, PersistingHorrorReappliesKeywords, PersistingHorrorAdditiveEnchantment, PersistingHorrorTemporaryBuff, PersistingHorrorBoundaries, RudeAwakening, RudeAwakeningCombo, RudeAwakeningChooseOne, RudeAwakeningDecisions,
            SweetDreams, SweetDreamsBurnAndStack, SweetDreamsFullDeckConsumesGrant, ExistingSweetDreamsHandChoice, WallowSweetDreamsChoice, WallowMirror, WallowMirrorCost, GiftGivenTrigger, ForeignPoolBattlecry,
            DeckOfferSampling, DeckDiscoverContinuations, DeckDiscoverBoundaries, DeckDiscoverValidationAndReplay, Determinism }) {
            try { proof(); Console.WriteLine("PASS dark gift behaviour " + proof.Method.Name); }
            catch (Exception e) { Console.WriteLine("FAIL " + proof.Method.Name + ": " + e.Message); failures.Add(e); }
        }
        if (failures.Count != 0) throw new AggregateException(failures);
    }
}
