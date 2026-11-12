using PegasusGame;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineSpell = SabberStoneCore.Model.Entities.Spell;
using EngineGame = SabberStoneCore.Model.Game;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static TransportInvariants.SelfChecks;
using EngineHistory = SabberStoneCore.Kettle;
using NativeHistory = PegasusGame.PowerHistory;

namespace TransportInvariants;

internal static class HandCardStateChecks
{
    private static List<PowerHistoryData> Decode(NativeSession session, int from)
    {
        var result = new List<PowerHistoryData>();
        foreach (var packet in session.Packets.Skip(from).Where(p => p.Id == 19))
        {
            using var stream = new MemoryStream(Convert.FromBase64String(packet.Base64));
            var history = new NativeHistory(); history.Deserialize(stream); result.AddRange(history.List);
        }
        return result;
    }
    private static bool ReferencesFollowFull(IEnumerable<PowerHistoryData> entries, HashSet<int> created)
    {
        var full = new HashSet<int>();
        foreach (var e in entries)
        {
            if (e.HasFullEntity) { full.Add(e.FullEntity.Entity); continue; }
            int id = e.HasShowEntity ? e.ShowEntity.Entity : e.HasChangeEntity ? e.ChangeEntity.Entity : e.HasTagChange ? e.TagChange.Entity : e.HasHideEntity ? e.HideEntity.Entity : 0;
            if (created.Contains(id) && !full.Contains(id)) return false;
        }
        return created.IsSubsetOf(full);
    }
    private static bool Private(IEnumerable<PowerHistoryData> entries, HashSet<int> hidden)
    {
        bool Structural(int tag) => tag is 53 or 50 or 49 or 263;
        var rows = entries.ToList();
        bool ShatterIndex(int at)
        {
            var tag = rows[at].TagChange;
            if (tag.Tag != 1436) return false;
            for (int ordinal = 0; ordinal < 2; ordinal++)
            {
                bool Match(int pulse) => pulse >= 0 && pulse + 1 < rows.Count
                    && rows[pulse].HasSubSpellStart && rows[pulse + 1].HasSubSpellEnd
                    && rows[pulse].SubSpellStart.SpellPrefabGuid == "2795f3f54446b6c45a96f1981cda0df8"
                    && rows[pulse].SubSpellStart.TargetEntityIds.Count == 2
                    && rows[pulse].SubSpellStart.TargetEntityIds[ordinal] == tag.Entity;
                if (tag.Value == ordinal && Match(at + 2 - ordinal) || tag.Value == 0 && Match(at - 2 - ordinal)) return true;
            }
            return false;
        }
        for (int index = 0; index < rows.Count; index++)
        {
            var e = rows[index];
            var snapshot = e.HasFullEntity ? e.FullEntity : e.HasShowEntity ? e.ShowEntity : e.HasChangeEntity ? e.ChangeEntity : null;
            if (snapshot != null && hidden.Contains(snapshot.Entity) &&
                (snapshot.Name.Length != 0 || snapshot.Tags.Any(t => !Structural(t.Name)))) return false;
            if (e.HasTagChange && hidden.Contains(e.TagChange.Entity) && !Structural(e.TagChange.Tag) && !ShatterIndex(index)) return false;
            if (snapshot?.Tags.Any(t => ShatterRules.IsInternalTag((GameTag)t.Name) || HandProgressRules.IsInternalTag((GameTag)t.Name)) == true) return false;
        }
        return true;
    }
    internal static void Run(bool shatterOnly = false)
    {
        ShatterPredictedPlay();
        if (!shatterOnly)
        {
            TransformRoundTrip();
            LifecycleTagAudit();
            AllAttachmentPrivacy();
        }
        var game = OfflinePoolTestRecipes.NewGame(); var p = game.Player1;
        foreach (var c in new[] { p, p.Opponent })
            foreach (var held in c.HandZone.ToArray()) c.SetasideZone.Add(c.HandZone.Remove(held));
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
        int firstPacket = session.Packets.Count;
        Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Generic.DrawCard(p, Cards.FromId("CATA_489"));
        Generic.DrawCard(p.Opponent, Cards.FromId("CATA_489")); // split and immediate join in same history batch
        session.Flush(); game.PowerHistory.Last.Clear();
        var family = game.IdEntityDic.Values.Where(c => c.Card.Shatter != null).ToArray();
        var created = family.Select(c => c.Id).ToHashSet();
        var hidden = family.Where(c => c.Controller == p.Opponent).Select(c => c.Id).ToHashSet();
        var entries = Decode(session, firstPacket);
        Check(ReferencesFollowFull(entries, created), "card-state FULL before every reference, including retired halves");
        int half = family.First(c => c.Card.Id == "CATA_489t").Id;
        Check(!ReferencesFollowFull(entries.Where(e => !e.HasFullEntity || e.FullEntity.Entity != half), created), "missing-FULL fault rejected despite ClientModel auto-create");
        Check(Private(entries, hidden), "retired bot parent/half INVALID fallback cannot reveal identity or links");
        var injected = entries.Concat(new[] { new PowerHistoryData { TagChange = new PowerHistoryTagChange { Entity = hidden.First(), Tag = 4239, Value = 1 } } });
        Check(!Private(injected, hidden), "secret-tag fault rejected");
        Check(!Private(entries.Concat(new[] { new PowerHistoryData { TagChange = new PowerHistoryTagChange {
            Entity = hidden.First(), Tag = 1436, Value = 1 } } }), hidden), "unscoped Shatter index fault rejected");
        Check(!Invariants.Compare(game, session.Client).Any(), "split strict hand actors/cost/positions");
        // Missing position update has a separate oracle from missing identity.
        var actor = p.HandZone.First(); int position = session.Client.Entities[actor.Id].Get(GameTag.ZONE_POSITION);
        session.Client.Entities[actor.Id].Tags[263] = position + 1;
        Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == actor.Id && v.Tag == "ZONE_POSITION"), "position fault rejected");
        session.Client.Entities[actor.Id].Tags[263] = position;
        var separator = p.HandZone.Single(c => c.Card.Shatter == null);
        p.SetasideZone.Add(p.HandZone.Remove(separator)); session.Flush(); game.PowerHistory.Last.Clear();
        Check(!Invariants.Compare(game, session.Client).Any(), "join has no retired actor ghosts");
        if (shatterOnly) { Console.WriteLine("PASS Shatter hand-state transport: ordering, privacy, fault injection and strict parity"); return; }
        foreach (var owner in new[] { p, p.Opponent })
        {
            int start = session.Packets.Count;
            var held = Generic.DrawCard(owner, Cards.FromId("JAIL_801"));
            session.Flush(); game.PowerHistory.Last.Clear();
            var before = session.Client.Copy();
            held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            var spell = (EngineSpell)EngineEntity.FromCard(owner, Cards.FromId("GAME_005"));
            var cohort = HandProgressRules.CaptureCast(spell);
            HandProgressRules.CompleteCast(owner, cohort, HistoryCastKind.Player, HistoryCastOutcome.Completed);
            var target = game.IdEntityDic[held.Id];
            var historical = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
            target.Cost = 9; // Later mutation must not rewrite captured upgrade payload.
            session.Invoke("SendHistory", historical);
            var shown = session.Client.Entities[held.Id];
            if (owner == p)
            {
                Check(shown.CardId == "JAIL_801t" && shown.Get(GameTag.COST) == 3 && shown.Get(GameTag.ATK) == 3 &&
                    shown.Get(GameTag.HEALTH) == 3 && shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0 && shown.Get(GameTag.SPELL_SCHOOL) == 0,
                    "historical cross-type CHANGE has same id/slot, printed stats/cost, explicit old zeros");
                var wire = Decode(session, start);
                var missing = before.Copy(); missing.Apply(new NativeHistory { List = wire.Where(e => !e.HasChangeEntity).ToList() });
                Check(Invariants.Compare(game, missing).Any(v => v.Entity == target.Id && v.Tag == "CARD_ID"), "missing-CHANGE fault rejected");
                var clear = before.Copy();
                var altered = wire.Where(e => !(e.HasTagChange && e.TagChange.Entity == target.Id && e.TagChange.Tag == 2 && e.TagChange.Value == 0)).ToList();
                foreach (var e in altered.Where(e => e.HasChangeEntity && e.ChangeEntity.Entity == target.Id)) e.ChangeEntity.Tags.RemoveAll(t => t.Name == 2);
                clear.Apply(new NativeHistory { List = altered });
                Check(Invariants.Compare(game, clear).Any(v => v.Entity == target.Id && v.Tag == GameTag.TAG_SCRIPT_DATA_NUM_1.ToString()), "missing-zero fault rejected");
            }
            else Check(Private(Decode(session, start), new HashSet<int> { target.Id }), "hidden progress/upgrade contains structural tags only");
            target.Cost = 3; game.PowerHistory.Last.Clear();
        }
        // Grant, retain through cross-type upgrade, then flush once: final definition has no declaration.
        foreach (var owner in new[] { p, p.Opponent })
        {
            int start = session.Packets.Count;
            var held = Generic.DrawCard(owner, Cards.FromId("JAIL_801"));
            Generic.AddEnchantmentBlock(game, Cards.FromId("CS2_087e"), owner.Hero, held);
            int attachment = held.AppliedEnchantments.Single().Id;
            held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            var cast = (EngineSpell)EngineEntity.FromCard(owner, Cards.FromId("GAME_005"));
            HandProgressRules.CompleteCast(owner, HandProgressRules.CaptureCast(cast), HistoryCastKind.Player, HistoryCastOutcome.Completed);
            session.Flush(); game.PowerHistory.Last.Clear();
            var wire = Decode(session, start);
            if (owner == p.Opponent)
                Check(Private(wire, new HashSet<int> { held.Id, attachment }), "same-batch upgrade keeps opponent attachment SHOW identity and ATTACHED private");
            else
                Check(wire.Any(e => e.HasShowEntity && e.ShowEntity.Entity == attachment && e.ShowEntity.Name == "CS2_087e" &&
                    e.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == held.Id)), "owner sees retained attachment across same-batch upgrade");
        }
        Console.WriteLine("PASS SameBatchUpgradeAttachmentPrivacy: Unpinned_HandProjection");
        foreach (var owner in new[] { p, p.Opponent })
        {
            var held = Generic.DrawCard(owner, Cards.FromId("JAIL_801"));
            held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            var cast = (EngineSpell)EngineEntity.FromCard(owner, Cards.FromId("GAME_005"));
            HandProgressRules.CompleteCast(owner, HandProgressRules.CaptureCast(cast), HistoryCastKind.Player, HistoryCastOutcome.Completed);
            session.Flush(); game.PowerHistory.Last.Clear();
            foreach (var next in new[] { game, game.CloneExact(history: true), EngineGame.LoadSnapshot(game.SaveSnapshot()) })
                LaterAttachments(next, held.Id);
        }
        Console.WriteLine("PASS PostUpgradeAttachmentPrivacy: handbuff/copy/bounce packets, both views, clone/restore; Unpinned_HandProjection");
        OrdinarySpellChoice();
        Console.WriteLine("PASS card-state transport: serialized split/join/upgrade, retired-half privacy, immutable history; missing FULL/CHANGE/position/zero and secret-tag faults");
    }

    private static void ShatterPredictedPlay()
    {
        foreach (string parent in new[] { "CATA_134", "CATA_306", "CATA_479", "CATA_489", "CATA_820" })
        {
            var game = OfflinePoolTestRecipes.NewGame(); var owner = game.Player1;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray()) owner.SetasideZone.Add(old.Zone.Remove(old));
            owner.BaseMana = 10;
            var family = Cards.FromId(parent).Shatter;
            var left = EngineEntity.FromCard(owner, Cards.FromId(family.FirstHalfId), zone: owner.HandZone);
            var bola = EngineEntity.FromCard(owner, Cards.FromId("JAIL_377"), zone: owner.HandZone);
            var right = EngineEntity.FromCard(owner, Cards.FromId(family.SecondHalfId), zone: owner.HandZone);
            foreach (string id in new[] { "CATA_478", "JAIL_326", "CATA_475", "TIME_015", "CORE_BT_292" })
                EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            var drawn = EngineEntity.FromCard(owner, Cards.FromId("CATA_473"), zone: owner.DeckZone);
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
            session.Client.PredictLocalSpellPlay(bola.Id);
            session.Process(PlayCardTask.Any(owner, bola));
            var combined = owner.HandZone.Single(c => c.Card.Id == parent);
            Check(!session.Client.HasUnresolvedLocalChange, parent + " native local play acknowledged");
            Check(!session.Client.IgnoredCardZoneChanges.Contains(combined.Id), parent + " combined hand actor survives predicted PLAY suppression");
            Check(!session.Client.IgnoredCardZoneChanges.Contains(left.Id) && !session.Client.IgnoredCardZoneChanges.Contains(right.Id),
                parent + " half retirement survives predicted PLAY suppression");
            Check(owner.HandZone.Last() == drawn && owner.DeckZone.Count == 0 && owner.HandZone.Count == 7, parent + " exact high-cost draw/merge result");
            Check(!Invariants.Compare(game, session.Client).Any(), parent + " serialized ClientModel logical hand parity");
        }
        Console.WriteLine("PASS ShatterPredictedPlay: five families; Holy Bola/Nozdormu, local prediction suppression and strict logical parity");
    }

    private static void TransformRoundTrip()
    {
        foreach (string id in new[] { "JAIL_801", "CATA_489t", "CATA_489t2", "CS2_120", "TIME_046" })
        foreach (string through in id == "TIME_046" ? new[] { "CS2_182" } : new[] { "CS2_029", "CS2_182" })
        foreach (bool opponent in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame(); var owner = opponent ? game.Player2 : game.Player1;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            NativeSession session = null;
            var match = new NativeMatch((type, packet) => session.Receive(type, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
            IPlayable card = Generic.DrawCard(owner, Cards.FromId(id));
            int helperId;
            if (id == "TIME_046")
            {
                owner.HandZone.Remove(card); Generic.SummonBlock(game, (Minion)card, -1, owner.Hero);
                helperId = DormantRules.SleepGeneration((Minion)card);
                DormantRules.Accelerate((Minion)card, 1);
            }
            else
            {
                TemporaryRules.Make(card); helperId = card.AppliedEnchantments.Single().Id;
                if (id == "JAIL_801") card[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            }
            session.Flush(); game.PowerHistory.Last.Clear();
            int start = session.Packets.Count;
            string label = $"TransformRoundTripTransport {id}/{through}/{opponent}: ";
            card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(through), true);
            session.Flush(); game.PowerHistory.Last.Clear();
            foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, (GameTag)401, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
                Check(card[tag] == 0 && (opponent && id != "TIME_046" || session.Client.Entities[card.Id].Get(tag) == 0),
                    label + "outbound clear " + tag);
            var violations = Invariants.Compare(game, session.Client).ToArray();
            Check(violations.Length == 0, label + "outbound strict parity: " + string.Join("; ", violations.Select(v => v.ToString())));
            card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(id), true);
            session.Flush(); game.PowerHistory.Last.Clear();
            foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, (GameTag)401, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
            {
                int expected = id == "TIME_046" && tag == GameTag.TAG_SCRIPT_DATA_NUM_1 ? 3 : card.Card[tag];
                Check(card[tag] == expected, label + "returned engine " + tag + "=" + expected);
                if (!opponent || id == "TIME_046") Check(session.Client.Entities[card.Id].Get(tag) == expected,
                    label + "returned serialized " + tag + "=" + expected);
            }
            if (id == "TIME_046")
                Check(((Minion)card).IsDormant && DormantRules.TurnsRemaining((Minion)card) == 3 &&
                    DormantRules.SleepGeneration((Minion)card) != helperId && game.IdEntityDic[helperId][GameTag.ATTACHED] == 0,
                    label + "new sleep and retired helper");
            else
            {
                Check(TemporaryRules.IsTemporary(card) && card.AppliedEnchantments.Single().Id == helperId &&
                    card.AppliedEnchantments.Single().Target == card, label + "Temporary helper survives both legs once");
                if (opponent) Check(Private(Decode(session, start), new HashSet<int> { card.Id, helperId }), label + "hidden round-trip privacy");
            }
            Check(!Invariants.Compare(game, session.Client).Any(), label + "returned strict parity");
            if (id == "JAIL_801")
            {
                var cast = (EngineSpell)EngineEntity.FromCard(owner, Cards.FromId("GAME_005"));
                for (int remaining = 2; remaining >= 0; remaining--)
                {
                    HandProgressRules.CompleteCast(owner, HandProgressRules.CaptureCast(cast), HistoryCastKind.Player, HistoryCastOutcome.Completed);
                    session.Flush(); game.PowerHistory.Last.Clear();
                    Check(game.IdEntityDic[card.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == remaining &&
                        (opponent || session.Client.Entities[card.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == remaining), label + "later progress " + remaining);
                }
                Check(game.IdEntityDic[card.Id].Card.Id == "JAIL_801t", label + "later upgrade");
                Check(!Invariants.Compare(game, session.Client).Any(), label + "upgrade strict parity");
            }
            var replay = new ClientModel(); replay.Apply(new NativeHistory { List = Decode(session, 0) });
            Check(!Invariants.Compare(game, replay).Any(), label + "fresh serialized full-stream replay");
        }
        Console.WriteLine("PASS CardStateTransformRoundTripTransport: serialized outbound clears/returned tags/later upgrade; Temporary/both Shatter halves/board Dormant; both views; replay; " +
            "Unpinned_HandFresh Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_HandProjection Unpinned_TemporaryResidence Unpinned_TemporaryProjection Unpinned_ShatterPermanent Unpinned_ShatterProjection Unpinned_DormantLifecycle Unpinned_DormantCounterProjection");
    }

    private static void LifecycleTagAudit()
    {
        foreach (string id in new[] { "CS2_182", "JAIL_801", "CATA_489t", "CATA_489", "TIME_046" })
        foreach (string move in new[] { "shuffle", "CS2_120", "CS2_029" })
        {
            var game = OfflinePoolTestRecipes.NewGame(); var owner = game.Player1;
            NativeSession session = null;
            var match = new NativeMatch((type, packet) => session.Receive(type, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match); session.Flush(); game.PowerHistory.Last.Clear();
            var card = id == "CATA_489" ? ShatterRules.GiveCombined(owner, id, owner.Hero)
                : Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            if (id == "TIME_046")
            {
                owner.HandZone.Remove(card); Generic.SummonBlock(game, (Minion)card, -1, owner.Hero);
                card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(id), true);
                Check(DormantRules.Awaken((Minion)card) && Generic.ReturnToHandBlock(owner, (Minion)card), "declared board transform, awaken and bounce");
            }
            else if (id != "CATA_489" && id != "CS2_182")
                card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(id), true);
            TemporaryRules.Make(card);
            if (id == "JAIL_801") card[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            session.Flush(); game.PowerHistory.Last.Clear();
            if (move == "shuffle")
            {
                owner.HandZone.Remove(card); card.Reset();
                Check(Generic.ShuffleIntoDeck(owner, owner.Hero, card), "card-state shuffle");
                session.Flush(); game.PowerHistory.Last.Clear();
                Generic.AddHandPhase(owner, owner.DeckZone.Remove(card));
            }
            else card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(move), true);
            session.Flush(); game.PowerHistory.Last.Clear();
            var shown = session.Client.Entities[card.Id];
            string label = $"card-state lifecycle {id}/{move}";
            Check(shown.Get(GameTag.GHOSTLY) == (move == "shuffle" ? 0 : 1)
                && card[TemporaryRules.Temporary] == (move == "shuffle" ? 0 : 1), label + " Temporary clear/retention");
            foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, (GameTag)401, (GameTag)2889, (GameTag)1719,
                (GameTag)1078, (GameTag)1452, (GameTag)1652, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
            {
                Check(shown.Get(tag) == card[tag], label + " " + tag + " engine=" + card[tag] + " client=" + shown.Get(tag));
                if (move != "shuffle") Check(card[tag] == 0, label + " old tag retired: " + tag);
            }
            if (move == "shuffle" && id == "JAIL_801") Check(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, label + " fresh residence counter");
            Check(!Invariants.Compare(game, session.Client).Any(), label + " strict parity");
        }
        Console.WriteLine("PASS CardStateLifecycleTagAudit: Dormant/Temporary/hand-progress/Shatter shuffle/reveal and same/cross-type transform; "
            + "Unpinned_DormantLifecycle Unpinned_TemporaryResidence Unpinned_HandFresh Unpinned_ShatterPermanent Unpinned_TemporaryProjection Unpinned_HandProjection Unpinned_ShatterProjection");
    }

    private static void AllAttachmentPrivacy()
    {
        foreach (string id in new[] { "CS2_182", "CATA_489t", "JAIL_801" })
        foreach (bool markAfterBuff in new[] { false, true })
        foreach (bool transform in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame();
            NativeSession session = null;
            var match = new NativeMatch((type, packet) => session.Receive(type, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
            int start = session.Packets.Count;
            var hidden = new HashSet<int>();
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                var target = Generic.DrawCard(owner, Cards.FromId(id));
                if (!markAfterBuff) TemporaryRules.Make(target);
                if (target is Minion) Generic.AddEnchantmentBlock(game, Cards.FromId("CFM_305e"), owner.Hero, target);
                else SabberStoneCore.Model.Entities.Enchantment.GetInstance(owner, owner.Hero, target, Cards.FromId("CFM_305e"));
                if (markAfterBuff) TemporaryRules.Make(target);
                if (transform) target = Generic.ChangeEntityBlock(owner, target, Cards.FromId("CS2_120"), true);
                if (target is Minion) Generic.AddEnchantmentBlock(game, Cards.FromId("CFM_305e"), owner.Hero, target);
                else SabberStoneCore.Model.Entities.Enchantment.GetInstance(owner, owner.Hero, target, Cards.FromId("CFM_305e"));
                if (owner == game.Player2)
                {
                    hidden.Add(target.Id);
                    hidden.UnionWith(game.IdEntityDic.Values.OfType<SabberStoneCore.Model.Entities.Enchantment>().Where(e => e.Target.Id == target.Id).Select(e => e.Id));
                }
            }
            session.Flush(); game.PowerHistory.Last.Clear();
            Check(Private(Decode(session, start), hidden),
                "Round3AttachmentPrivacy all attachments private before/after marking and definition loss: " + id + "/" + markAfterBuff + "/" + transform);
            Check(!Invariants.Compare(game, session.Client).Any(), "Round3AttachmentPrivacy strict serialized parity");
        }
        Console.WriteLine("PASS Round3AttachmentPrivacy: serialized Temporary/Shatter/hand-progress handbuffs; earlier/later/retired attachments; transform");
    }

    private static void LaterAttachments(EngineGame game, int targetId)
    {
        var target = game.IdEntityDic[targetId]; var owner = target.Controller;
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
        for (int step = 0; step < 3; step++)
        {
            int start = session.Packets.Count;
            if (step == 1) target = Generic.Copy(owner, owner.Hero, target, SabberStoneCore.Enums.Zone.HAND);
            if (step == 2)
            {
                owner.HandZone.Remove(target); Generic.SummonBlock(game, (Minion)target, -1, null);
                game.DeathProcessingAndAuraUpdate(); Generic.ReturnToHandBlock(owner, (Minion)target);
                session.Flush(); game.PowerHistory.Last.Clear(); start = session.Packets.Count;
            }
            Generic.AddEnchantmentBlock(game, Cards.FromId("CFM_305e"), owner.Hero, target);
            var attachments = target.AppliedEnchantments.Select(e => e.Id).ToHashSet();
            session.Flush(); game.PowerHistory.Last.Clear();
            var wire = Decode(session, start);
            if (owner == game.Player2)
            {
                attachments.Add(target.Id);
                Check(Private(wire, attachments), "post-upgrade handbuff hides CFM_305e and ATTACHED target, step=" + step);
            }
            else
                Check(wire.Any(e => e.HasShowEntity && attachments.Contains(e.ShowEntity.Entity) && e.ShowEntity.Name == "CFM_305e" &&
                    e.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == target.Id)), "owner sees post-upgrade handbuff attachment, step=" + step);
        }
    }

    private static void OrdinarySpellChoice()
    {
        var game = OfflinePoolTestRecipes.NewGame(); var owner = game.CurrentPlayer;
        foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
        owner.BaseMana = 10;
        EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.DeckZone);
        var target = Generic.DrawCard(owner, Cards.FromId("JAIL_801")); target[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        var spell = Generic.DrawCard(owner, Cards.FromId("EDR_528"));
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush(); game.PowerHistory.Last.Clear();
        session.Process(PlayCardTask.Any(owner, spell)); session.PublishDecision(owner);
        Check(owner.Choice != null && session.Client.Entities[target.Id].CardId == "JAIL_801" &&
            session.Client.Entities[target.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 1, "ordinary Discover packets do not upgrade before answer");
        session.Process(ChooseTask.Pick(owner, owner.Choice.Choices[0]));
        Check(session.Client.Entities[target.Id].CardId == "JAIL_801t" && !Invariants.Compare(game, session.Client).Any(),
            "ordinary Discover answer publishes exactly completed current hand form with strict parity");
        Console.WriteLine("PASS OrdinarySpellChoiceTransport: pending counter-1, final-answer CHANGE, strict parity");
    }
}
