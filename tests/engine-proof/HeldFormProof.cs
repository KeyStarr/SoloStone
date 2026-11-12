using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HeldFormProof
{
    [GroupProof("b15-held-form")]
    internal static void Run()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        var held = Add(p, "DINO_407");
        End(g); Play(g, p.Opponent, "CS2_182");
        Core2026Proof.Check(held.Card.Id == "CS2_182" && held.Cost == 3 &&
            ((SabberStoneCore.Model.Entities.Minion)held).AttackDamage == 3,
            "held form observes future opponent plays and becomes 3-Cost 3/4");
        SeededEntityPicks();
        OrdinaryHandTransforms();
        InformantDoubleBattlecry();
        var failures = new List<string>();
        foreach (Action probe in new Action[] { Unpinned_ShapeshifterTurnOrder, ComposedRecurrences, ShatterRetainsRecurrence, CastCohortGeneration })
            try { probe(); } catch (Exception error) { failures.Add(error.Message); }
        Core2026Proof.Check(failures.Count == 0, string.Join("; ", failures));
        RewindState();
        CapacityAndHistoryOff();
        Pools();
        DefinitionResetHistory();
        Console.WriteLine("PASS held forms: seeded entity picks, phases, copy/clone/snapshot/Rewind, capacity, history-off and class pools");
    }

    internal static void DefinitionResetHistory()
    {
        foreach (string id in new[] { "DINO_407", "TIME_876", "CORE_RLK_567" })
        {
            var game = HistoryFactsProof.Fixture(); var owner = game.Player1;
            var held = Add(owner, id);
            var form = (Minion)HandProgressRules.TransformHeld(held, Cards.FromId("CS2_182"));
            form.AttackDamage = 1; form.BaseHealth = 1;
            foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
            {
                var source = next.IdEntityDic[form.Id]; int cursor = next.PowerHistory.Full.Count;
                var changed = (Minion)HandProgressRules.TransformHeld(source, Cards.FromId("CS2_200"));
                var history = next.PowerHistory.Full.Skip(cursor).ToArray();
                int change = Array.FindIndex(history, e => e is SabberStoneCore.Kettle.PowerHistoryChangeEntity);
                Core2026Proof.Check(change >= 0 && !history.Take(change).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                    .Any(t => t.EntityId == form.Id && t.Tag is GameTag.ATK or GameTag.HEALTH),
                    id + " replacement stats arrive with CHANGE_ENTITY, not a pre-transform hand buff");
                Core2026Proof.Check(changed.AttackDamage == 6 && changed.BaseHealth == 7 && changed.Zone == next.Player1.HandZone,
                    id + " replacement keeps base stats and hand residence");
            }
        }
        Console.WriteLine("PASS held transform definition stats: all three callers; clone/snapshot");
    }

    private static void InformantDoubleBattlecry()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool brann in new[] { false, true })
        {
            var game = HeraldProof.New(CardClass.ROGUE).CloneExact(history: history);
            var player = game.Player1;
            if (brann) Summon(player, "LOE_077");
            var informant = Add(player, "CATA_614");
            new AddEnchantmentTask("EDR_100t7e", EntityType.SOURCE).Process(game, player, informant, null);
            int hand = player.HandZone.Count - 1;
            PlayEntity(game, player, informant);
            Core2026Proof.Check(game.Resolution.HasSuspendedEffect, "Informant first Discover waits");
            foreach (var next in new[] { game, game.CloneExact(history: history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                for (int pick = 0; pick < 2; pick++)
                {
                    var owner = next.Player1;
                    Core2026Proof.Check(owner.Choice != null && owner.Choice.Choices.Count >= 1, "Informant sequential Discover offer");
                    var decision = next.GetPendingDecisions().Single();
                    Core2026Proof.Check(next.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
                        decision.Id, decision.ControllerId, new[] { owner.Choice.Choices[0] }), out var error),
                        "Informant answer accepted: " + error);
                    Core2026Proof.Check(owner.HandZone.Count == hand + pick + 1, "Informant gives exactly one card per answer");
                }
                Core2026Proof.Check(!next.Resolution.HasSuspendedEffect && next.Player1.Choice == null
                    && next.Player1.NumMinionsPlayedThisTurn == 1,
                    "Informant completes two Battlecries and one outer play, including Brann plus gift");
            }
        }
    }

    private static void OrdinaryHandTransforms()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        foreach (var card in p.Opponent.DeckZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.DeckZone.Remove(card));
        var chameleos = Add(p, "GIL_142"); Add(p.Opponent, "DINO_407");
        End(g); End(g);
        Core2026Proof.Check(chameleos.Card.Id == "DINO_407", "Chameleos becomes the only opposing hand card");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Play(next, next.Player1, "CS2_182");
            Core2026Proof.Check(next.IdEntityDic[chameleos.Id].Card.Id == "DINO_407", "transformed Mirrex ignores owner minion plays");
            End(next); Play(next, next.Player2, "CS2_200");
            var form = (Minion)next.IdEntityDic[chameleos.Id];
            Core2026Proof.Check(form.Card.Id == "CS2_200" && form.Cost == 3 && form.AttackDamage == 3 && form.BaseHealth == 4,
                "ordinary Chameleos transform initializes Mirrex for the next opponent play after clone/load");
        }
        foreach (string source in new[] { "CS2_182", "GAME_005" })
        foreach (bool removeEnchantments in new[] { false, true })
        {
            g = HeraldProof.New(CardClass.ROGUE); p = g.Player1;
            var mirrex = Generic.ChangeEntityBlock(p, Add(p, source), Cards.FromId("DINO_407"), removeEnchantments);
            var demise = Generic.ChangeEntityBlock(p, Add(p, source), Cards.FromId("CORE_RLK_567"), removeEnchantments);
            var rng = g.Random.Clone(); rng.Next(11);
            var previous = Add(p, source);
            previous[GameTag.TAG_SCRIPT_DATA_NUM_1] = 15; previous[GameTag.TAG_SCRIPT_DATA_NUM_2] = 99;
            var informant = Generic.ChangeEntityBlock(p, previous, Cards.FromId("CATA_614"), removeEnchantments);
            Core2026Proof.Check(informant[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0 && informant[GameTag.TAG_SCRIPT_DATA_NUM_2] == 0 &&
                g.Random.CaptureState().Equals(rng.CaptureState()),
                "ordinary same/cross-type transforms initialize selected class exactly once");
            Play(g, p, "GAME_005");
            Core2026Proof.Check(g.IdEntityDic[demise.Id].Card.Id == "GAME_005", "ordinary transforms initialize Shadow of Demise");
            End(g); Play(g, p.Opponent, "CS2_200");
            Core2026Proof.Check(g.IdEntityDic[mirrex.Id].Card.Id == "CS2_200", "ordinary same/cross-type transforms initialize Mirrex");
        }
    }

    private static void CastCohortGeneration()
    {
        foreach (string mode in new[] { "transform", "reset", "upgrade" })
        {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        var held = Add(p, "CORE_RLK_567"); Play(g, p, "JAIL_801", g.Player2.Hero);
        var cohort = HandProgressRules.CaptureCast((Spell)Entity.FromCard(p, Cards.FromId("GAME_005")));
        var actor = held.AppliedEnchantments.Single(e => e.Card.Id == "RLK_567e2");
        if (mode == "upgrade")
        {
            held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
            held = HandProgressRules.Upgrade(held);
        }
        else HandProgressRules.TransformHeld(held, Cards.FromId("CORE_RLK_567"), actor);
        if (mode == "reset")
        {
            p.HandZone.Remove(held); ((Entity)held).Reset(); p.DeckZone.Add(held); Generic.Draw(p, held);
        }
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var form = next.IdEntityDic[held.Id];
            actor = form.AppliedEnchantments.Single(e => e.Card.Id == "RLK_567e2");
            HandProgressRules.TransformHeld(form, Cards.FromId("JAIL_801"), actor);
            HandProgressRules.CompleteCast(next.Player1, cohort.ToArray(), HistoryCastKind.Player, HistoryCastOutcome.Completed);
            Core2026Proof.Check(form[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "an earlier cast cohort cannot advance a later recurring form, mode=" + mode);
        }
        }
    }

    private static void ShatterRetainsRecurrence()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        Add(p, "CS2_182"); Add(p, "CORE_RLK_567");
        var combined = ShatterRules.GiveCombined(p, "CATA_489", p.Hero);
        PlayEntity(g, p, combined, g.Player2.Hero);
        var halves = p.HandZone.Where(c => c.Card.Id == "CATA_489t" || c.Card.Id == "CATA_489t2").ToArray();
        Core2026Proof.Check(halves.Length == 2 && halves.All(c => c.AppliedEnchantments?.Count(e => e.Card.Id == "RLK_567e2") == 1),
            "copying a Shatter spell enrolls recurrence before deferred fragment creation");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Play(next, next.Player1, "GAME_005");
            Core2026Proof.Check(halves.All(c => next.IdEntityDic[c.Id].Card.Id == "GAME_005"), "both fragment identities continue transforming independently");
        }
    }

    private static void ComposedRecurrences()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        foreach (var c in g.Player2.DeckZone.ToArray()) g.Player2.SetasideZone.Add(g.Player2.DeckZone.Remove(c));
        var held = Add(p, "TIME_876"); Add(p.Opponent, "DINO_407"); End(g); End(g);
        Core2026Proof.Check(held.AppliedEnchantments.Count(e => e.Card.CopyHandTrigger) == 2, "copied Mirrex has its intrinsic hand ability and original Shapeshifter recurrence");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            End(next); Play(next, next.Player2, "CS2_200");
            var form = (Minion)next.IdEntityDic[held.Id];
            Core2026Proof.Check(form.Card.Id == "CS2_200" && form.AttackDamage == 3 && form.BaseHealth == 4 &&
                form.AppliedEnchantments.Any(e => e.Card.Id == "TIME_876e"), "Mirrex transform cannot erase original Shapeshifter recurrence");
            Play(next, next.Player2, "TIME_876");
            foreach (var c in next.Player2.HandZone.ToArray()) next.Player2.SetasideZone.Add(next.Player2.HandZone.Remove(c));
            Add(next.Player2, "CS2_182");
            var rng = next.Random.Clone(); rng.Next(1); End(next);
            Core2026Proof.Check(form.Card.Id == "CS2_182" && form.AttackDamage == 4 && form.BaseHealth == 5 &&
                form.AppliedEnchantments.Any(e => e.Card.Id == "DINO_407e") && next.Random.CaptureState().Equals(rng.CaptureState()),
                "Shapeshifter rerolls once, clears Crystalline stats and retains both recurring identities: " + form.Card.Id + " " + form.AttackDamage + "/" + form.BaseHealth + " retained=" + form.AppliedEnchantments.Any(e => e.Card.Id == "DINO_407e") + " rng=" + next.Random.CaptureState().Equals(rng.CaptureState()));
        }
    }

    private static void SeededEntityPicks()
    {
        var results = new HashSet<string>();
        for (int seed = 1; seed <= 8; seed++)
        {
            var game = HeraldProof.New(CardClass.ROGUE, seed); var owner = game.Player1;
            var shape = Add(owner, "TIME_876");
            foreach (var c in game.Player2.DeckZone.ToArray()) game.Player2.SetasideZone.Add(game.Player2.DeckZone.Remove(c));
            var candidates = new[] { Add(game.Player2, "CS2_200"), Add(game.Player2, "CS2_182"), Add(game.Player2, "CS2_200") };
            var rng = game.Random.Clone(); int index = rng.Next(3);
            string expected = candidates[index].Card.Id;
            foreach (var next in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                End(next); End(next);
                Core2026Proof.Check(next.IdEntityDic[shape.Id].Card.Id == expected && next.Random.CaptureState().Equals(rng.CaptureState()),
                    "seeded pick uses opposing minion entities in hand order and exactly one RNG draw, seed=" + seed);
            }
            results.Add(expected);
        }
        Core2026Proof.Check(results.Count == 2, "seeded sample exercises both definitions");
    }

    private static void Unpinned_ShapeshifterTurnOrder()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        var held = Add(p, "TIME_876");
        foreach (var c in g.Player2.DeckZone.ToArray()) g.Player2.SetasideZone.Add(g.Player2.DeckZone.Remove(c));
        // A later-registered battlefield callback queues a hand change, rather than mutating during notification.
        SabberStoneCore.Model.TriggerManager.TriggerHandler later = source => {
            if (source != p) return;
            g.TaskQueue.Enqueue(new CustomTask((game, c, s, t, stack) => Add(c.Opponent, "CS2_200")), p, p.Hero, null);
        };
        g.TriggerManager.TurnStartTrigger += later;
        End(g); End(g);
        Core2026Proof.Check(held.Card.Id == "CS2_200", "Unpinned_ShapeshifterTurnOrder: queued earlier phase drains before initial hand trigger");
        g.TriggerManager.TurnStartTrigger -= later;
        // The recurring enchantment is in the attached phase, before a later hand callback.
        bool observed = false;
        foreach (var c in p.Opponent.HandZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(c));
        Add(p.Opponent, "CS2_182");
        var original = Cards.FromId("CATA_614").Power;
        try
        {
            Cards.FromId("CATA_614").Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_START) {
                TriggerActivation = TriggerActivation.HAND,
                SingleTask = new CustomTask((game, c, s, t, stack) => observed = game.IdEntityDic[held.Id].Card.Id == "CS2_182") } };
            Add(p, "CATA_614"); End(g); End(g);
            Core2026Proof.Check(observed, "later Shapeshifter enchantment resolves in attached phase before hand-card phase");
        }
        finally { Cards.FromId("CATA_614").Power = original; }
        PhaseSourceMovesToHand();
    }

    private static void PhaseSourceMovesToHand()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        foreach (var c in g.Player2.DeckZone.ToArray()) g.Player2.SetasideZone.Add(g.Player2.DeckZone.Remove(c));
        Add(p, "TIME_876");
        var boardPower = Cards.FromId("CS2_182").Power; var handPower = Cards.FromId("CATA_614").Power;
        bool observed = false;
        try
        {
            Cards.FromId("CS2_182").Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_START) {
                FastExecution = true,
                SingleTask = new CustomTask((game, c, s, t, stack) => {
                    Generic.ReturnToHandBlock(c, (Minion)s);
                    game.TaskQueue.Enqueue(new CustomTask((inner, owner, source, target, values) => Add(owner.Opponent, "CS2_200")), c, (IPlayable)s, null);
                }) } };
            Cards.FromId("CATA_614").Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_START) {
                TriggerActivation = TriggerActivation.HAND, FastExecution = true,
                SingleTask = new CustomTask((game, c, s, t, stack) => observed = c.Opponent.HandZone.Any(e => e.Card.Id == "CS2_200")) } };
            Summon(p, "CS2_182"); Add(p, "CATA_614"); End(g); End(g);
            Core2026Proof.Check(observed, "phase boundary stays frozen when earlier battlefield source returns to hand");
        }
        finally { Cards.FromId("CS2_182").Power = boardPower; Cards.FromId("CATA_614").Power = handPower; }
    }

    private static void RewindState()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        var demise = Add(p, "CORE_RLK_567"); var mirrex = Add(p, "DINO_407"); var shape = Add(p, "TIME_876");
        var informant = Add(p, "CATA_614"); int selected = informant[GameTag.TAG_SCRIPT_DATA_NUM_1];
        var construct = Summon(p, "BE_254");
        var fire = Add(p, "CS2_029"); fire[GameTag.REWIND] = 1;
        PlayEntity(g, p, fire, g.Player2.Hero);
        Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(g), "marked real spell offers Rewind");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            RewindTestHelpers.AnswerRewind(next);
            Core2026Proof.Check(next.IdEntityDic[construct.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 11 &&
                next.IdEntityDic[demise.Id].Card.Id == "CS2_029" &&
                next.IdEntityDic[demise.Id].AppliedEnchantments.Count(e => e.Card.Id == "RLK_567e2") == 1 &&
                next.IdEntityDic[mirrex.Id].AppliedEnchantments.Count(e => e.Card.Id == "DINO_407e") == 1 &&
                next.IdEntityDic[informant.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == selected &&
                next.IdEntityDic[shape.Id].Card.Id == "TIME_876", "Rewind replays payment/transform once and retains independent held state");
            RewindTestHelpers.KeepIfOffered(next);
        }
    }

    private static void CapacityAndHistoryOff()
    {
        var g = HeraldProof.New(CardClass.ROGUE); var p = g.Player1;
        var held = Add(p, "CORE_RLK_567");
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        var spell = Add(p, "GAME_005");
        PlayEntity(g, p, spell);
        Core2026Proof.Check(p.HandZone.Count == 9 && held.Card.Id == "GAME_005" && held.ZonePosition == 0,
            "full hand transformation consumes no extra slot");
        StandardSetsProof.ClearBoard(g, p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        // Direct Battlecry body with one remaining slot after the Isiset body occupies the sixth.
        while (p.HandZone.Count >= 9) p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.Last()));
        Play(g, p, "BE_259");
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "BE_254") == 1, "Construct summons honor remaining board space");
        g = HeraldProof.New(CardClass.ROGUE).CloneExact(history: false); p = g.Player1;
        var mirrex = (Minion)Add(p, "DINO_407"); End(g); Play(g, p.Opponent, "CS2_182"); End(g);
        PlayEntity(g, p, mirrex); mirrex.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(mirrex.AttackDamage == 4 && mirrex.BaseHealth == 5, "history-off Crystalline remains a removable layer");
    }

    private static void Pools()
    {
        var g = HeraldProof.New(CardClass.ROGUE);
        foreach (string name in PoolRequirement.PlayableClasses)
        {
            CardClass cls = name == "DEMONHUNTER" ? (CardClass)14 : Enum.Parse<CardClass>(name);
            var filter = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: cls, type: CardType.SPELL);
            int count = OfflineCardPools.Candidates(g, CardClass.ROGUE, filter, 123385).Count;
            Core2026Proof.Check(count >= 3, "selected-class Discover pool has three candidates: " + name);
            Console.WriteLine("POOL CATA_614 " + name + "=" + count);
        }
    }
}
