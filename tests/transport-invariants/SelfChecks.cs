using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using NativeHistory = PegasusGame.PowerHistory;
using NativeEntity = PegasusGame.PowerHistoryEntity;

namespace TransportInvariants;
using Card = SabberStoneCore.Model.Card;
using Tag = PegasusGame.Tag;
using Zone = SabberStoneCore.Enums.Zone;

internal static class SelfChecks
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("SELF-CHECK: " + message);
    }

    internal static void Run()
    {
        CardStateChecks.Run();
        HandCardStateChecks.Run();
        CostRulesChecks.Run();
        HeraldChecks.Run();
        ImbueProof.Run();
        OutcastProof.Run();
        EvHooksTransport.WatfinIdentity();
        var model = new ClientModel();
        void Apply(PowerHistoryData entry) => model.Apply(new NativeHistory { List = new() { entry } });
        NativeEntity Snapshot(string id, params (GameTag Tag, int Value)[] tags) => new() {
            Entity = 80, Name = id, Tags = tags.Select(t => new Tag { Name = (int)t.Tag, Value = t.Value }).ToList()
        };
        Apply(new() { FullEntity = Snapshot("EX1_335", (GameTag.ENTITY_ID, 80), (GameTag.ATK, 0), (GameTag.HEALTH, 5), (GameTag.COST, 4), (GameTag.TAUNT, 1)) });
        Check(model.Entities.ContainsKey(80), "FULL must create a client entity");
        Apply(new() { ShowEntity = Snapshot("EX1_335", (GameTag.DAMAGE, 2)) });
        Check(model.Entities[80].Get(GameTag.TAUNT) == 1 && model.Entities[80].Get(GameTag.HEALTH) == 5, "SHOW must merge, not replace");
        Apply(new() { ChangeEntity = Snapshot("CS2_231", (GameTag.ATK, 1), (GameTag.HEALTH, 1), (GameTag.TAUNT, 0)) });
        Check(model.Entities[80].CardId == "CS2_231" && model.Entities[80].Get(GameTag.COST) == 4 && model.Entities[80].Get(GameTag.TAUNT) == 0,
            "CHANGE must change identity and merge explicit zero");
        Apply(new() { HideEntity = new() { Entity = 80, Zone = (int)Zone.DECK } });
        Check(model.Entities[80].CardId == "" && model.Entities[80].Get(GameTag.DAMAGE) == 0
            && model.Entities[80].Get(GameTag.COST) == 0 && model.Entities[80].Get(GameTag.ZONE) == (int)Zone.DECK,
            "HIDE must clear identity, reset printed stats and move zone");
        Apply(new() { TagChange = new() { Entity = 80, Tag = (int)GameTag.ATK, Value = 7 } });
        Apply(new() { MetaData = new() { Type = HistoryMeta.Type.DAMAGE, Data = 99, Info = new() { 80 } } });
        Check(model.Entities[80].Get(GameTag.ATK) == 7 && model.Entities[80].Get(GameTag.DAMAGE) == 0,
            "metadata must not apply damage twice");
        Check(model.Entities[80].Get(GameTag.ARMOR) == 0, "missing numeric tags read zero");

        var packets = new List<IProtoBuf>();
        var match = new NativeMatch((_, packet) => packets.Add(packet), "priest", "mage", 0);
        match.Start();
        var choice = packets.OfType<EntityChoices>().Last();
        match.SendChoices(choice.Id, new());
        var session = new NativeSession(match);
        foreach (var packet in packets) session.Receive(0, packet);
        match.Game.PowerHistory.Last.Clear();
        match.Game.Player1.BaseMana = 10;
        var card = SabberStoneCore.Actions.Generic.DrawCard(match.Game.Player1, Cards.FromId("EX1_335"));
        // Fixture draw/setup are flushed separately; the actual play uses production Process/SendHistory.
        session.Flush();
        session.Process(PlayCardTask.Minion(match.Game.Player1, card));
        session.Client = new ClientModel();
        foreach (var packet in packets) session.Receive(0, packet);
        var lightspawn = match.Game.Player1.BoardZone.Single(m => m.Id == card.Id);
        Check(lightspawn.AttackDamage == 5 && lightspawn.Health == 5, "Lightspawn fixture must be effective 5/5");
        var found = Invariants.Compare(match.Game, session.Client).ToList();
        Check(!found.Any(v => v.Tag is "ZONE" or "ZONE_POSITION"), "normal hero/minion locations must match native zone and one-based positions");
        int attackOnWire = session.Client.Entities[card.Id].Get(GameTag.ATK);
        // The repaired transport must publish the getter, not merely let the oracle diagnose its absence.
        Check(attackOnWire == 5 && !found.Any(v => v.Entity == card.Id && v.Tag == "ATK"),
            "real NativeMatch must publish Lightspawn's effective attack");
        session.Client.Entities[card.Id].Tags.Remove((int)GameTag.ATK);
        Check(Invariants.Compare(match.Game, session.Client).Any(v => v.Entity == card.Id && v.Tag == "ATK" && v.Expected == "5" && v.Shown == "0"),
            "dropping derived Lightspawn attack must be detected even after the plugin is repaired");
        session.Client.Entities[card.Id].Tags[(int)GameTag.ATK] = 5;
        Check(!Invariants.Compare(match.Game, session.Client).Any(v => v.Entity == card.Id && v.Tag == "ATK"),
            "published derived attack must remove Lightspawn violation");
        foreach (var tag in new[] { GameTag.HEALTH, GameTag.DAMAGE, GameTag.COST, GameTag.ARMOR, GameTag.EXHAUSTED,
            GameTag.TAUNT, GameTag.DIVINE_SHIELD, GameTag.STEALTH, GameTag.FROZEN, GameTag.WINDFURY, GameTag.CHARGE,
            GameTag.SILENCED, GameTag.SPELLPOWER, GameTag.ZONE, GameTag.ZONE_POSITION, GameTag.CONTROLLER })
        {
            var tags = session.Client.Entities[card.Id].Tags;
            bool present = tags.TryGetValue((int)tag, out int before);
            tags[(int)tag] = before + 10;
            Check(Invariants.Compare(match.Game, session.Client).Any(v => v.Entity == card.Id && v.Tag == tag.ToString()), "missed corrupted visible " + tag);
            if (present) tags[(int)tag] = before; else tags.Remove((int)tag);
        }
        var hidden = match.Game.Player2.HandZone.First();
        session.Client.Entities[hidden.Id].Tags[(int)GameTag.ATK] = 999;
        Check(!Invariants.Compare(match.Game, session.Client).Any(v => v.Entity == hidden.Id), "opponent hand must stay outside visible comparisons");
        session.Client.Entities.Remove(card.Id);
        Check(Invariants.Compare(match.Game, session.Client).Any(v => v.Entity == card.Id && v.Tag == "ENTITY"),
            "missing visible entity must not be silently skipped");
        TradeReturningSource();
        AuraRemovalUsesProductionBridge();
        HeroShieldAndSilenceHealth();
        WeaponRemap();
        MultipleMinionTypes();
        OpponentCopiedCostReset();
        OpponentHiddenTransformReveal();
        LightningJoltDamage();
        LegacyRetainedTaunt();
        Console.WriteLine("PASS transport self-checks: lifecycle, metadata, all stat mutations, privacy, real Start/Lightspawn, aura/copy bridges, weapon remap, copied cost reset");
    }

    private static void LegacyRetainedTaunt()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
        session = new(match);
        match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game; var owner = game.Player1;
        game.PowerHistory.Last.Clear();
        var buffed = (Minion)SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CS2_182"));
        buffed.HasTaunt = true; session.Flush(); game.PowerHistory.Last.Clear();
        SabberStoneCore.Actions.Generic.ChangeEntityBlock(owner, buffed, Cards.FromId("BOT_236"), false);
        var retained = game.PowerHistory.Last.ToList(); game.PowerHistory.Last.Clear();
        Check(buffed.HasTaunt, "engine retains explicit Taunt through identity change");
        buffed.HasTaunt = false;
        session.Invoke("SendHistory", retained);
        Check(session.Client.Entities[buffed.Id].Get(GameTag.TAUNT) == 1,
            "historical CHANGE retains granted Taunt independently of later live mutation");
        session.Flush(); game.PowerHistory.Last.Clear();
        Console.WriteLine("PASS legacy retained Taunt: historical CHANGE survives later live mutation");
    }

    private static void TradeReturningSource()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "priest", "mage", 0);
        session = new(match);
        match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game; var player = game.Player1;
        game.PowerHistory.Last.Clear(); player.BaseMana = 10; player.UsedMana = 0;
        foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
        var ninja = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("TLC_513t2"));
        player.DeckZone.Add(ninja);
        var source = SabberStoneCore.Actions.Generic.DrawCard(player, Cards.FromId("TLC_255"));
        session.Flush();
        string trace = null; match.TradeTrace = value => trace = value;
        session.PublishDecision(player); session.Process(TradeCardTask.Any(player, source));
        Check(player.HandZone.Single() == source && ninja.Zone == player.BoardZone,
            "Summoned When Drawn replacement draws the traded entity back from the otherwise empty deck");
        using var record = System.Text.Json.JsonDocument.Parse(trace);
        Check(record.RootElement.GetProperty("humanReplacementEntity").GetInt32() == source.Id,
            "Trade proof records actual replacement even when it was in the pre-trade hand");
    }

    private static void HeroShieldAndSilenceHealth()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "priest", "mage", 0);
        session = new(match);
        match.Start();
        match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game;
        var hero = game.Player1.Hero;
        game.PowerHistory.Last.Clear();
        hero.Armor = 5;
        hero[GameTag.DIVINE_SHIELD] = 1;
        session.Flush();
        Check(session.Client.Entities[hero.Id].Get(GameTag.DIVINE_SHIELD) == 1,
            "hero shield grant must reach real NativeMatch packet");
        session.Client.Entities[hero.Id].Tags[(int)GameTag.DIVINE_SHIELD] = 0;
        Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == hero.Id && v.Tag == "DIVINE_SHIELD"),
            "oracle must detect missing hero shield");
        session.Client.Entities[hero.Id].Tags[(int)GameTag.DIVINE_SHIELD] = 1;
        game.PowerHistory.Last.Clear();
        hero.TakeDamage(game.Player2.Hero, 8);
        session.Flush();
        Check(session.Client.Entities[hero.Id].Get(GameTag.DIVINE_SHIELD) == 0
            && session.Client.Entities[hero.Id].Get(GameTag.DAMAGE) == 0
            && session.Client.Entities[hero.Id].Get(GameTag.ARMOR) == 5,
            "prevented hero hit emits shield loss without armor/health damage");
        Check(!Invariants.Compare(game, session.Client).Any(), "hero shield wire state agrees with engine");

        game.PowerHistory.Last.Clear();
        var recruit = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_101t"), zone: game.Player1.BoardZone);
        SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_222"), zone: game.Player1.BoardZone);
        game.DeathProcessingAndAuraUpdate();
        recruit.TakeDamage(game.Player2.Hero, 1);
        session.Flush();
        game.PowerHistory.Last.Clear();
        recruit.Silence();
        game.DeathProcessingAndAuraUpdate();
        session.Flush();
        Check(session.Client.Entities[recruit.Id].Get(GameTag.HEALTH) == 2
            && session.Client.Entities[recruit.Id].Get(GameTag.DAMAGE) == 1,
            "silenced aura recipient displays 1/2 without phantom healing");
        Check(!Invariants.Compare(game, session.Client).Any(), "Silence aura wire state agrees with engine");
    }

    private static void AuraRemovalUsesProductionBridge()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "priest", "mage", 0);
        session = new(match);
        match.Start();
        match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game;
        game.PowerHistory.Last.Clear();
        game.Player1.BaseMana = 10;
        var wisp = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.BoardZone);
        var champion = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_222"), zone: game.Player1.BoardZone);
        game.DeathProcessingAndAuraUpdate();
        var death = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("EX1_622"));
        session.Flush();
        session.Process(PlayCardTask.SpellTarget(game.Player1, death, (ICharacter)champion));
        Check(wisp[GameTag.ATK] == 1, "aura-removal fixture must remove Champion buff");
        Check(!Invariants.Compare(game, session.Client).Any(v => v.Entity == wisp.Id && v.Tag is "ATK" or "HEALTH"),
            "production AuraTransportBridge must run; unpatched engine would create false transport findings");
        game.PowerHistory.Last.Clear();
        wisp[GameTag.ATK] = 4;
        wisp[GameTag.HEALTH] = 5;
        wisp[GameTag.DAMAGE] = 2;
        wisp[GameTag.FROZEN] = 1;
        var faceless = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("EX1_564"));
        session.Flush();
        session.Process(PlayCardTask.Any(game.Player1, faceless, (ICharacter)wisp));
        Check(!Invariants.Compare(game, session.Client).Any(v => v.Entity == faceless.Id && v.Tag is "ATK" or "HEALTH" or "DAMAGE" or "FROZEN"),
            "production CopyTransportBridge must publish copied internal stats after CHANGE reset");
        Check(session.Client.Entities[faceless.Id].Get(GameTag.DAMAGE) == 2, "copy fixture must preserve damage");
    }

    private static void OpponentCopiedCostReset()
    {
        var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
            RandomSeed = 31, StartPlayer = 2, SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList()
        });
        game.StartGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.Flush();
        game.PowerHistory.Last.Clear();
        var opponent = game.Player2;
        opponent.BaseMana = 10;
        var hidden = opponent.HandZone.First();
        hidden.Cost = 7;
        var original = SabberStoneCore.Actions.Generic.DrawCard(opponent, Cards.FromId("EDR_459"));
        original.Cost = 3;
        var agent = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(opponent, Cards.FromId("BE_541"), zone: opponent.BoardZone);
        session.Flush();
        game.PowerHistory.Last.Clear();
        agent.ToBeDestroyed = true;
        game.DeathProcessingAndAuraUpdate();
        session.Flush();
        var copy = opponent.HandZone.Single(p => p.Card.Id == "EDR_459" && p.Id != original.Id);
        Check(copy.Cost == 3 && session.Client.Entities[copy.Id].CardId == ""
            && session.Client.Entities[copy.Id].Get(GameTag.COST) == 0,
            "Agent of N'Zoth copy retains discounted cost without exposing hidden hand identity/cost");
        session.Process(PlayCardTask.Minion(opponent, copy));
        Check(copy.Zone == opponent.BoardZone && copy.Cost == 4 && opponent.UsedMana == 3,
            "copied Devastator must pay discounted cost and reset to printed cost in play");
        Check(session.Client.Entities[copy.Id].Get(GameTag.COST) == 4
            && !Invariants.Compare(game, session.Client).Any(v => v.Entity == copy.Id),
            "opponent copy must publish cost reset filtered while historical zone was HAND");
        Check(session.Client.Entities[hidden.Id].CardId == "" && session.Client.Entities[hidden.Id].Get(GameTag.COST) == 0,
            "reconciling public cost must not publish hidden opponent hand identity or cost");
    }

    private static void OpponentHiddenTransformReveal()
    {
        var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
            RandomSeed = 31, StartPlayer = 2, SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList()
        });
        game.StartGame();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        session.Flush();
        game.PowerHistory.Last.Clear();
        var opponent = game.Player2;
        opponent.BaseMana = 10;
        var hidden = opponent.HandZone.First();
        // Start publicly in SETASIDE so the hidden transform must clear a legitimately known identity.
        var scout = SabberStoneCore.Model.Entities.Entity.FromCard(opponent, Cards.FromId("CATA_552"), zone: opponent.SetasideZone);
        scout.Cost = 3;
        session.Flush();
        Check(session.Client.Entities[scout.Id].CardId == "CATA_552", "fixture must begin with a published Scout");
        game.PowerHistory.Last.Clear();
        opponent.SetasideZone.Remove(scout);
        SabberStoneCore.Actions.Generic.AddHandPhase(opponent, scout);
        var dragon = SabberStoneCore.Actions.Generic.DrawCard(opponent, Cards.FromId("EX1_043"));
        session.Flush();
        session.Process(PlayCardTask.Minion(opponent, dragon));
        Check(scout.Card.Id == "CATA_552t" && scout.Zone == opponent.HandZone,
            "playing a Dragon must transform the hidden Scout");
        Check(session.Client.Entities[scout.Id].CardId == "" && session.Client.Entities[scout.Id].Get(GameTag.CARDRACE) == 0,
            "hidden transformation must not publish new identity or Dragon race");
        session.Process(PlayCardTask.Any(opponent, scout, game.Player1.Hero));
        var shown = session.Client.Entities[scout.Id];
        Check(scout.Zone == opponent.BoardZone && shown.CardId == "CATA_552t" && shown.Get(GameTag.CARDRACE) == 24,
            "playing a previously published, hidden-transformed Scout must reveal its new identity and race");
        Check(!Invariants.Compare(game, session.Client).Any(v => v.Entity == scout.Id),
            "transformed Scout identity, stats and paid-cost reset must agree on wire");
        Check(session.Client.Entities[hidden.Id].CardId == "" && session.Client.Entities[hidden.Id].Get(GameTag.CARDRACE) == 0,
            "revealing a played transform must keep other opponent hand identities private");
        Console.WriteLine("PASS transport hidden transform: generated Scout, hidden CHANGE, play reveal, race/cost parity, opponent privacy");
    }

    private static void LightningJoltDamage()
    {
        foreach (bool boosted in new[] { false, true })
        {
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
            session = new(match);
            match.Start();
            match.SendChoices(match.HumanChoiceId, new());
            var game = match.Game;
            var owner = game.Player1;
            game.PowerHistory.Last.Clear();
            owner.BaseMana = 10;
            var hammer = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("AT_050"));
            session.Flush();
            session.Process(PlayCardTask.Any(owner, hammer));
            game.PowerHistory.Last.Clear();
            ((Weapon)hammer).Damage = 4;
            game.DeathProcessingAndAuraUpdate();
            Check(owner.Hero.HeroPower.Card.Id == "AT_050t", "Charged Hammer must grant Lightning Jolt without any EDR cards");
            SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("EX1_012"), zone: owner.BoardZone);
            game.DeathProcessingAndAuraUpdate();
            owner.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE] = 1;
            owner.Hero.HeroPowerDamage = boosted ? 3 : 0;
            owner.ControllerAuraEffects[GameTag.HERO_POWER_DOUBLE] = boosted ? 1 : 0;
            Check(owner.CurrentSpellPower == 1, "damage fixture must have Spell Damage +1");
            session.Flush();
            int before = game.Player2.Hero.Damage;
            session.Process(HeroPowerTask.Any(owner, game.Player2.Hero));
            Check(game.Player2.Hero.Damage - before == (boosted ? 10 : 2),
                "Lightning Jolt must use hero-power bonuses, never spell-damage bonuses");
            game.PowerHistory.Last.Clear();
            owner.UsedMana = 0;
            var fireball = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CS2_029"));
            session.Flush();
            before = game.Player2.Hero.Damage;
            session.Process(PlayCardTask.SpellTarget(owner, fireball, game.Player2.Hero));
            Check(game.Player2.Hero.Damage - before == 14, "Fireball must retain spell power and spell doubling without hero-power bonuses");
        }
        Console.WriteLine("PASS Lightning Jolt: legacy Charged Hammer, base/boosted hero power, spell-bonus isolation");
    }

    private static void MultipleMinionTypes()
    {
        CrossTypeRaceClearing();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
        session = new(match);
        match.Start();
        match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game;
        var owner = game.Player1;
        game.PowerHistory.Last.Clear();
        var minion = (Minion)SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CORE_LOE_039"));
        session.Flush();
        void Races(Minion entity, Race primary, int additional)
        {
            var shown = session.Client.Entities[entity.Id];
            Check(shown.Get(GameTag.CARDRACE) == (int)primary && shown.Get((GameTag)additional) == 1,
                "native primary and additional race must match printed identity");
            Check(!Invariants.Compare(game, session.Client).Any(v => v.Entity == entity.Id
                && (v.Tag == "CARDRACE" || Card.AdditionalRaceTags.Keys.Any(t => t.ToString() == v.Tag))),
                "native race invariant must pass");
        }
        Races(minion, Race.MECHANICAL, 2542);
        using (var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz"))
        using (var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress))
        using (var doc = System.Text.Json.JsonDocument.Parse(gzip))
        {
            var pinned = doc.RootElement.GetProperty("cards").EnumerateArray()
                .Single(row => row.GetProperty("cardId").GetString() == minion.Card.Id).GetProperty("tags");
            foreach (var tag in Card.AdditionalRaceTags.Keys.Append(GameTag.CARDRACE))
            {
                int expected = pinned.TryGetProperty(((int)tag).ToString(), out var value) ? value.GetInt32() : 0;
                // Non-overridden additional tags fall through to the pinned native card definition.
                int preview = NativeUIProjection.TryGetAuditDefinitionTag(true, minion.Card.Id, (int)tag, out int overridden)
                    ? overridden : expected;
                Check(preview == minion.Card[tag], "preview/native definition and engine races agree");
            }
        }
        game.PowerHistory.Last.Clear();
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(minion));
        session.Flush();
        Races(minion, Race.MECHANICAL, 2542);
        session.Client.Entities[minion.Id].Tags[2542] = 0;
        Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == minion.Id && v.Tag == "2542"),
            "missing additional race must fail transport invariant");
        session.Client.Entities[minion.Id].Tags[2542] = 1;
        game.PowerHistory.Last.Clear();
        minion = (Minion)SabberStoneCore.Actions.Generic.ChangeEntityBlock(owner, minion, Cards.FromId("TLC_225"), true);
        session.Flush();
        Races(minion, Race.ELEMENTAL, 2536);
        Check(session.Client.Entities[minion.Id].Get((GameTag)2542) == 0, "transform clears previous Beast flag");
        game.PowerHistory.Last.Clear();
        minion = (Minion)SabberStoneCore.Actions.Generic.ChangeEntityBlock(owner, minion, Cards.FromId("CS2_182"), true);
        session.Flush();
        Check(session.Client.Entities[minion.Id].Get(GameTag.CARDRACE) == 0
            && Card.AdditionalRaceTags.Keys.All(t => session.Client.Entities[minion.Id].Get(t) == 0),
            "transform to typeless clears all race flags");
        foreach (string target in new[] { "CS2_182", "TLC_225" })
        {
            game.PowerHistory.Last.Clear();
            var shuffled = (Minion)SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CORE_LOE_039"));
            session.Flush();
            Races(shuffled, Race.MECHANICAL, 2542);
            game.PowerHistory.Last.Clear();
            owner.HandZone.Remove(shuffled);
            Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(owner, null, shuffled), "race fixture must shuffle successfully");
            session.Flush();
            Check(session.Client.Entities[shuffled.Id].CardId == ""
                && session.Client.Entities[shuffled.Id].Get((GameTag)2542) == 1,
                "HIDE must retain previously published Beast flag while clearing identity");
            game.PowerHistory.Last.Clear();
            shuffled = (Minion)SabberStoneCore.Actions.Generic.ChangeEntityBlock(owner, shuffled, Cards.FromId(target), true);
            session.Flush();
            Check(session.Client.Entities[shuffled.Id].CardId == ""
                && session.Client.Entities[shuffled.Id].Get((GameTag)2542) == 1
                && session.Client.Entities[shuffled.Id].Get((GameTag)2536) == 0,
                "hidden transform must not disclose new identity or race flags");
            game.PowerHistory.Last.Clear();
            Check(SabberStoneCore.Actions.Generic.Draw(owner, shuffled) == shuffled,
                "race fixture must draw the transformed entity");
            session.Flush();
            Check(session.Client.Entities[shuffled.Id].CardId == target
                && session.Client.Entities[shuffled.Id].Get((GameTag)2542) == 0,
                "draw after hidden transform must clear stale Beast flag: " + target
                    + "; shown=" + session.Client.Entities[shuffled.Id].CardId
                    + "; Beast=" + session.Client.Entities[shuffled.Id].Get((GameTag)2542));
            if (target == "TLC_225") Races(shuffled, Race.ELEMENTAL, 2536);
            else Check(session.Client.Entities[shuffled.Id].Get(GameTag.CARDRACE) == 0
                && Card.AdditionalRaceTags.Keys.All(t => session.Client.Entities[shuffled.Id].Get(t) == 0),
                "draw after hidden typeless transform must clear every race flag");
        }
        game.PowerHistory.Last.Clear();
        var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CORE_LOE_039"), zone: game.Player2.DeckZone);
        SabberStoneCore.Actions.Generic.Draw(game.Player2, hidden.ZonePosition);
        session.Flush();
        Check(session.Client.Entities[hidden.Id].CardId == ""
            && !session.Client.Entities[hidden.Id].Tags.ContainsKey(2542), "opponent hand does not disclose race");
        Console.WriteLine("PASS transport multiple minion types: FULL/SHOW/CHANGE, stale flags, hidden transform/draw, corruption, privacy");
    }

    private static void CrossTypeRaceClearing()
    {
        var failures = new List<string>();
        foreach (bool hidden in new[] { false, true })
        foreach (string target in new[] { "CS2_029", "CS2_106" })
        {
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
            session = new(match);
            match.Start();
            match.SendChoices(match.HumanChoiceId, new());
            var game = match.Game;
            var owner = game.Player1;
            game.PowerHistory.Last.Clear();
            var entity = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CORE_LOE_039"));
            session.Flush();
            var shown = session.Client.Entities[entity.Id];
            Check(shown.Get(GameTag.CARDRACE) == 17 && shown.Get((GameTag)2542) == 1,
                "cross-type fixture must publish Mech and Beast before transforming");
            if (hidden)
            {
                game.PowerHistory.Last.Clear();
                owner.HandZone.Remove(entity);
                Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(owner, null, entity), "cross-type fixture shuffle");
                session.Flush();
            }
            game.PowerHistory.Last.Clear();
            entity = SabberStoneCore.Actions.Generic.ChangeEntityBlock(owner, entity, Cards.FromId(target), true);
            session.Flush();
            if (hidden)
            {
                Check(shown.CardId == "" && shown.Get(GameTag.CARDRACE) == 17 && shown.Get((GameTag)2542) == 1,
                    "hidden cross-type transform must not disclose identity or race changes");
                game.PowerHistory.Last.Clear();
                Check(SabberStoneCore.Actions.Generic.Draw(owner, entity) == entity, "cross-type fixture reveal");
                session.Flush();
            }
            Check(shown.CardId == target && shown.Get(GameTag.CARDTYPE) == (int)entity.Card.Type,
                "cross-type transform must publish destination identity and type");
            if (shown.Get(GameTag.CARDRACE) != 0 || Card.AdditionalRaceTags.Keys.Any(t => shown.Get(t) != 0))
                failures.Add($"{(hidden ? "hidden/reveal" : "visible")} {target}: CARDRACE={shown.Get(GameTag.CARDRACE)}, Beast={shown.Get((GameTag)2542)}");
        }
        Check(failures.Count == 0, "cross-type transforms must clear every previous race: " + string.Join("; ", failures));
        Console.WriteLine("PASS transport cross-type races: visible/hidden minion-to-spell/weapon clears primary and additional tags");
    }

    private static void WeaponRemap()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "warrior", "mage", 0);
        session = new(match);
        match.Start();
        match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game;
        game.PowerHistory.Last.Clear();
        game.Player1.BaseMana = 10;
        var weapon = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("CS2_106"));
        session.Flush();
        session.Process(PlayCardTask.Any(game.Player1, weapon));
        game.PowerHistory.Last.Clear();
        ((SabberStoneCore.Model.Entities.Weapon)weapon).Damage = 1;
        session.Flush();
        Check(session.Client.Entities[weapon.Id].Get(GameTag.HEALTH) == 2 && session.Client.Entities[weapon.Id].Get(GameTag.DAMAGE) == 1,
            "weapon transport must map base durability to HEALTH and retain DAMAGE");
        Check(!Invariants.Compare(game, session.Client).Any(v => v.Entity == weapon.Id && v.Tag == "DURABILITY"), "remaining weapon durability must match");
        session.Client.Entities[weapon.Id].Tags[(int)GameTag.HEALTH] = 9;
        Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == weapon.Id && v.Tag == "DURABILITY"), "corrupt weapon health must fail durability");
    }
}
