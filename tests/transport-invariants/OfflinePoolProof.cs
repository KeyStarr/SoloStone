using System.Reflection;
using HsSim;
using SabberStoneCore.CardSets.Client;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
namespace TransportInvariants;
using Card = SabberStoneCore.Model.Card;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;

internal static class OfflinePoolProof
{
    private static void Check(bool value, string label) => SelfChecks.Check(value, "offline pool " + label);
    private static (Game game, NativeMatch match, NativeSession session) Fixture(bool opponent = false, CardClass hero = CardClass.MAGE, bool smallPool = false)
    {
        var game = OfflinePoolTestRecipes.NewGame(opponent, hero: hero);
        if (smallPool) PinnedPoolFixture.Use(game);
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match);
        return (game, match, session);
    }
    private static EntityChoices Packet(NativeSession session)
    {
        var packet = new EntityChoices();
        using var stream = new MemoryStream(Convert.FromBase64String(session.Packets.Last(p => p.Id == 17).Base64));
        packet.Deserialize(stream); return packet;
    }
    private static void Identity(Game game, NativeSession session, int id, Card card, int source, Zone zone, int? creator = null, int? cost = null)
    {
        var entity = game.IdEntityDic[id]; var actor = session.Client.Entities[id];
        Check(entity.Card.Id == card.Id && entity.Card.AssetId == card.AssetId && actor.CardId == card.Id,
            "exact cardId/dbfId identity");
        // The native wire identifies cards by cardId; resolve the pinned export dbfId as the client does.
        var policy = OfflinePoolPolicy.Load();
        int dbfId = policy.Members.SingleOrDefault(m => m.CardId == actor.CardId)?.DbfId
            ?? policy.Dependencies.Single(d => d.CardId == actor.CardId).DbfId;
        Check(dbfId == card.AssetId, "native definition dbfId");
        foreach (var pair in new[] {
            (GameTag.CONTROLLER, entity.Controller.PlayerId), (GameTag.CREATOR, creator ?? source),
            (GameTag.DISPLAYED_CREATOR, source),
            (GameTag.COST, cost ?? (CostRules.HasManaNumbers(entity) ? CostRules.ManaNumbers(entity) : card.Cost)),
            (GameTag.ATK, entity is Minion m ? m.AttackDamage : card.ATK),
            (GameTag.HEALTH, entity is Minion minion ? minion.BaseHealth : card.Health), (GameTag.ZONE, (int)zone) })
            Check(entity[pair.Item1] == pair.Item2 && actor.Get(pair.Item1) == pair.Item2,
                "generated " + zone + " " + pair.Item1 + " expected=" + pair.Item2 + " engine=" + entity[pair.Item1] + " native=" + actor.Get(pair.Item1));
        Check(!Invariants.Compare(game, session.Client).Any(), "generated transport invariants");
    }
    private static void Generated()
    {
        var (game, _, session) = Fixture(); var source = game.Player1.Hero;
        var context = new EffectContext(game, game.Player1.Id, source.Id);
        var reference = game.CloneExact();
        var hand = context.PickRandomCard(OfflinePoolTestRecipes.Past);
        Check(hand.Id == OfflineCardPools.Pick(reference, OfflinePoolTestRecipes.Past, CardClass.MAGE, source.Card.AssetId).Id, "hand pick");
        // Existing generation tasks set DISPLAYED_CREATOR only; Discover sets both creator tags.
        context.AddToHand(hand.Id);
        var board = context.PickRandomCard(OfflinePoolTestRecipes.Past);
        Check(board.Id == OfflineCardPools.Pick(reference, OfflinePoolTestRecipes.Past, CardClass.MAGE, source.Card.AssetId).Id, "board pick");
        int boardId = context.Summon(board.Id);
        session.Flush();
        Identity(game, session, game.Player1.HandZone.Last().Id, hand, source.Id, Zone.HAND, creator: 0);
        Identity(game, session, boardId, board, source.Id, Zone.PLAY, creator: 0);
        Check(game.Random.CaptureState().Equals(reference.Random.CaptureState()), "generation exact RNG");
    }
    private static string State(Game game) => Canonical.State(game) + Canonical.History(game) +
        Canonical.Value(game.Random.CaptureState()) + Canonical.Value(game.IdEntityDic.Select(p => p).OrderBy(p => p.Key)
            .Select(p => new { p.Key, p.Value.NativeTags }));

    private static void Invalid(Game game, NativeMatch match, NativeSession session, EntityChoices packet)
    {
        byte[] bytes = game.SaveSnapshot(); string packets = Canonical.Value(session.Packets);
        foreach (var invalid in new[] { new List<int>(), new List<int> { int.MaxValue }, new List<int> { packet.Entities[0], packet.Entities[0] } })
        {
            bool rejected = false;
            try { match.SendChoices(packet.Id, invalid); } catch (Exception e) when (e is InvalidOperationException || e is ArgumentException) { rejected = true; }
            Check(rejected && bytes.SequenceEqual(game.SaveSnapshot()) && packets == Canonical.Value(session.Packets),
                "invalid native answer leaves packets/state/RNG unchanged");
        }
    }
    private static void Ordinary(int count, bool opponent)
    {
        var (game, match, session) = Fixture(opponent, smallPool: true); var player = game.CurrentPlayer;
        var source = Entity.FromCard(player, Cards.FromId(count == 2 ? "EX1_028" : "CS2_231"), zone: player.HandZone);
        var context = new EffectContext(game, player.Id, source.Id);
        var filter = count == 0 ? OfflinePoolTestRecipes.Empty : count == 1 ?
            OfflinePoolFilter.Random(scope: PoolScope.Past, race: Race.PIRATE) : OfflinePoolTestRecipes.Past;
        Check(context.Discover(filter) == (count > 0), "ordinary open count"); session.Flush();
        if (count == 0)
        {
            Check(player.Choice == null && !session.Packets.Any(p => p.Id == 17), "zero offers no prompt");
            return;
        }
        Select(game, match, session, source, count, opponent);
    }
    private static void Select(Game game, NativeMatch match, NativeSession session, IPlayable source, int count, bool opponent, bool summon = false)
    {
        var player = game.CurrentPlayer; var ids = player.Choice.Choices.ToArray();
        Check(ids.Length == count, "offer count");
        foreach (int id in ids)
            if (opponent) Check(session.Client.Entities[id].CardId == "", "opponent offers private");
            else Identity(game, session, id, game.IdEntityDic[id].Card, source.Id, Zone.SETASIDE);
        session.PublishDecision(player);
        if (!opponent)
        {
            var packet = Packet(session);
            Check(packet.ChoiceType == 2 && packet.CountMin == 1 && packet.CountMax == 1 && packet.Source == source.Id &&
                packet.Entities.SequenceEqual(ids), "GENERAL mandatory single pick with valid source and 1-3 actors");
            Invalid(game, match, session, packet);
            int before = session.Packets.Count;
            match.SendChoices(packet.Id, new List<int> { ids[0] });
            Check(session.Packets.Skip(before).Count(p => p.Id == 13) == 1, "single selected acknowledgment");
            string state = State(game); int packetCount = session.Packets.Count; bool rejected = false;
            try { match.SendChoices(packet.Id, new List<int> { ids[0] }); } catch (Exception e) when (e is InvalidOperationException || e is ArgumentException) { rejected = true; }
            Check(rejected && state == State(game) && packetCount == session.Packets.Count, "stale native answer unchanged");
            if (!summon) Identity(game, session, ids[0], game.IdEntityDic[ids[0]].Card, source.Id, Zone.HAND);
        }
        else
        {
            Check(session.OpposingPromptsHidden(player.PlayerId), "opponent prompt shown only as card backs");
            session.Process(ChooseTask.Pick(player, ids[0]));
            Check(ids.All(id => session.Client.Entities[id].CardId == ""), "offers remain private after selection");
        }
        Check(player.Choice == null, "selection resolves once");
        if (summon)
        {
            var summoned = player.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
            Check(summoned.Card.Id == game.IdEntityDic[ids[0]].Card.Id && summoned.Id != ids[0],
                "Story summons a fresh chosen identity instead of moving offer to hand");
            Identity(game, session, summoned.Id, summoned.Card, source.Id, Zone.PLAY, creator: 0, cost: summoned.Cost);
        }
        else Check(game.IdEntityDic[ids[0]].Zone.Type == Zone.HAND, "selected actor moves to hand");
        Check(!Invariants.Compare(game, session.Client).Any(), "selected-card transport invariants");
        Check(session.Client.Entities.Values.All(e => e.Tags.Keys.All(t => t < 31001 || t > 31009)), "private tags not projected");
        if (opponent)
        {
            session.Process(PlayCardTask.Any(player, game.IdEntityDic[ids[0]]));
            Identity(game, session, ids[0], game.IdEntityDic[ids[0]].Card, source.Id, Zone.PLAY);
            Check(ids.Skip(1).All(id => session.Client.Entities[id].CardId == ""), "unchosen offers stay private after selected card is played");
        }
    }
    internal static void Run()
    {
        PendingOutcastReveal();
        Pilots();
        OfflinePoolTestRecipes.WithDeclarations(() => {
            Generated();
            foreach (bool opponent in new[] { false, true })
            {
                for (int count = 0; count <= 3; count++) Ordinary(count, opponent);
                OfflinePoolTestRecipes.WithSource(OfflinePoolTestRecipes.Small, card => {
                    var (game, match, session) = Fixture(opponent, smallPool: true);
                    var source = Generic.DrawCard(game.CurrentPlayer, card); session.Flush();
                    session.Process(PlayCardTask.Any(game.CurrentPlayer, source));
                    Select(game, match, session, source, 3, opponent);
                    Check(game.CurrentPlayer.Hero.Armor == 1 && !game.Resolution.HasSuspendedEffect, "native selection resumes recipe once");
                });
            }
        });
        Console.WriteLine("PASS offline pool transport: generated hand/board identity, owner/creator/cost/stats/zone; GENERAL 1-3 offers; selection/ack; ordinary/suspended privacy; invalid/stale unchanged; violations=0");
    }

    private static readonly DecisionBattlecryTask OutcastRecipe = new("transport/outcast/decision/v1",
        new SelectEntityThenContinueTask(), EffectContext.Task(ctx => TaskState.COMPLETE));

    private static void PendingOutcastReveal()
    {
        var card = Cards.FromId("CORE_BT_480"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = OutcastRecipe };
            var (game, _, session) = Fixture(opponent: true); var player = game.CurrentPlayer;
            var source = Entity.FromCard(player, card, zone: player.HandZone); session.Flush();
            Check(session.Client.Entities[source.Id].CardId == "", "opponent Outcast starts hidden");
            session.Process(PlayCardTask.Any(player, source));
            Check(player.Choice != null && OutcastRules.PlayedFromEdge(source), "opponent Outcast decision remains pending");
            Check(Generic.IsDecisionInternalTag((GameTag)31008), "shared internal-tag range covers Outcast");
            Check(session.Client.Entities[source.Id].CardId == card.Id, "pending opponent play is revealed");
            // Inspect serialized packets as well as actors: a later snapshot must not hide an earlier leak.
            foreach (var wire in session.Packets.Where(p => p.Id == 19))
            {
                var packet = new PegasusGame.PowerHistory();
                using var stream = new MemoryStream(Convert.FromBase64String(wire.Base64));
                packet.Deserialize(stream);
                Check(packet.List.All(h => (h.ShowEntity == null || h.ShowEntity.Tags.All(t => t.Name != 31008))
                    && (h.FullEntity == null || h.FullEntity.Tags.All(t => t.Name != 31008))
                    && (h.TagChange == null || h.TagChange.Tag != 31008)), "pending opponent reveal packets exclude Outcast marker");
            }
            Check(session.Client.Entities.Values.All(e => !e.Tags.ContainsKey(31008)), "pending opponent actors exclude Outcast marker");
            Check(!Invariants.Compare(game, session.Client).Any(), "pending Outcast reveal transport invariants");
        }
        finally { card.Power = original; }
    }

    private static void Pilots()
    {
        foreach (var declaration in OfflinePoolDeclarations.Pilots.Concat(OfflinePoolDeclarations.DeathKnight).Append(OfflinePoolDeclarations.Cards.Single(c => c.CardId == "JAIL_892")))
        {
            bool dk = declaration.Pools.Any(r => r.ClassDomain.Contains("DEATHKNIGHT"));
            var (game, match, session) = Fixture(hero: dk ? CardClass.DEATHKNIGHT : CardClass.MAGE); var player = game.Player1;
            foreach (var opening in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(opening));
            if (declaration.CardId == "JAIL_328")
                foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
            Minion victim = null;
            if (declaration.CardId is "CORE_CATA_009" or "RLK_025" or "CORE_RLK_066")
            {
                var owner = declaration.CardId is "CORE_RLK_066" ? player : player.Opponent;
                victim = (Minion)Entity.FromCard(owner, Cards.FromId("CS2_101t"));
                Generic.SummonBlock(game, victim, -1, null);
                if (declaration.CardId == "CORE_RLK_066") { victim.Destroy(); game.DeathProcessingAndAuraUpdate(); }
            }
            var source = Generic.DrawCard(player, Cards.FromId(declaration.CardId));
            session.Flush();
            var task = player.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id &&
                (t.Target == null || t.Target.Id == (declaration.CardId is "CORE_CATA_009" or "RLK_025" ? victim.Id : player.Opponent.Hero.Id)));
            session.Process(task);
            if (source.Card.Id == "DINO_415")
            {
                // Selector decision-spell semantics: Story is held out of the graveyard (engine and native actor) until the answer.
                Check(player.Choice != null && game.SpellCompletion?.SourceId == source.Id && !Generic.HasDecisionPlay(player) &&
                    source.Zone?.Type != Zone.GRAVEYARD && session.Client.Entities[source.Id].Get(GameTag.ZONE) != (int)Zone.GRAVEYARD &&
                    player.NumSpellsPlayedThisGame == 0, "Story pends outside the graveyard with its cast tail deferred");
                Select(game, match, session, source, 3, false, summon: true);
                Check(game.SpellCompletion == null && source.Zone == player.GraveyardZone &&
                    session.Client.Entities[source.Id].Get(GameTag.ZONE) == (int)Zone.GRAVEYARD && player.NumSpellsPlayedThisGame == 1,
                    "Story reaches the graveyard once after the answer, on engine and native actor");
            }
            else if (player.Choice != null) Select(game, match, session, source, 3, false);
            else if (source.Card.Id == "EDR_530") session.Process(EndTurnTask.Any(player));
            else if (source.Card.Power.DeathrattleTask != null)
            {
                ((Minion)source).ToBeDestroyed = true;
                game.DeathProcessingAndAuraUpdate(); session.Flush();
            }
            bool Live(IPlayable e) => e.Zone?.Type is Zone.HAND or Zone.PLAY || source.Card.Id == "JAIL_892" && e.Zone?.Type == Zone.DECK;
            var roots = game.IdEntityDic.Values.Where(e => e[GameTag.DISPLAYED_CREATOR] == source.Id &&
                (Live(e) || e.Card.Shatter is { } family && e.Card.Id == family.ParentId &&
                    e[ShatterRules.AlreadyCombined] == 0 && e[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME)).ToArray();
            int expected = source.Card.Id is "CATA_723" or "BE_220" or "JAIL_892" or "BE_749" ? 2 : source.Card.Id == "EDR_848" ? 3 : 1;
            Check(roots.Length == expected, declaration.CardId + " native generated root count");
            IPlayable Root(IPlayable e)
            {
                // Split/join entities point to older members of the same Shatter family.
                while (e.Card.Shatter is { } family && e[GameTag.DISPLAYED_CREATOR] != source.Id &&
                    game.IdEntityDic.TryGetValue(e[GameTag.DISPLAYED_CREATOR], out var parent) &&
                    parent.Id < e.Id && parent.Card.Shatter?.ParentId == family.ParentId) e = parent;
                return e;
            }
            var outputs = game.IdEntityDic.Values.Where(e => Live(e) && roots.Any(root => root.Id == Root(e).Id)).ToArray();
            int halves = outputs.Count(e => e.Card.Shatter is { } family && e.Card.Id != family.ParentId);
            Check(halves % 2 == 0 && outputs.Length == expected + halves / 2 &&
                outputs.Where(e => e.Card.Shatter != null).GroupBy(e => e.Card.Shatter.ParentId).All(group =>
                    group.Count(e => e.Card.Id == e.Card.Shatter.FirstHalfId) == group.Count(e => e.Card.Id == e.Card.Shatter.SecondHalfId)),
                declaration.CardId + " exact live output count with complementary split halves");
            foreach (var output in outputs)
            {
                if (source.Card.Id == "JAIL_328")
                    Check(output.Cost == Math.Max(0, output.Card.Cost - (output[ShatterRules.AlreadyCombined] == 1 ? 4 : 2)) &&
                        Entity.FromCard(player, output.Card).Cost == output.Card.Cost, "Bruiser entity-only discount");
                if (output.Zone.Type == Zone.DECK)
                {
                    var actor = session.Client.Entities[output.Id];
                    Check(actor.CardId == "" && actor.Get(GameTag.ZONE) == (int)Zone.DECK, "generated deck card stays hidden");
                    Generic.Draw(player, output); session.Flush();
                }
                Identity(game, session, output.Id, output.Card, output[GameTag.DISPLAYED_CREATOR], output.Zone.Type,
                    creator: source.Card.Id is "CATA_484" or "CORE_CATA_009" or "CORE_RLK_066" or "RLK_025" ? source.Id : 0, cost: output.Cost);
            }
            Check(!Invariants.Compare(game, session.Client).Any(), declaration.CardId + " pilot transport");
        }
        Console.WriteLine($"PASS offline pool recipe transport: cardsPlayed={OfflinePoolDeclarations.Pilots.Count + OfflinePoolDeclarations.DeathKnight.Count + 1}; nine unchanged pilots; generated hand/board/deck/offers; entity discount; violations=0");
    }
}
