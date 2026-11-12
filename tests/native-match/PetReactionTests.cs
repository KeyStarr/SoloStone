using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SabberStoneCore.Enums;
using EngineZone = SabberStoneCore.Enums.Zone;
using SabberStoneCore.Kettle;
using NativeHistory = PegasusGame.PowerHistoryData;
using E = SabberStoneCore.Kettle.IPowerHistoryEntry;

internal static partial class Program
{
    private static void PetReactions()
    {
        foreach (NativePetReactions.PetEvent mirrored in Enum.GetValues(typeof(NativePetReactions.PetEvent)))
            Check(Enum.TryParse<PetEventType>(mirrored.ToString(), out var pinned) && (int)pinned == (int)mirrored, "pet event mirrors pinned enum: " + mirrored);
        var projection = new NativePetReactions();
        const int pet = 9999;
        PowerHistoryFullEntity Entity(int id, int owner, CardType type, int cost = 0, EngineZone zone = EngineZone.PLAY, int secret = 0) => new PowerHistoryFullEntity {
            Entity = new PowerHistoryEntity { Id = id, Tags = new Dictionary<GameTag, int> {
                [GameTag.CONTROLLER] = owner, [GameTag.CARDTYPE] = (int)type, [GameTag.COST] = cost,
                [GameTag.ZONE] = (int)zone, [GameTag.SECRET] = secret } } };
        PowerHistoryCreateGame Create() => new PowerHistoryCreateGame {
            Game = new PowerEntity { Id = 1, Tags = new Dictionary<GameTag, int>() },
            Players = new[] { 1, 2 }.Select(owner => new PowerPlayer { PlayerId = owner,
                PowerEntity = new PowerEntity { Id = owner + 1, Tags = new Dictionary<GameTag, int>() } }).ToArray() };
        PowerHistoryMetaData Hit(int id, int amount, MetaDataType kind = MetaDataType.DAMAGE) => new PowerHistoryMetaData {
            Type = kind, Data = amount, Info = new List<int> { id } };
        List<NativeHistory> Project(params E[] entries) => projection.Project(entries.ToList(), 1, pet)
            .OrderBy(pair => pair.Key).SelectMany(pair => pair.Value).ToList();
        void Expect(int[] expected, params E[] entries) {
            var wire = Project(entries);
            Check(wire.All(e => e.TagChange.Entity == pet && e.TagChange.Tag == 4038), "reaction targets only cosmetic pet");
            Check(wire.Select(e => e.TagChange.Value).SequenceEqual(expected.SelectMany(e => new[] { e, 0 })),
                "ordered native reactions: expected " + string.Join(",", expected) + " got " + string.Join(",", wire.Select(e => e.TagChange.Value)));
        }
        Expect(Array.Empty<int>(), Create(), Entity(10, 1, CardType.HERO), Entity(20, 2, CardType.HERO),
            Entity(30, 1, CardType.MINION, 3), Entity(31, 2, CardType.MINION, 4), Entity(32, 1, CardType.MINION, 7));
        Expect(new[] { 3, 4, 1, 2, 27, 28, 25, 26 }, Hit(10, 5), Hit(10, 6), Hit(20, 5), Hit(20, 6),
            Hit(10, 5, MetaDataType.HEALING), Hit(10, 6, MetaDataType.HEALING), Hit(20, 5, MetaDataType.HEALING), Hit(20, 6, MetaDataType.HEALING));
        Expect(Array.Empty<int>(), Hit(10, 0), Hit(20, -1), Hit(30, 8), Hit(404, 7));
        Expect(new[] { 3, 3 }, Hit(10, 2), Hit(10, 2));
        Expect(new[] { 3 }, Hit(10, 2)); // Same reaction across packet boundary.
        E Play(int id) => PowerHistoryBuilder.BlockStart(BlockType.PLAY, id, "", 0, 0);
        E End() => PowerHistoryBuilder.BlockEnd();
        E Zone(int id, EngineZone zone) => PowerHistoryBuilder.TagChange(id, GameTag.ZONE, (int)zone);
        E Step(SabberStoneCore.Enums.Step step) => PowerHistoryBuilder.TagChange(1, GameTag.STEP, (int)step);
        // Minions react on entering play, not on the PLAY block itself.
        Expect(Array.Empty<int>(), Zone(30, EngineZone.HAND), Zone(31, EngineZone.HAND), Zone(32, EngineZone.HAND), Play(30), End());
        Expect(new[] { 8, 6, 10 }, Play(30), Zone(30, EngineZone.PLAY), End(), Play(31), Zone(31, EngineZone.PLAY), End(), Play(32), Zone(32, EngineZone.PLAY), End());
        Expect(new[] { 9 }, Zone(30, EngineZone.HAND), PowerHistoryBuilder.TagChange(30, GameTag.COST, 6), Play(30), Zone(30, EngineZone.PLAY), End());
        Expect(new[] { 5 }, Zone(30, EngineZone.HAND), PowerHistoryBuilder.TagChange(30, GameTag.CONTROLLER, 2), PowerHistoryBuilder.TagChange(30, GameTag.COST, 0), Play(30), Zone(30, EngineZone.PLAY), End());
        // A frozen reveal can carry the move into play and an aura-modified cost absent from preceding tag changes.
        Expect(new[] { 7 }, Zone(30, EngineZone.HAND), Play(30), new PowerHistoryShowEntity { Entity = Entity(30, 2, CardType.MINION, 7).Entity }, End());
        // Summons count too (retail), tiered by their own cost; staying in play or re-revealing does not repeat.
        Expect(new[] { 8, 7 }, Entity(33, 1, CardType.MINION, 2, EngineZone.SETASIDE), Zone(33, EngineZone.PLAY),
            Entity(34, 2, CardType.MINION, 9, EngineZone.SETASIDE), Zone(34, EngineZone.PLAY), Zone(34, EngineZone.PLAY),
            new PowerHistoryShowEntity { Entity = Entity(34, 2, CardType.MINION, 9).Entity });
        // Spells, hero powers and location uses react at the PLAY tail, after their effects (retail order).
        Expect(new[] { 1, 15 }, Entity(44, 1, CardType.SPELL, 2, EngineZone.HAND), Play(44), Hit(20, 3), End());
        // ...right after their own POWER block: after-cast triggers and deaths later in the PLAY block come after (retail).
        E Power(int id) => PowerHistoryBuilder.BlockStart(BlockType.POWER, id, "", 0, 0);
        E Deaths() => PowerHistoryBuilder.BlockStart(BlockType.DEATHS, 1, "", 0, 0);
        Expect(new[] { 8, 1, 15, 19 }, Entity(45, 1, CardType.SPELL, 2, EngineZone.HAND), Entity(46, 1, CardType.MINION, 2),
            Play(45), Power(45), Hit(20, 3), End(), Deaths(), Zone(46, EngineZone.GRAVEYARD), End(), End());
        // An automatic play (Fyrakk's casts) is presentation, never a card play: its effects react, the "spell played" does not.
        E EffectPlay(int id) => new PowerHistoryEffectPlayStart { BlockType = BlockType.PLAY, Source = id, Index = -1, EffectCardId = "", ForceShowBigCard = true };
        Expect(new[] { 1 }, Entity(49, 1, CardType.SPELL, 2, EngineZone.SETASIDE), EffectPlay(49), Power(49), Hit(20, 3), End(), End());
        // An autoplayed minion still enters play (a summon), tiered by its own cost.
        Expect(new[] { 8 }, Entity(50, 1, CardType.MINION, 2, EngineZone.SETASIDE), EffectPlay(50), Zone(50, EngineZone.PLAY), End());
        Expect(new[] { 8, 30, 19 }, Entity(47, 1, CardType.HERO_POWER), Entity(48, 1, CardType.MINION, 2),
            Play(47), Power(47), End(), Power(47), End(), Deaths(), Zone(48, EngineZone.GRAVEYARD), End(), End());
        Expect(new[] { 15, 14, 30, 29 }, Entity(40, 1, CardType.SPELL, 3, EngineZone.HAND), Play(40), End(),
            Entity(41, 2, CardType.SPELL, 4, EngineZone.HAND), Play(41), End(),
            Entity(42, 1, CardType.HERO_POWER), Play(42), End(), Entity(43, 2, CardType.HERO_POWER), Play(43), End());
        // Turn start waits for MAIN_START_TRIGGERS (retail); the CURRENT_PLAYER switch alone is silent.
        Expect(Array.Empty<int>(), PowerHistoryBuilder.TagChange(2, GameTag.CURRENT_PLAYER, 1));
        Expect(new[] { 12 }, Step(SabberStoneCore.Enums.Step.MAIN_START_TRIGGERS), Step(SabberStoneCore.Enums.Step.MAIN_START));
        Expect(Array.Empty<int>(), PowerHistoryBuilder.TagChange(2, GameTag.CURRENT_PLAYER, 0), PowerHistoryBuilder.TagChange(3, GameTag.CURRENT_PLAYER, 1));
        Expect(new[] { 11, 24, 23, 38, 37 },
            Step(SabberStoneCore.Enums.Step.MAIN_START_TRIGGERS),
            PowerHistoryBuilder.TagChange(10, GameTag.ARMOR, 5), PowerHistoryBuilder.TagChange(10, GameTag.ARMOR, 5),
            PowerHistoryBuilder.TagChange(10, GameTag.ARMOR, 2), PowerHistoryBuilder.TagChange(20, GameTag.ARMOR, 1),
            PowerHistoryBuilder.TagChange(2, GameTag.LAST_CARD_DISCARDED, 40), PowerHistoryBuilder.TagChange(3, GameTag.LAST_CARD_DISCARDED, 41));
        // Retail never reacts to placing a location; only its activation.
        Expect(new[] { 22, 21, 34, 33 }, Entity(50, 1, CardType.WEAPON, zone: EngineZone.HAND),
            PowerHistoryBuilder.TagChange(50, GameTag.ZONE, (int)EngineZone.PLAY), Entity(51, 2, CardType.WEAPON, zone: EngineZone.HAND),
            PowerHistoryBuilder.TagChange(51, GameTag.ZONE, (int)EngineZone.PLAY), Entity(52, 1, (CardType)39, zone: EngineZone.HAND),
            PowerHistoryBuilder.TagChange(52, GameTag.ZONE, (int)EngineZone.PLAY), Entity(53, 2, (CardType)39, zone: EngineZone.HAND),
            PowerHistoryBuilder.TagChange(53, GameTag.ZONE, (int)EngineZone.PLAY), Play(52), End(), Play(53), End());
        Expect(new[] { 40, 39 }, Entity(60, 1, CardType.SPELL, zone: EngineZone.SECRET, secret: 1),
            PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, 60, "", -1, 0),
            PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, 60, "", -1, 0), End(), End(),
            Entity(61, 2, CardType.SPELL, zone: EngineZone.SECRET, secret: 1), PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, 61, "", -1, 0), End());
        Expect(new[] { 18, 20 }, PowerHistoryBuilder.BlockStart(BlockType.DEATHS, 1, "", 0, 0),
            PowerHistoryBuilder.TagChange(31, GameTag.ZONE, (int)EngineZone.GRAVEYARD), PowerHistoryBuilder.TagChange(32, GameTag.ZONE, (int)EngineZone.GRAVEYARD), End());
        Expect(Array.Empty<int>(), PowerHistoryBuilder.TagChange(30, GameTag.ZONE, (int)EngineZone.HAND));
        Expect(Array.Empty<int>(), PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, 10, "", 0, 0),
            new PowerHistoryResetGame { CreateGame = Create() }, Entity(10, 2, CardType.HERO), End());
        Expect(new[] { 1, 23 }, Hit(10, 2), PowerHistoryBuilder.TagChange(10, GameTag.ARMOR, 5));
        Expect(Array.Empty<int>(), Hit(20, 2)); // Discarded graph must be gone.
        PetReactionTransport();
        Console.WriteLine("PASS pet reactions: thresholds, perspective, ordered/repeated delivery, reset, native transport and no pet");
    }

    private static void PetReactionTransport()
    {
        using var fixture = new CosmeticsFixture();
        var previousProfile = NativeMatch.CosmeticsProfile;
        var previousVariant = NativeMatch.NativePetVariant;
        string root = Path.Combine(Path.GetTempPath(), "pet-reactions-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try {
            string path = Path.Combine(root, "profile.xml");
            using (var fresh = new LocalProfile(path)) fresh.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData)); LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 5000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using var profile = new LocalProfile(path);
            profile.Purchase("pet-PET_3_1", 1, 5000, "pet-reactions");
            NativeMatch.CosmeticsProfile = () => profile;
            NativeMatch.NativePetVariant = _ => 5;
            foreach (bool equipped in new[] { true, false }) {
                if (!equipped) profile.SelectPet(null);
                var capture = new Capture();
                var match = new NativeMatch(capture.Send, "mage", "warrior", 1337); match.Start();
                match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
                var game = match.Game;
                int cursor = capture.Packets.Count;
                game.PowerHistory.Last.Clear();
                game.Player1.Hero.TakeDamage(game.Player2.Hero, 6);
                game.Player1.Hero.TakeDamage(game.Player2.Hero, 6);
                game.Player1.Hero.TakeHeal(game.Player1.Hero, 5);
                FlushHistory(match);
                var history = NativeHistory(capture, cursor);
                var reactions = history.Where(e => e.HasTagChange && e.TagChange.Tag == 4038).ToArray();
                Check(reactions.Select(e => e.TagChange.Value).SequenceEqual(equipped ? new[] { 4, 0, 4, 0, 27, 0 } : Array.Empty<int>()),
                    "real resolved damage/healing reactions and no-equipped-pet suppression");
                Check(game.Player1.Hero.Damage == 7, "pet projection preserves gameplay damage");
                if (equipped) {
                    int pet = NativeHistory(capture).Single(e => e.HasFullEntity && e.FullEntity.Name == "PET_3_1").FullEntity.Entity;
                    Check(reactions.All(e => e.TagChange.Entity == pet), "native reactions target injected pet");
                    int damage = history.FindIndex(e => e.HasMetaData && e.MetaData.Data == 6);
                    Check(damage >= 0 && history[damage + 1].TagChange?.Tag == 4038, "reaction stays beside resolved hit metadata");
                    var reset = new List<E> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, game.Player1.Hero.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
                    reset.AddRange(game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e => PowerHistoryBuilder.FullEntity(e, reset: true)));
                    reset.Add(PowerHistoryBuilder.BlockEnd());
                    cursor = capture.Packets.Count; SendHistoryEntries(match, reset);
                    Check(!NativeHistory(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Tag == 4038), "native reset snapshots produce no reactions");
                    game.PowerHistory.Last.Clear(); game.Player1.Hero.TakeDamage(game.Player2.Hero, 6);
                    cursor = capture.Packets.Count; FlushHistory(match);
                    Check(NativeHistory(capture, cursor).Where(e => e.HasTagChange && e.TagChange.Tag == 4038).Select(e => e.TagChange.Value).SequenceEqual(new[] { 4, 0 }),
                        "same reaction resumes once after rewind");
                }
                {
                    int[] Events(int from) => NativeHistory(capture, from).Where(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Value != 0).Select(e => e.TagChange.Value).ToArray();
                    var human = game.Player1;
                    game.PowerHistory.Last.Clear();
                    var discarded = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: human.HandZone);
                    FlushHistory(match);
                    cursor = capture.Packets.Count; game.PowerHistory.Last.Clear();
                    SabberStoneCore.Actions.Generic.DiscardBlock(human, discarded); FlushHistory(match);
                    Check(Events(cursor).SequenceEqual(equipped ? new[] { 38 } : Array.Empty<int>()), "real engine discard marker, gated by equipped pet");
                    human.GraveyardZone.Remove(discarded); human.HandZone.Add(discarded);
                    FlushHistory(match); cursor = capture.Packets.Count; game.PowerHistory.Last.Clear();
                    SabberStoneCore.Actions.Generic.DiscardBlock(human, discarded); FlushHistory(match);
                    Check(Events(cursor).SequenceEqual(equipped ? new[] { 38 } : Array.Empty<int>()), "same entity discarded twice remains two events");
                    Check(!NativeHistory(capture).Any(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.LAST_CARD_DISCARDED), "internal discard marker never reaches native controller state");
                    if (equipped) {
                        foreach (int paid in new[] { 2, 0 }) {
                            game.PowerHistory.Last.Clear(); human.BaseMana = 10; human.UsedMana = 0;
                            var spell = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_029"), zone: human.HandZone);
                            SabberStoneCore.Actions.CostRules.AddNextCardRule(human, setCost: paid);
                            FlushHistory(match); cursor = capture.Packets.Count;
                            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, spell, game.Player2.Hero)), "discounted Fireball accepted");
                            FlushHistory(match);
                            Check(Events(cursor).SequenceEqual(new[] { 2, 15 }), "paid spell tier survives consumed discount, including zero, and follows its damage: " + string.Join(",", Events(cursor)));
                        }
                        // A lethal spell reacts before the death it causes (retail order).
                        game.PowerHistory.Last.Clear(); human.UsedMana = 0;
                        var yeti = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
                        var lethal = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_029"), zone: human.HandZone);
                        FlushHistory(match); cursor = capture.Packets.Count;
                        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, lethal, (SabberStoneCore.Model.Entities.ICharacter)yeti)), "lethal Fireball accepted");
                        FlushHistory(match);
                        Check(Events(cursor).SequenceEqual(new[] { 16, 18 }), "spell reaction precedes the death it causes: " + string.Join(",", Events(cursor)));
                        game.PowerHistory.Last.Clear(); game.Player2.BaseMana = 10;
                        var secret = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, SabberStoneCore.Model.Cards.FromId("EX1_287"), zone: game.Player2.HandZone);
                        Check(SabberStoneCore.Actions.Generic.PlayCard(game.Player2, secret, skipPrePhase: true), "Counterspell installed by engine");
                        secret.IsExhausted = false; // Fixture installed out of turn; normal turn-start readies the secret.
                        var countered = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_029"), zone: human.HandZone);
                        human.UsedMana = 0; FlushHistory(match); cursor = capture.Packets.Count;
                        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, countered, game.Player2.Hero)), "countered Fireball accepted");
                        FlushHistory(match);
                        Check(Events(cursor).Count(e => e == 39) == 1 && !Events(cursor).Contains(2), "real secret fires once and countered spell deals no damage reaction: " + string.Join(",", Events(cursor)));
                        // Retail reacts to every minion entering play: the played Hunter and its summoned Boar.
                        game.PowerHistory.Last.Clear(); human.UsedMana = 0;
                        var hunter = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_196"), zone: human.HandZone);
                        FlushHistory(match); cursor = capture.Packets.Count;
                        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(human, hunter)), "Razorfen Hunter accepted");
                        FlushHistory(match);
                        Check(Events(cursor).SequenceEqual(new[] { 8, 8 }), "played minion and its summon each react once: " + string.Join(",", Events(cursor)));
                        // Hero power reacts after its effect.
                        game.PowerHistory.Last.Clear(); human.UsedMana = 0; FlushHistory(match); cursor = capture.Packets.Count;
                        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask.Any(human, game.Player2.Hero)), "Fireblast accepted");
                        FlushHistory(match);
                        Check(Events(cursor).SequenceEqual(new[] { 1, 30 }), "hero power reaction follows its damage: " + string.Join(",", Events(cursor)));
                        // One turn-start reaction per real turn, from the new current player's side.
                        game.PowerHistory.Last.Clear(); cursor = capture.Packets.Count;
                        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(human)), "turn ended");
                        FlushHistory(match);
                        Check(Events(cursor).Count(e => e == 11 || e == 12) == 1 && Events(cursor).Contains(11), "one opposing turn start per turn: " + string.Join(",", Events(cursor)));
                    }
                }
                PetDiscoverReactions(match, capture, equipped);
                var finalReset = new List<E> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, game.Player1.Hero.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
                finalReset.AddRange(game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e => PowerHistoryBuilder.FullEntity(e, reset: true)));
                finalReset.Add(PowerHistoryBuilder.BlockEnd());
                cursor = capture.Packets.Count; SendHistoryEntries(match, finalReset);
                Check(NativeHistory(capture, cursor).Where(e => e.HasResetGame).SelectMany(e => e.ResetGame.CreateGame.Players)
                    .All(p => p.Entity.Tags.All(t => t.Name != (int)GameTag.LAST_CARD_DISCARDED)), "reset also filters internal discard marker with and without pet");
                Check(!NativeHistory(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Tag == 4038), "rewind snapshots never replay Discover events");
                Check(NativeHistory(capture, cursor).Where(e => e.HasResetGame).SelectMany(e => e.ResetGame.CreateGame.Players)
                    .All(p => p.Entity.Tags.All(t => t.Name != 32414)), "Discover completion is not saved controller state");
                var restoredOwner = game.CurrentPlayer;
                game.PowerHistory.Last.Clear(); cursor = capture.Packets.Count;
                new SabberStoneCore.EffectSystem.EffectContext(game, restoredOwner.Id, restoredOwner.Hero.Id)
                    .Discover(new[] { SabberStoneCore.Model.Cards.FromId("CS2_231") });
                Check(SabberStoneCore.Actions.Generic.ChoicePick(restoredOwner, game, restoredOwner.Choice.Choices[0]), "Discover after rewind accepted");
                FlushHistory(match);
                Check(NativeHistory(capture, cursor).Where(e => e.HasTagChange && e.TagChange.Tag == 4038).Select(e => e.TagChange.Value)
                    .SequenceEqual(equipped ? new[] { 35, 0 } : Array.Empty<int>()), "same Discover reaction delivered once after reset");
                capture.SerializeAll();
            }
        } finally {
            NativeMatch.CosmeticsProfile = previousProfile; NativeMatch.NativePetVariant = previousVariant;
            Directory.Delete(root, true);
        }
    }
    private static void PetDiscoverReactions(NativeMatch match, Capture capture, bool equipped)
    {
        var game = match.Game;
        int cursor = capture.Packets.Count;
        void Flush() { FlushHistory(match); game.PowerHistory.Last.Clear(); }
        void Expect(params int[] values) {
            var rows = NativeHistory(capture, cursor);
            var events = rows.Where(e => e.HasTagChange && e.TagChange.Tag == 4038)
                .Select(e => e.TagChange.Value).Where(value => value == 35 || value == 36).ToArray();
            Check(events.SequenceEqual(equipped ? values : Array.Empty<int>()),
                "completed Discover reactions: expected " + string.Join(",", equipped ? values : Array.Empty<int>()) + " got " + string.Join(",", events));
            Check(!rows.Any(e => e.HasTagChange && e.TagChange.Tag == 32414), "private Discover completion never reaches native tags");
            cursor = capture.Packets.Count;
        }
        foreach (var owner in new[] { game.Player1, game.Player2 }) {
            if (game.CurrentPlayer != owner) Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(game.CurrentPlayer)), "Discover fixture turn");
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            var context = new SabberStoneCore.EffectSystem.EffectContext(game, owner.Id, owner.Hero.Id);
            var options = new[] { SabberStoneCore.Model.Cards.FromId("CS2_231") };
            Flush(); cursor = capture.Packets.Count;
            int expected = owner == game.Player1 ? 36 : 35;
            // A real completion must precede its continuation, even when it produces more gameplay history.
            context.Discover(options, afterChoose: new SabberStoneCore.Tasks.SimpleTasks.CustomTask((g, c, source, target, stack) => {
                c.Opponent.Hero.TakeDamage(c.Hero, 1);
            }));
            Flush(); Expect(); // Opening a picker is not completion.
            Check(!SabberStoneCore.Actions.Generic.ChoicePick(owner, game, int.MaxValue), "invalid Discover choice rejected");
            Flush(); Expect();
            Check(SabberStoneCore.Actions.Generic.ChoicePick(owner, game, owner.Choice.Choices[0]), "real Discover answer accepted");
            Flush();
            if (equipped) {
                var events = NativeHistory(capture, cursor).Where(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Value != 0).Select(e => e.TagChange.Value).ToArray();
                Check(events.SequenceEqual(new[] { expected, owner == game.Player1 ? 1 : 3 }), "Discover event precedes continuation damage");
            }
            Expect(expected);
            for (int repeat = 0; repeat < 2; repeat++) {
                context.Discover(options);
                Check(SabberStoneCore.Actions.Generic.ChoicePick(owner, game, owner.Choice.Choices[0]), "repeated Discover accepted");
            }
            Flush(); Expect(expected, expected);
            SabberStoneCore.Actions.Generic.CreateChoiceCards(owner, owner.Hero, null, ChoiceType.GENERAL, SabberStoneCore.Model.ChoiceAction.HAND, options, null);
            Check(SabberStoneCore.Actions.Generic.ChoicePick(owner, game, owner.Choice.Choices[0]), "fixed non-Discover choice accepted");
            Flush(); Expect();
            Check(!context.Discover(Array.Empty<SabberStoneCore.Model.Card>()), "empty Discover has no choice");
            Flush(); Expect();
            while (!owner.HandZone.IsFull) SabberStoneCore.Model.Entities.Entity.FromCard(owner, options[0], zone: owner.HandZone);
            context.Discover(options);
            Check(SabberStoneCore.Actions.Generic.ChoicePick(owner, game, owner.Choice.Choices[0]), "full-hand Discover accepted");
            Check(owner.HandZone.Count == 10, "Discover overflow preserves hand capacity");
            Flush(); Expect(expected);
            new SabberStoneCore.Tasks.SimpleTasks.DiscoverTask(CardType.MINION, keepAllCondition: _ => true).Process(game, owner, owner.Hero, null);
            Flush(); Expect(expected); // Keep-all is one completion, not one per granted card.
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            owner.BaseMana = 10; owner.UsedMana = 0; owner.Hero.IsImmune = true;
            var key = SabberStoneCore.Actions.Generic.DrawCard(owner, SabberStoneCore.Model.Cards.FromId("JAIL_319"));
            Flush(); cursor = capture.Packets.Count;
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner, key)), "Skeleton Key opens refreshable Discover");
            Flush(); Expect();
            void Answer(int selected) {
                var decision = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out var error), "Discover decision accepted: " + error);
            }
            Answer(owner.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "JAIL_319t"));
            Flush(); Expect();
            Answer(owner.Choice.Choices.First(id => game.IdEntityDic[id].Card.Id != "JAIL_319t"));
            Flush(); Expect(expected);
            owner.Hero.IsImmune = false;
        }
    }

}
