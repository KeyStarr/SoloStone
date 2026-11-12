using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace TransportInvariants;
using Zone = SabberStoneCore.Enums.Zone;

internal static class DarkGiftProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Dark Gift " + message);
    private static IEnumerable<PowerHistoryData> History(IEnumerable<WirePacket> packets)
    {
        foreach (var packet in packets.Where(p => p.Id == 19))
        {
            using var stream = new MemoryStream(Convert.FromBase64String(packet.Base64));
            var history = new PegasusGame.PowerHistory();
            history.Deserialize(stream);
            foreach (var entry in history.List) yield return entry;
        }
    }
    private static PegasusGame.PowerHistoryEntity Snapshot(PowerHistoryData entry) =>
        entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity :
        entry.HasChangeEntity ? entry.ChangeEntity : null;

    // Inspect every wire entry, not only final actors: a later HIDE cannot undo an identity leak.
    private static void Private(NativeSession session, IEnumerable<int> ids, int since = 0)
    {
        var hidden = ids.ToHashSet();
        foreach (var entry in History(session.Packets.Skip(since)))
        {
            var snapshot = Snapshot(entry);
            if (snapshot != null && hidden.Contains(snapshot.Entity))
                Check(snapshot.Name == "" && snapshot.Tags.All(t => t.Name is 53 or 50 or 49 or 263),
                    "hidden graph snapshot identity/details: " + snapshot.Entity + ":" + snapshot.Name
                    + ":" + string.Join(",", snapshot.Tags.Select(t => t.Name + "=" + t.Value)));
            if (entry.HasTagChange && hidden.Contains(entry.TagChange.Entity))
                Check(entry.TagChange.Tag is 53 or 50 or 49 or 263,
                    "hidden graph tag/link: " + entry.TagChange.Entity + ":" + entry.TagChange.Tag);
            if (entry.HasPowerStart)
                Check(entry.PowerStart.EffectCardId?.StartsWith("EDR_100t", StringComparison.Ordinal) != true, "hidden effect card id");
        }
        foreach (int id in hidden)
            if (session.Client.Entities.TryGetValue(id, out var actor))
                Check(actor.CardId == "" && actor.Get(GameTag.DARK_GIFT_ENTITY) == 0,
                    "hidden final actor identity/link");
    }

    private static void Discover(string mode, bool opponent, bool sweetDreamsCase = false)
    {
        // Both perspectives need a supported Herald profile when the expanded pool offers a Herald.
        var game = OfflinePoolTestRecipes.NewGame(opponent, hero: CardClass.WARRIOR);
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        session.Flush(); game.PowerHistory.Last.Clear();
        var player = game.CurrentPlayer;
        var source = player.Hero;
        var context = new EffectContext(game, player.Id, source.Id);
        // Seed 22 pairs the first fixed-card/deck offer with Sweet Dreams for either chooser.
        if (sweetDreamsCase) game.SetRandomSeed(22);
        bool opened = mode switch {
            "pool" => context.DiscoverWithDarkGift(OfflinePoolDeclarations.FourCostMinion(),
                recipeDbfId: 122641),
            "deck" => context.DiscoverEntitiesWithDarkGift(player.DeckZone.Take(3).ToArray()),
            _ => context.DiscoverWithDarkGift(new[] { Cards.FromId("CS2_182"), Cards.FromId("CS2_231"), Cards.FromId("EX1_028") })
        };
        Check(opened, mode + " opens");
        var choice = player.Choice;
        int executor = choice.SourceId;
        int[] offers = choice.Choices.ToArray();
        int[] spells = offers.Select(id => game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]).ToArray();
        Check(offers.Length == 3 && spells.All(id => id > 0) && spells.Distinct().Count() == 3,
            "one distinct spell per offer");
        Check(spells.Select(id => game.IdEntityDic[id].Card.Id).Distinct().Count() == 3, "distinct gifts");
        var graph = offers.Concat(spells).Append(executor).ToArray();
        EntityChoices deckChoice = null;
        session.Flush();
        session.PublishDecision(player);
        if (opponent)
        {
            Check(session.OpposingPromptsHidden(player.PlayerId), "bot choice published only as card backs");
            Private(session, graph);
        }
        else
        {
            int prompt = session.Packets.FindLastIndex(p => p.Id == 17);
            Check(prompt >= 0, "chooser prompt");
            var beforeChoice = new ClientModel();
            foreach (var wire in session.Packets.Take(prompt).Where(p => p.Id == 19))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                var history = new PegasusGame.PowerHistory(); history.Deserialize(stream); beforeChoice.Apply(history);
            }
            using var choiceStream = new MemoryStream(Convert.FromBase64String(session.Packets[prompt].Base64));
            var packet = new EntityChoices(); packet.Deserialize(choiceStream);
            Check(packet.Source == executor && packet.Entities.Count == offers.Length, "choice source executor/offer count");
            if (mode == "deck")
            {
                deckChoice = packet;
                Private(session, offers);
                Check(packet.Entities.SequenceEqual(Enumerable.Range(0, offers.Length).Select(i => int.MaxValue - i))
                    && packet.Entities.All(id => !game.IdEntityDic.ContainsKey(id)), "transport-only descending proxy ids");
            }
            else Check(packet.Entities.SequenceEqual(offers), "choice native offer ids");
            var actor = beforeChoice.Entities[executor];
            Check(actor.CardId == "EDR_102t" && actor.Get(GameTag.ZONE) == (int)Zone.SETASIDE
                && actor.Get(GameTag.CREATOR) == source.Id, "executor visible before prompt");
            for (int i = 0; i < offers.Length; i++)
            {
                int displayId = packet.Entities[i];
                var offer = beforeChoice.Entities[displayId];
                var spell = beforeChoice.Entities[spells[i]];
                Check(offer.CardId == game.IdEntityDic[offers[i]].Card.Id
                    && offer.Get(GameTag.DARK_GIFT_ENTITY) == spells[i]
                    && offer.Get(GameTag.ENTITY_ID) == displayId
                    && offer.Get(GameTag.ZONE) == (int)Zone.SETASIDE,
                    "offer identity/normalised 4865/zone before prompt " + mode);
                if (mode == "deck")
                    Check(offer.Get(GameTag.LINKED_ENTITY) == offers[i]
                        && offer.Get(GameTag.CREATOR) == executor && offer.Get(GameTag.DISPLAYED_CREATOR) == executor
                        && History(session.Packets.Take(prompt)).Any(e => e.HasFullEntity
                            && e.FullEntity.Entity == displayId && e.FullEntity.Name == offer.CardId),
                        "named FULL_ENTITY proxy links original/source before prompt");
                Check(spell.CardId == game.IdEntityDic[spells[i]].Card.Id && spell.CardId.StartsWith("EDR_100t")
                    && spell.Get(GameTag.CARDTYPE) == 5 && spell.Get(GameTag.ZONE) == (int)Zone.SETASIDE
                    && spell.Get(GameTag.CREATOR) == executor && spell.Get(GameTag.ENTITY_ID) == spells[i],
                    "SPELL identity/type/creator before prompt");
                Check(offer.Get(GameTag.HAS_DARK_GIFT) == 0 && game.IdEntityDic[offers[i]].AppliedEnchantments?.Any() != true,
                    "offers not yet enchanted");
            }
            Check(beforeChoice.Entities.Values.Count(e => e.Get(GameTag.CREATOR) == executor && e.Get(GameTag.CARDTYPE) == 5) == 3,
                "exactly one gift spell per offer");
        }
        var gift = DarkGifts.Registry.Single(g => g.SpellId == game.IdEntityDic[spells[0]].Card.Id);
        bool sweetDreams = gift.SpellId == "EDR_100t8";
        if (sweetDreamsCase) Check(sweetDreams, "deterministic Sweet Dreams pairing " + mode);
        var chosen = (Minion)game.IdEntityDic[offers[0]];
        string chosenCardId = chosen.Card.Id;
        int attack = chosen.AttackDamage, health = chosen.BaseHealth;
        int answerStart = session.Packets.Count;
        game.PowerHistory.Last.Clear();
        // Avoid SendChoices' bot turn loop while exercising the production proxy answer mapping.
        if (deckChoice != null)
        {
            session.Invoke("CompleteHumanChoice", deckChoice.Id, new List<int> { deckChoice.Entities[0] }, false);
            using var answerStream = new MemoryStream(Convert.FromBase64String(session.Packets.Last(p => p.Id == 13).Base64));
            var answer = new EntitiesChosen(); answer.Deserialize(answerStream);
            Check(answer.ChooseEntities.Entities.SequenceEqual(new[] { deckChoice.Entities[0] }), "acknowledgment uses proxy id");
            Private(session, offers.Skip(1));
        }
        else session.Process(ChooseTask.Pick(player, offers[0]));
        Check(sweetDreams ? chosen.Zone == player.DeckZone : chosen.Zone == player.HandZone,
            sweetDreams ? "chosen actual entity relocates to DECK" : "chosen actual entity reaches HAND");
        var enchant = chosen.AppliedEnchantments.Single(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1);
        if (sweetDreams) Check(player.DeckZone.TopCard == chosen && chosen.AttackDamage == attack + 4
            && chosen.BaseHealth == health + 5, "Sweet Dreams deck top retains +4/+5");
        foreach (int id in offers.Skip(1))
        {
            Check(game.IdEntityDic[id].AppliedEnchantments?.Any() != true && session.Client.Entities[id].Get(GameTag.HAS_DARK_GIFT) == 0,
                "unchosen cards untouched");
            if (mode == "deck") Check(game.IdEntityDic[id].Zone == player.DeckZone, "unchosen deck actors stay in DECK");
        }
        if (opponent) Private(session, graph.Append(enchant.Id));
        else
        {
            var entries = History(session.Packets.Skip(answerStart)).ToList();
            int attachment = entries.FindIndex(e => Snapshot(e)?.Entity == enchant.Id && Snapshot(e).Name == gift.EnchantmentId);
            int flag = entries.FindIndex(e => e.HasTagChange && e.TagChange.Entity == chosen.Id && e.TagChange.Tag == 3880 && e.TagChange.Value == 1);
            int hand = entries.FindIndex(e => e.HasTagChange && e.TagChange.Entity == chosen.Id && e.TagChange.Tag == 49 && e.TagChange.Value == 3
                || Snapshot(e)?.Entity == chosen.Id && Snapshot(e).Tags.Any(t => t.Name == 49 && t.Value == 3));
            Check(attachment >= 0 && hand > attachment && (mode == "deck" || flag >= 0 && hand > flag),
                "attachment and gift flag before/as HAND " + mode + $" (attachment={attachment}, flag={flag}, hand={hand})");
            if (mode == "deck")
                Check(entries[hand].HasShowEntity && entries[hand].ShowEntity.Entity == offers[0]
                    && entries[hand].ShowEntity.Name == chosenCardId
                    && entries[hand].ShowEntity.Tags.Any(t => t.Name == (int)GameTag.HAS_DARK_GIFT && t.Value == 1),
                    "draw names original engine id/gift flag with SHOW_ENTITY HAND");
            var beforeHand = new ClientModel();
            beforeHand.Apply(new PegasusGame.PowerHistory { List = History(session.Packets.Take(answerStart)).ToList() });
            beforeHand.Apply(new PegasusGame.PowerHistory { List = entries.Take(hand).ToList() });
            Check(beforeHand.Entities[enchant.Id].Get(GameTag.ATTACHED) == chosen.Id
                && beforeHand.Entities[enchant.Id].Get(GameTag.CREATOR) == spells[0]
                && beforeHand.Entities[enchant.Id].Get(GameTag.IS_NIGHTMARE_BONUS) == 1
                && beforeHand.Entities[enchant.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_6) == gift.SpellDbfId
                && (mode == "deck" || beforeHand.Entities[chosen.Id].Get(GameTag.HAS_DARK_GIFT) == 1), "complete attachment/flag before HAND");
            beforeHand.Apply(new PegasusGame.PowerHistory { List = entries.Skip(hand).Take(1).ToList() });
            Check(beforeHand.Entities[chosen.Id].CardId == chosenCardId
                && beforeHand.Entities[chosen.Id].Get(GameTag.ZONE) == (int)Zone.HAND
                && beforeHand.Entities[chosen.Id].Get(GameTag.HAS_DARK_GIFT) == 1, "visible gifted HAND before continuation");
            if (sweetDreams)
            {
                Check(beforeHand.Entities[chosen.Id].Get(GameTag.ATK) == attack + 4
                    && beforeHand.Entities[chosen.Id].Get(GameTag.HEALTH) == health + 5, "Sweet Dreams projected HAND +4/+5");
                int deck = entries.FindIndex(hand + 1, e => e.HasTagChange && e.TagChange.Entity == chosen.Id
                    && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)Zone.DECK);
                int hide = entries.FindIndex(hand + 1, e => e.HasHideEntity && e.HideEntity.Entity == chosen.Id
                    && e.HideEntity.Zone == (int)Zone.DECK);
                Check(deck > hand && hide >= deck, "Sweet Dreams projected HAND then DECK then HIDE");
                Check(session.Client.Entities[chosen.Id].CardId == ""
                    && session.Client.Entities[enchant.Id].CardId == "", "Sweet Dreams deck card/attachment hidden again");
            }
        }
        if (sweetDreams)
        {
            var entries = History(session.Packets.Skip(answerStart)).ToList();
            var zones = entries.Where(e => e.HasTagChange && e.TagChange.Entity == chosen.Id
                && e.TagChange.Tag == (int)GameTag.ZONE).Select(e => e.TagChange.Value).ToArray();
            Check(zones.TakeLast(2).SequenceEqual(new[] { (int)Zone.HAND, (int)Zone.DECK }),
                "Sweet Dreams projected HAND then DECK for either chooser");
            Check(session.Client.Entities[chosen.Id].CardId == ""
                && session.Client.Entities[chosen.Id].Get(GameTag.ZONE) == (int)Zone.DECK
                && session.Client.Entities[chosen.Id].Get(GameTag.CONTROLLER) == player.PlayerId,
                "Sweet Dreams hidden deck-top actor belongs to chooser");
            // Draw the actual top card so the existing public-play and backwards-removal proofs still run.
            game.PowerHistory.Last.Clear();
            Generic.Draw(player); session.Flush();
            Check(chosen.Zone == player.HandZone && DarkGifts.Has(chosen)
                && chosen.AttackDamage == attack + 4 && chosen.BaseHealth == health + 5,
                "Sweet Dreams later draw retains gift without relocating again");
            if (opponent) Private(session, graph.Append(enchant.Id));
            else
            {
                var actor = session.Client.Entities[enchant.Id];
                var expected = match.SnapshotVisibleTransportTags(enchant);
                foreach (var tag in new[] { GameTag.ATK, GameTag.HEALTH, GameTag.COST, GameTag.DAMAGE })
                {
                    Check(actor.Tags.TryGetValue((int)tag, out int actual) && actual == 0
                        && !expected.ContainsKey((int)tag), "Sweet Dreams HIDE creates client-only zero " + tag);
                    // Creation SHOW may send DAMAGE=0; HIDE also creates the three absent printed stats.
                    if (tag != GameTag.DAMAGE)
                        Check(!History(session.Packets.Skip(answerStart)).Any(entry =>
                            Snapshot(entry)?.Entity == enchant.Id && Snapshot(entry).Tags.Any(t => t.Name == (int)tag)
                            || entry.HasTagChange && entry.TagChange.Entity == enchant.Id && entry.TagChange.Tag == (int)tag),
                            "Sweet Dreams wire never sends synthetic enchantment stat " + tag);
                    Check(NativeUIProjection.TestControlTransportTagMatches((int)tag, CardType.ENCHANTMENT,
                        true, actual, false, 0), "Sweet Dreams endpoint accepts native HIDE zero " + tag);
                }
            }
        }
        if (!opponent)
        {
            var attached = session.Client.Entities[enchant.Id];
            Check(attached.CardId == gift.EnchantmentId && attached.Get(GameTag.ATTACHED) == chosen.Id
                && attached.Get(GameTag.CREATOR) == spells[0] && attached.Get(GameTag.IS_NIGHTMARE_BONUS) == 1
                && attached.Get(GameTag.TAG_SCRIPT_DATA_NUM_6) == gift.SpellDbfId, "attachment identity/ATTACHED/CREATOR/3567/2921");
            Check(session.Client.Entities[chosen.Id].Get(GameTag.ZONE) == 3
                && session.Client.Entities[chosen.Id].Get(GameTag.HAS_DARK_GIFT) == 1, "hand tooltip prerequisites");
        }
        game.PowerHistory.Last.Clear();
        if (mode != "deck")
        {
            // Public play must reveal the chosen graph, without exposing its unchosen siblings.
            var play = player.Options().OfType<PlayCardTask>().FirstOrDefault(task => task.Source?.Id == chosen.Id);
            Check(play != null, "chosen public play has a legal task: " + chosen.Card.Id);
            session.Process(play);
            Check(session.Client.Entities[chosen.Id].CardId == chosen.Card.Id, "chosen public play identity");
            Check(session.Client.Entities[enchant.Id].CardId == gift.EnchantmentId
                && session.Client.Entities[enchant.Id].Get(GameTag.ATTACHED) == chosen.Id, "public target reveals attachment");
            Check(session.Client.Entities[spells[0]].CardId == gift.SpellId
                && session.Client.Entities[spells[0]].Get(GameTag.CARDTYPE) == 5
                && session.Client.Entities[executor].CardId == "EDR_102t", "public chosen graph revealed");
            if (opponent) Private(session, offers.Skip(1).Concat(spells.Skip(1)), answerStart);
            game.PowerHistory.Last.Clear();
        }
        int removalStart = session.Packets.Count;
        if (mode == "pool") chosen.Silence();
        else if (mode == "cards") Generic.ReturnToHandBlock(player, chosen);
        else player.DeckZone.Add(player.HandZone.Remove(chosen));
        session.Flush();
        var removals = History(session.Packets.Skip(removalStart)).ToList();
        if (mode == "deck" && opponent) Private(session, graph.Append(enchant.Id));
        else
        {
            Check(removals.Any(e => e.HasTagChange && e.TagChange.Entity == enchant.Id && e.TagChange.Tag == (int)GameTag.ATTACHED && e.TagChange.Value == 0)
                && removals.Any(e => e.HasTagChange && e.TagChange.Entity == enchant.Id && e.TagChange.Tag == 49 && e.TagChange.Value == (int)Zone.REMOVEDFROMGAME),
                "attachment explicit detach/removal " + mode);
            Check(removals.Any(e => e.HasTagChange && e.TagChange.Entity == chosen.Id && e.TagChange.Tag == 3880 && e.TagChange.Value == 0),
                "explicit 3880 clear " + mode);
            Check(session.Client.Entities[chosen.Id].Get(GameTag.HAS_DARK_GIFT) == 0
                && session.Client.Entities[enchant.Id].Get(GameTag.ATTACHED) == 0
                && session.Client.Entities[enchant.Id].Get(GameTag.ZONE) == (int)Zone.REMOVEDFROMGAME,
                "client retains removal and flag clear " + mode);
        }
        Check(!DarkGifts.Has(chosen), "engine gift removed");
        Console.WriteLine($"PASS Dark Gift transport {mode}/{(opponent ? "bot" : "human")}{(sweetDreamsCase ? "/sweet-dreams-seed-22" : "")}: pre-choice graph; selected attachment/hand{(sweetDreams ? "/deck-top/redraw" : "")}; privacy; removal; violations=0");
    }

    internal static void Run()
    {
        foreach (bool opponent in new[] { true, false })
            foreach (string mode in new[] { "pool", "cards", "deck" }) Discover(mode, opponent);
        foreach (bool opponent in new[] { true, false })
            foreach (string mode in new[] { "cards", "deck" }) Discover(mode, opponent, sweetDreamsCase: true);
        KnownOpponentReplayRefreshesGift();
        InHandTransformPreservesTaunt();
        TargetPrivacy();
        CopyPrivacy();
        CyclePrivacy();
    }

    private static void InHandTransformPreservesTaunt()
    {
        var game = OfflinePoolTestRecipes.NewGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        var player = game.Player1;
        var minion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.HandZone);
        session.Flush(); game.PowerHistory.Last.Clear();
        var spell = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("EDR_100t3"), zone: player.SetasideZone);
        Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_100t3e"), spell, minion);
        session.Flush(); game.PowerHistory.Last.Clear();
        Generic.ChangeEntityBlock(player, minion, Cards.FromId("BOT_236"), false);
        session.Flush();
        Check(minion.HasTaunt && session.Client.Entities[minion.Id].Get(GameTag.TAUNT) == 1,
            "in-hand transform preserves granted Taunt in native projection");
        Check(!Invariants.Compare(game, session.Client).Any(), "transformed gift strict parity");
        Console.WriteLine("PASS Dark Gift in-hand transform: granted Taunt preserved on Kangor; violations=0");
    }

    private static void KnownOpponentReplayRefreshesGift()
    {
        var game = OfflinePoolTestRecipes.NewGame(opponent: true);
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        var player = game.CurrentPlayer;
        var wallow = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("EDR_487"), zone: player.HandZone);
        session.Flush();
        session.Process(PlayCardTask.Any(player, wallow));
        Check(Generic.ReturnToHandBlock(player, wallow), "known opponent Wallow returns to hand");
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(session.Client.Entities[wallow.Id].CardId == "EDR_487", "bounced opponent identity remains known");
        var spell = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("EDR_100t3"), zone: player.SetasideZone);
        Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_100t3e"), spell, wallow);
        session.Flush(); game.PowerHistory.Last.Clear();
        Check(wallow.HasTaunt && session.Client.Entities[wallow.Id].Get(GameTag.TAUNT) == 0,
            "new Taunt remains private in known opponent hand");
        player.UsedMana = 0;
        session.Process(PlayCardTask.Any(player, wallow));
        Check(session.Client.Entities[wallow.Id].Get(GameTag.TAUNT) == 1,
            "replayed known opponent card reveals privately granted Taunt");
        Check(!Invariants.Compare(game, session.Client).Any(), "known opponent replay transport invariants");
        Console.WriteLine("PASS Dark Gift known opponent replay: private hand grant refreshes public Taunt; violations=0");
    }

    private static void CopyPrivacy()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame(opponent);
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            session.Flush(); game.PowerHistory.Last.Clear();
            var player = game.CurrentPlayer;
            var original = player.HandZone.OfType<Minion>().First();
            var spell = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("EDR_100t"),
                zone: player.SetasideZone);
            Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_100te"), spell, original);
            new CopyTask(EntityType.TARGET, Zone.HAND).Process(game, player, player.Hero, original);
            var handCopy = player.HandZone.OfType<Minion>().Last();
            var handGift = handCopy.AppliedEnchantments.Single(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1);
            Check(handCopy != original && handGift.Creator == spell, "hand copy retains gift attachment");
            session.Flush();
            if (opponent) Private(session, new[] { original.Id, handCopy.Id, spell.Id, handGift.Id });
            else Check(session.Client.Entities[handCopy.Id].CardId == "CS2_231"
                && session.Client.Entities[handGift.Id].Get(GameTag.ATTACHED) == handCopy.Id,
                "human hand copy and attachment visible");

            game.PowerHistory.Last.Clear();
            session.Process(PlayCardTask.Any(player, original));
            game.PowerHistory.Last.Clear();
            // Living Nightmare's full board-copy shape: SummonCopyTask recreates gifts with creator=copy.
            new SummonCopyTask(EntityType.TARGET).Process(game, player, player.Hero, original);
            var summoned = player.BoardZone.OfType<Minion>().Last();
            var selfGift = summoned.AppliedEnchantments.Single(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1);
            Check(summoned != original && selfGift.Creator == summoned, "summon copy has self-created gift");
            session.Flush(); game.PowerHistory.Last.Clear();
            // Generic.Copy preserves that creator, so this attachment points at the original board minion.
            var copied = (Minion)Generic.Copy(player, player.Hero, summoned, Zone.PLAY);
            var copiedGift = copied.AppliedEnchantments.Single(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1);
            Check(copiedGift.Creator == summoned, "full copy has original-created gift");
            session.Flush();
            foreach (var minion in new[] { original, summoned, copied })
            {
                var enchant = minion.AppliedEnchantments.Single(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1);
                Check(session.Client.Entities[minion.Id].CardId == "CS2_231"
                    && session.Client.Entities[enchant.Id].CardId == "EDR_100te"
                    && session.Client.Entities[enchant.Id].Get(GameTag.ATTACHED) == minion.Id
                    && session.Client.Entities[enchant.Id].Get(GameTag.CREATOR) == enchant.Creator.Id,
                    "public full copy identity/attachment/creator");
            }
            if (opponent) Private(session, new[] { handCopy.Id, handGift.Id });
        }
        Console.WriteLine("PASS Dark Gift transport copy privacy: human/bot hand-to-hand; SummonCopyTask self creator; full copy original creator; violations=0");
    }

    private static void CyclePrivacy()
    {
        foreach (bool publicExit in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            session.Flush(); game.PowerHistory.Last.Clear();
            var a = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("EDR_100t"), zone: game.Player1.SetasideZone);
            var b = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("EDR_102t"), zone: game.Player1.SetasideZone);
            // Retained graph edges can outlive engine links; exercise a cycle independently of producer guards.
            var graph = (Dictionary<int, HashSet<int>>)typeof(NativeMatch)
                .GetField("privacyTargets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            graph[a.Id] = new() { b.Id };
            graph[b.Id] = new() { a.Id };
            session.Flush();
            Private(session, new[] { a.Id, b.Id });
            game.PowerHistory.Last.Clear();
            graph[b.Id].Add(publicExit ? game.Player1.Hero.Id : game.Player2.HandZone.First().Id);
            session.Flush();
            if (publicExit) Check(session.Client.Entities[a.Id].CardId == "EDR_100t"
                && session.Client.Entities[b.Id].CardId == "EDR_102t", "cycle with public exit reveals graph");
            else Private(session, new[] { a.Id, b.Id });
        }
        Console.WriteLine("PASS Dark Gift transport privacy cycles: closed cycle and hidden exit stay private; public exit reveals; violations=0");
    }

    private static void TargetPrivacy()
    {
        foreach (bool ownDeck in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            session.Flush(); game.PowerHistory.Last.Clear();
            var target = ownDeck ? game.Player1.DeckZone.First() : game.Player2.HandZone.First();
            // Human-owned spell/enchantment on a hidden target catches the old SHOW owner bypass.
            var spell = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("EDR_100t"),
                new EntityData { [GameTag.CREATOR] = game.Player1.Hero.Id }, game.Player1.SetasideZone);
            Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_100te"), spell, target);
            var enchant = target.AppliedEnchantments.Single(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1);
            Check(enchant.Controller == game.Player1, "privacy fixture has human-owned attachment");
            session.Flush();
            Private(session, new[] { target.Id, spell.Id, enchant.Id });
            game.PowerHistory.Last.Clear();
            ((Minion)target).Silence(); session.Flush();
            Private(session, new[] { target.Id, spell.Id, enchant.Id });
        }
        Console.WriteLine("PASS Dark Gift transport target privacy: human-owned spell/attachment on opponent HAND and own DECK; orphan privacy after removal; violations=0");
    }
}
