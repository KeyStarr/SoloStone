using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.EffectSystem;
using static Core2026Proof;

internal static class OutcastProof
{
    private const GameTag Edge = (GameTag)31008;
    private static readonly DecisionBattlecryTask DecisionRecipe = new("proof/outcast/decision/v1",
        new SelectEntityThenContinueTask(), EffectContext.Task(ctx => {
            if (ctx.PlayedFromEdge) ctx.RunLegacy(new ArmorTask(1));
            return TaskState.COMPLETE;
        }));
    // Plain suspendable primary powers: the gated step runs only after the player's answer.
    private static readonly ISimpleTask Gated = EffectContext.Task(ctx => {
        if (ctx.PlayedFromEdge) ctx.RunLegacy(new ArmorTask(1));
        return TaskState.COMPLETE;
    });
    private static readonly SuspendableTask DiscoverRecipe = StateTaskList.Suspendable("proof/outcast/discover-resume/v1",
        new DiscoverThenContinueTask("CS2_231", "CS2_182", "CS2_172"), Gated);
    // Round 4: the resumed continuation queues the gated step into the legacy queue (drained after the frame pops).
    private static readonly SuspendableTask QueuedRecipe = StateTaskList.Suspendable("proof/outcast/discover-queued/v1",
        new DiscoverThenContinueTask("CS2_231", "CS2_182", "CS2_172"), new ArmorTask(10), new EnqueueTask(1, Gated));
    // Work queued before a second suspension drains while the play is still pending.
    private static readonly SuspendableTask TwiceRecipe = StateTaskList.Suspendable("proof/outcast/discover-twice/v1",
        new DiscoverThenContinueTask("CS2_231", "CS2_182", "CS2_172"), new EnqueueTask(1, Gated),
        new DiscoverThenContinueTask("CS2_231", "CS2_182", "CS2_172"), Gated);
    private static readonly SuspendableTask PoolRecipe = StateTaskList.Suspendable("proof/outcast/pool-discover-resume/v1",
        new DiscoverThenContinueTask(OfflinePoolFilter.Discover()), Gated);

    internal static void EmptyHand(Controller player)
    {
        foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
    }

    internal static void DrawCases(Game game, Controller player, string id, int baseline)
    {
        foreach (var (size, position, expected) in new[] { (1, 0, 1), (2, 0, 1), (2, 1, 1), (3, 0, 1), (3, 1, 0), (3, 2, 1) })
        {
            EmptyHand(player); IPlayable source = null;
            for (int i = 0; i < size; i++) {
                var card = Add(player, i == position ? id : "CS2_182");
                if (i == position) source = card;
            }
            int deck = player.DeckZone.Count;
            PlayEntity(game, player, source);
            Core2026Proof.Check(player.DeckZone.Count == deck - baseline - expected
                && player.HandZone.Count == size - 1 + baseline + expected, "R1/R2 draw: left/right/middle/single/two cards");
            Core2026Proof.Check(!OutcastRules.PlayedFromEdge(source), "draw card play clears snapshot");
            if (source is Minion minion) Kill(game, minion);
        }
    }

    internal static void DrawBurn(Game game, Controller player, string id, int draws, bool fillOnPlay = false)
    {
        EmptyHand(player);
        for (int i = 0; i < 9; i++) Add(player, "CS2_182");
        var source = Add(player, id);
        void Fill(IEntity played) { if (played.Id == source.Id) Add(player, "CS2_182"); }
        if (fillOnPlay) game.TriggerManager.PlayCardTrigger += Fill;
        int deck = player.DeckZone.Count, graveyard = player.GraveyardZone.Count;
        try { PlayEntity(game, player, source); }
        finally { if (fillOnPlay) game.TriggerManager.PlayCardTrigger -= Fill; }
        Core2026Proof.Check(player.HandZone.Count == 10 && player.DeckZone.Count == deck - draws
            && player.GraveyardZone.Count == graveyard + 1 + (source is Spell ? 1 : 0), "hand-full draw consumes and burns exactly one card");
    }

    internal static void Run()
    {
        // R1 tier A: https://hearthstone.blizzard.com/en-us/news/23305788 (2020-03-17), left/right-most position.
        // R2 tier C: https://hearthstone.wiki.gg/wiki/Outcast (ed. 2024-08-30), either outermost spot.
        foreach (int count in new[] { 1, 2, 3 })
        for (int position = 0; position < count; position++)
        {
            var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
            var definition = Cards.FromId("CS2_231").Clone();
            definition.Tags[(GameTag)1333] = 1;
            definition.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (OutcastRules.PlayedFromEdge((IPlayable)s)) c.Hero.Armor++;
            }) };
            IPlayable source = null;
            for (int i = 0; i < count; i++)
            {
                var card = Generic.DrawCard(p, i == position ? definition : Cards.FromId("CS2_182"));
                if (i == position) source = card;
            }
            bool afterPlay = false;
            game.TriggerManager.AfterPlayMinionTrigger += played => {
                if (played.Id != source.Id) return;
                afterPlay = true;
                Core2026Proof.Check(!OutcastRules.PlayedFromEdge(source), "primary minion power clears Outcast before after-play triggers");
            };
            PlayEntity(game, p, source);
            Core2026Proof.Check(afterPlay && p.Hero.Armor == (position == 0 || position == count - 1 ? 1 : 0), "Outcast R1/R2 samples hand edge before removal");
            Core2026Proof.Check(source[Edge] == 0 && !source.NativeTags.ContainsKey(Edge), "Outcast removed after play; later raw copies cannot emit a zero marker");
            NoHistory(game);
        }
        ReorderAndHandActions();
        DecisionClones();
        DecisionCancellation();
        SuspendedPrimary();
        SuspendedDecisionSpell();
        Console.WriteLine("PASS Outcast primitive: hand-edge matrix; draw/trade reorder; Prepare/trade never play; clone/snapshot pending decision; copy isolation; deferred cleanup; Discover-resumed spell/minion/pool primaries; no internal-tag history");
    }

    private static void SuspendedDecisionSpell()
    {
        foreach (bool darkGift in new[] { false, true })
        {
            ISimpleTask discover = darkGift
                ? new DiscoverEntitiesWithDarkGiftThenContinueTask(c => c.DeckZone.GroupBy(card => card.Card.Id).Select(group => group.First()).Take(3).ToArray())
                : new DiscoverThenContinueTask("CS2_231", "CS2_182", "CS2_172");
            var recipe = new DecisionSpellTask("proof/outcast/decision-spell/" + darkGift + "/v1", discover, Gated);
            WithPower("CORE_BT_491", recipe, () => {
                foreach (int position in new[] { 0, 1, 2 })
                {
                    var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
                    IPlayable source = null;
                    for (int i = 0; i < 3; i++) {
                        var card = Add(p, i == position ? "CORE_BT_491" : "CS2_182");
                        if (i == position) source = card;
                    }
                    int armor = p.Hero.Armor, gain = position == 1 ? 0 : 1;
                    PlayEntity(game, p, source);
                    Core2026Proof.Check(source[Edge] == (gain == 1 ? source.Id | 1 << 30 : 0) &&
                        !OutcastRules.PlayedFromEdge(source), "decision spell parks Outcast before exposing its prompt");
                    var cancelled = Game.LoadSnapshot(game.SaveSnapshot());
                    cancelled.Resolution.CancelSuspendedEffect(cancelled);
                    Core2026Proof.Check(cancelled.SpellCompletion == null && !cancelled.IdEntityDic[source.Id].NativeTags.ContainsKey(Edge),
                        "decision spell cancellation releases completion and parked Outcast");
                    foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
                    {
                        var nextSource = next.IdEntityDic[source.Id]; int afterCast = 0;
                        void After(IEntity played) {
                            if (played.Id != source.Id) return;
                            afterCast++;
                            Core2026Proof.Check(!OutcastRules.PlayedFromEdge(nextSource), "decision spell after-cast cannot reuse Outcast");
                        }
                        next.TriggerManager.AfterCastTrigger += After;
                        Resume(next);
                        next.TriggerManager.AfterCastTrigger -= After;
                        Core2026Proof.Check(afterCast == 1 && next.Player1.Hero.Armor == armor + gain &&
                            next.SpellCompletion == null && !nextSource.NativeTags.ContainsKey(Edge), "decision spell resumes edge once and clears before cast tail");
                        NoHistory(next);
                    }
                }
            });
        }
    }

    private static void NoHistory(Game game) => Core2026Proof.Check(!game.PowerHistory.Full.Any(h => h switch {
        PowerHistoryTagChange tag => tag.Tag == Edge,
        PowerHistoryFullEntity full => full.Entity.Tags.ContainsKey(Edge),
        PowerHistoryShowEntity show => show.Entity.Tags.ContainsKey(Edge),
        _ => false
    }), "Outcast internal tag absent from tag changes and entity history");

    private static void ReorderAndHandActions()
    {
        var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
        var ordinary = Play(game, p, "CS2_231");
        Core2026Proof.Check(!ordinary.NativeTags.ContainsKey(Edge), "ordinary cards never get internal tag");
        var tradeDefinition = Cards.FromId("CORE_SW_072").Clone();
        tradeDefinition.Tags[GameTag.OUTCAST] = 1;
        var trade = Generic.DrawCard(p, tradeDefinition);
        var runner = Add(p, "CORE_BT_480"); Add(p, "CS2_182");
        int plays = p.NumCardsPlayedThisTurn;
        Core2026Proof.Check(game.Process(TradeCardTask.Any(p, trade)), "R5 trade accepted");
        Core2026Proof.Check(trade[Edge] == 0 && !trade.NativeTags.ContainsKey(Edge)
            && p.NumCardsPlayedThisTurn == plays && runner.ZonePosition == 0, "R5 trade is not play; removal changes left edge");
        int deck = p.DeckZone.Count; PlayEntity(game, p, runner);
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "new left edge after trade activates Outcast");
        EmptyHand(p); Add(p, "CS2_182"); runner = Add(p, "CORE_BT_480"); Generic.Draw(p);
        deck = p.DeckZone.Count; PlayEntity(game, p, runner);
        Core2026Proof.Check(p.DeckZone.Count == deck, "draw appends right; former right edge is now middle");
        EmptyHand(p); var prepared = Add(p, "CORE_BT_491"); PrepareRules.Grant(prepared);
        p.UsedMana = 0; plays = p.NumCardsPlayedThisTurn; deck = p.DeckZone.Count;
        Core2026Proof.Check(game.Process(PrepareCardTask.Any(p, prepared)), "R5 Prepare Outcast card accepted");
        Core2026Proof.Check(prepared[Edge] == 0 && !prepared.NativeTags.ContainsKey(Edge)
            && p.NumCardsPlayedThisTurn == plays && p.DeckZone.Count == deck, "R5 Prepare never sets Outcast, plays or draws");
        NoHistory(game);
    }

    private static void DecisionClones()
    {
        var card = Cards.FromId("CORE_BT_480"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = DecisionRecipe };
            var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
            var selected = Add(p, "CS2_182"); var source = Add(p, card.Id);
            PlayEntity(game, p, source);
            Core2026Proof.Check(p.Choice != null && OutcastRules.PlayedFromEdge(source), "edge snapshot retained while decision suspended");
            game.PowerHistory.Add(PowerHistoryBuilder.FullEntity(source));
            game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(source));
            NoHistory(game);
            // Even a raw-tag deck copy must not publish the pending play marker in FULL_ENTITY.
            Entity.FromCard(p, source.Card, new EntityData((EntityData)source.NativeTags), p.DeckZone);
            NoHistory(game);
            var copy = Generic.Copy(p, source, source, Zone.PLAY);
            Core2026Proof.Check(!OutcastRules.PlayedFromEdge(copy) && !copy.NativeTags.ContainsKey(Edge), "board copy does not inherit internal play tag");
            var stack = new TaskStack();
            new SummonCopyTask(EntityType.TARGET, addToStack: true).Process(game, p, p.Hero, source, stack);
            var rawCopy = stack.Playables.Single();
            Core2026Proof.Check(!OutcastRules.PlayedFromEdge(rawCopy) && !rawCopy.NativeTags.ContainsKey(Edge),
                "raw-tag summon copy excludes original entity's Outcast marker while decision pending");
            foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
            {
                var nextSource = next.IdEntityDic[source.Id];
                Core2026Proof.Check(OutcastRules.PlayedFromEdge(nextSource), "clone/snapshot keeps pending edge snapshot");
                bool afterPlay = false;
                next.TriggerManager.AfterPlayMinionTrigger += played => {
                    if (played.Id != nextSource.Id) return;
                    afterPlay = true;
                    Core2026Proof.Check(!OutcastRules.PlayedFromEdge(nextSource), "resumed primary clears Outcast before after-play triggers");
                };
                PlayerDecisionProof.Answer(next, selected.Id);
                Core2026Proof.Check(afterPlay && next.Player1.Hero.Armor == 1 && nextSource[Edge] == 0 && !OutcastRules.PlayedFromEdge(nextSource)
                    && next.Player1.Choice == null, "resumed own recipe sees edge then FinishCardPlay clears it");
                Core2026Proof.Check(!next.IdEntityDic[rawCopy.Id].NativeTags.ContainsKey(Edge), "finishing play removes raw-copy marker too");
                NoHistory(next);
            }
        }
        finally { card.Power = original; }
    }

    private static void DecisionCancellation()
    {
        var card = Cards.FromId("CORE_BT_480"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = DecisionRecipe };
            foreach (bool concede in new[] { false, true })
            {
                var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
                Add(p, "CS2_182"); var source = Add(p, card.Id);
                PlayEntity(game, p, source);
                Core2026Proof.Check(p.Choice != null && OutcastRules.PlayedFromEdge(source), "cancellation starts with pending Outcast play");
                if (concede) Core2026Proof.Check(game.Process(ConcedeTask.Any(p)), "concede during Outcast decision accepted");
                else game.Resolution.CancelSuspendedEffect(game);
                Core2026Proof.Check(!source.NativeTags.ContainsKey(Edge) && !OutcastRules.PlayedFromEdge(source)
                    && !Generic.HasDecisionPlay(p) && !game.Resolution.HasSuspendedEffect && p.Choice == null,
                    $"{(concede ? "concede" : "cancel")} removes pending Outcast marker and continuation");
                NoHistory(game);
            }
        }
        finally { card.Power = original; }
    }

    private static void WithPower(string id, ISimpleTask task, Action proof, bool outcast = false)
    {
        var card = Cards.FromId(id); var original = card.Power; bool hadTag = card.Tags.ContainsKey(GameTag.OUTCAST);
        try { card.Power = new Power { PowerTask = task }; if (outcast) card.Tags[GameTag.OUTCAST] = 1; proof(); }
        finally { card.Power = original; if (outcast && !hadTag) card.Tags.Remove(GameTag.OUTCAST); }
    }

    private static void Resume(Game game) => PlayerDecisionProof.Answer(game, game.Player1.Choice.Choices[0]);

    // Review round 3: a primary power that suspends on a Discover keeps its Outcast play for its own continuation.
    private static void SuspendedPrimary()
    {
        WithPower("CORE_BT_491", DiscoverRecipe, () => WithPower("CORE_BT_480", DiscoverRecipe, () => {
            foreach (string id in new[] { "CORE_BT_491", "CORE_BT_480" })
            foreach (var (size, position) in new[] { (1, 0), (3, 0), (3, 1), (3, 2) })
                SuspendedCase(PlayerDecisionProof.Fixture(), id, size, position, true);
        }));
        WithPower("CORE_BT_491", QueuedRecipe, () => WithPower("CORE_BT_480", QueuedRecipe, () => {
            foreach (string id in new[] { "CORE_BT_491", "CORE_BT_480" })
            foreach (var (size, position) in new[] { (3, 0), (3, 1) })
                SuspendedCase(PlayerDecisionProof.Fixture(), id, size, position, true, control: 10);
        }));
        WithPower("CORE_BT_491", TwiceRecipe, () => {
            foreach (int position in new[] { 0, 1 })
            {
                var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
                IPlayable source = null;
                for (int i = 0; i < 3; i++) { var card = Add(p, i == position ? "CORE_BT_491" : "CS2_182"); if (i == position) source = card; }
                PlayEntity(game, p, source);
                int armor = p.Hero.Armor, gain = position == 0 ? 1 : 0;
                foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
                {
                    var q = next.Player1; var nextSource = next.IdEntityDic[source.Id];
                    Resume(next);
                    Core2026Proof.Check(q.Choice != null && q.Hero.Armor == armor + gain && nextSource[Edge] == (gain == 1 ? source.Id | 1 << 30 : 0),
                        $"queued work before a second Discover reads the pending play, then re-parks ({position})");
                    var copy = next.CloneExact(history: true);
                    foreach (var g in new[] { copy, Game.LoadSnapshot(next.SaveSnapshot()), next })
                    {
                        Resume(g);
                        Core2026Proof.Check(g.Player1.Hero.Armor == armor + 2 * gain && g.Player1.Choice == null
                            && !g.IdEntityDic[source.Id].NativeTags.ContainsKey(Edge), $"second resume reads it once more, then clears ({position})");
                        NoHistory(g);
                    }
                }
            }
        });
        // Pool overload of DiscoverThenContinueTask; CS2_231 has a synthetic test-only pool declaration.
        OfflinePoolTestRecipes.WithDeclarations(() => WithPower("CS2_231", PoolRecipe, () => {
            foreach (var (size, position) in new[] { (3, 2), (3, 1) })
                SuspendedCase(OfflinePoolTestRecipes.NewGame(), "CS2_231", size, position, false);
        }, outcast: true));
        WithPower("CORE_BT_480", DiscoverRecipe, () => {
            foreach (bool concede in new[] { false, true })
            {
                var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
                var source = Add(p, "CORE_BT_480");
                PlayEntity(game, p, source);
                Core2026Proof.Check(p.Choice != null && source[Edge] == (source.Id | 1 << 30), "suspended Discover parks Outcast play");
                if (concede) Core2026Proof.Check(game.Process(ConcedeTask.Any(p)), "concede during suspended Outcast Discover accepted");
                else game.Resolution.CancelSuspendedEffect(game);
                Core2026Proof.Check(!source.NativeTags.ContainsKey(Edge) && !game.Resolution.HasSuspendedEffect && p.Choice == null,
                    $"{(concede ? "concede" : "cancel")} releases parked Outcast play");
                NoHistory(game);
            }
        });
    }

    private static void SuspendedCase(Game game, string id, int size, int position, bool snapshots, int control = 0)
    {
        var p = game.Player1; EmptyHand(p);
        IPlayable source = null;
        for (int i = 0; i < size; i++)
        {
            var card = Add(p, i == position ? id : "CS2_182");
            if (i == position) source = card;
        }
        bool edge = position == 0 || position == size - 1, spell = source is Spell;
        string label = $"{id} {(edge ? "edge" : "middle")} {position}/{size}";
        int afterPlay = 0;
        void After(IEntity played)
        {
            if (played.Id != source.Id) return;
            afterPlay++;
            Core2026Proof.Check(!OutcastRules.PlayedFromEdge(source), label + ": after-play/after-cast cannot reuse Outcast");
        }
        if (spell) game.TriggerManager.AfterCastTrigger += After; else game.TriggerManager.AfterPlayMinionTrigger += After;
        int armor = p.Hero.Armor;
        PlayEntity(game, p, source);
        if (spell) game.TriggerManager.AfterCastTrigger -= After; else game.TriggerManager.AfterPlayMinionTrigger -= After;
        Core2026Proof.Check(afterPlay == (spell ? 0 : 1) && p.Choice != null && game.Resolution.HasSuspendedEffect && p.Hero.Armor == armor,
            label + ": Discover defers spell after-cast; minion after-play has fired");
        Core2026Proof.Check(source[Edge] == (edge ? source.Id | 1 << 30 : 0) && !OutcastRules.PlayedFromEdge(source),
            label + ": parked Outcast play is unreadable while suspended");
        // A raw-tag copy made while parked cannot claim the original play.
        var raw = Entity.FromCard(p, source.Card, new EntityData((EntityData)source.NativeTags), p.SetasideZone);
        Core2026Proof.Check(!OutcastRules.PlayedFromEdge(raw), label + ": raw copy excluded");
        NoHistory(game);
        var games = snapshots ? new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game } : new[] { game };
        foreach (var next in games)
        {
            var nextSource = next.IdEntityDic[source.Id]; var q = next.Player1;
            Core2026Proof.Check(nextSource[Edge] == source[Edge], label + ": clone/snapshot keeps parked play");
            int afterCast = 0;
            void AfterResume(IEntity played)
            {
                if (played.Id != nextSource.Id) return;
                afterCast++;
                Core2026Proof.Check(q.Choice == null && !OutcastRules.PlayedFromEdge((IPlayable)nextSource), label + ": after-cast follows the answer and cannot reuse Outcast");
            }
            if (spell) next.TriggerManager.AfterCastTrigger += AfterResume;
            Resume(next);
            if (spell) next.TriggerManager.AfterCastTrigger -= AfterResume;
            Core2026Proof.Check(afterCast == (spell ? 1 : 0), label + ": spell after-cast fires exactly once after the answer");
            Core2026Proof.Check(q.Hero.Armor == armor + control + (edge ? 1 : 0) && q.Choice == null && !next.Resolution.HasSuspendedEffect,
                label + $": resumed continuation (and its queued work) sees the play-time edge; armor {q.Hero.Armor - armor}");
            Core2026Proof.Check(!nextSource.NativeTags.ContainsKey(Edge) && !next.IdEntityDic[raw.Id].NativeTags.ContainsKey(Edge),
                label + ": root completion clears the play and its raw copies");
            // R3 Electra/Voraxx/Shudderwock-style re-activation of the same entity after completion: no Outcast.
            if (nextSource is Spell s) Generic.CastSpell(q, next, s, null, 0);
            else { nextSource.ActivateTask(PowerActivation.POWER); next.ProcessTasks(); }
            Core2026Proof.Check(q.Choice != null && OutcastRules.PlayedFromEdge(nextSource) == false, label + ": recast suspends without the play");
            Resume(next);
            Core2026Proof.Check(q.Hero.Armor == armor + 2 * control + (edge ? 1 : 0) && !nextSource.NativeTags.ContainsKey(Edge), label + ": recast continuation reads false");
            NoHistory(next);
        }
    }
}
