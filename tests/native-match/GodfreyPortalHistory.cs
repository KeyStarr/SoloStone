using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    [GroupProof("godfrey-portal", ProofKind.Native)]
    private static void GodfreyPortalHistory()
    {
        foreach (bool enemy in new[] { false, true })
        {
            var (match, capture) = SogStart(enemy ? Array.Empty<string>() : new[] { "JAIL_509" },
                enemy ? new[] { "JAIL_509" } : Array.Empty<string>());
            SogComplete(match, capture);
            var game = match.Game;
            var owner = enemy ? game.Player2 : game.Player1;
            var effect = owner.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_509e");
            Check(effect.Target == owner, "Godfrey enchantment attaches to player, not hero");
            Check(owner.Hero[(GameTag)4717] == 1 && owner[(GameTag)4717] == 0,
                "Godfrey portal flag belongs to hero, independently of empty backlog");
            Check(DrawRows(capture).Any(r => r.HasTagChange && r.TagChange.Entity == owner.Hero.Id
                && r.TagChange.Tag == 4717 && r.TagChange.Value == 1), "both viewers receive hero portal activation");
            Check(DrawRows(capture).SelectMany(r => new[] { r.FullEntity, r.ShowEntity }).Any(e => e?.Entity == effect.Id
                && e.Name == "JAIL_509e"
                && e.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == owner.Id)),
                "both viewers receive player attachment at start of game");
            game.PowerHistory.Last.Clear();
            void Flush() { SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear(); }
            while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            var first = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            var second = EngineEntity.FromCard(owner, Cards.FromId("CS2_024"), zone: owner.DeckZone);
            Flush();
            int deck = owner.DeckZone.Count;
            capture.Packets.Clear();
            Generic.Draw(owner, first); Generic.Draw(owner, second); Flush();
            Check(game.HistoryFacts.Query.Events<CardBurnedFact>().Count(f => f.CardId == first.Id || f.CardId == second.Id) == 0,
                "retained overdraw is presentation only, never a destruction fact");
            var burns = DrawRows(capture).ToList();
            var displays = burns.Where(r => r.HasShowEntity && r.ShowEntity.Tags.Any(t => t.Name == 4722 && t.Value == 319))
                .Select(r => r.ShowEntity).ToArray();
            Check(displays.Length == 2 && displays.Select(e => e.Name).OrderBy(s => s)
                .SequenceEqual(new[] { "CS2_024", "CS2_029" }), "both viewers see exact public overdraw identities");
            foreach (var display in displays)
            {
                int show = burns.FindIndex(r => r.ShowEntity == display);
                int mill = burns.FindIndex(r => r.ShowEntity == display
                    && r.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)EngineZone.GRAVEYARD));
                int hide = burns.FindIndex(r => r.HasHideEntity && r.HideEntity.Entity == display.Entity);
                Check(display.Entity != first.Id && display.Entity != second.Id && show == mill && hide < 0,
                    "public burn uses disposable graveyard actor without a second move");
                Check(display.Tags.All(t => !PayloadRules.IsInternalTag((GameTag)t.Name)
                    && t.Name != (int)GameTag.ATTACHED && t.Name != (int)GameTag.CREATOR), "burn has no retained payload links");
            }
            Check(!burns.Any(r => r.HasSubSpellStart), "native handfull override owns burn; no duplicate subspell");
            Check(burns.SelectMany(r => new[] { r.FullEntity, r.ShowEntity, r.ChangeEntity })
                .Where(e => e?.Entity == first.Id || e?.Entity == second.Id).All(e => string.IsNullOrEmpty(e.Name)),
                "retained originals stay unnamed during public burn");
            Check(first.Zone == owner.SetasideZone && second.Zone == owner.SetasideZone
                && owner.DeckZone.Count == deck - 2, "Godfrey overdraw consumes actual deck cards");
            Check(effect[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2, "Godfrey publishes backlog count for native deck tooltip");
            Check(DrawRows(capture).Any(r => r.HasTagChange && r.TagChange.Entity == effect.Id
                && r.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_2 && r.TagChange.Value == 2),
                "both viewers receive backlog count");
            // Retail may reveal the overdraw event; pool contents and the random return remain private.
            capture.Packets.Clear();
            int draws = owner.NumCardsDrawnThisTurn;
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
            var returned = new[] { first, second }.Single(c => c.Zone == owner.HandZone);
            var returns = DrawRows(capture).ToList();
            int spawn = returns.FindIndex(r => r.HasSubSpellStart && r.SubSpellStart.SpellPrefabGuid == DrawReplacementRules.ReturnPrefab);
            int wire = match.WireReturnId(returned.Id);
            int hand = returns.FindIndex(r => r.HasTagChange && r.TagChange.Entity == wire
                && r.TagChange.Tag == (int)GameTag.ZONE && r.TagChange.Value == (int)EngineZone.HAND);
            Check(spawn >= 0 && hand > spawn && returns[spawn].SubSpellStart.SourceEntityId == owner.Hero.Id
                && returns[spawn].SubSpellStart.TargetEntityIds.SequenceEqual(new[] { wire })
                && returns[spawn + 1].HasSubSpellEnd, "both viewers install explicit card spawn override before hand entry");
            if (enemy)
            {
                Check(wire != first.Id && wire != second.Id && displays.All(e => e.Entity != wire),
                    "private return cannot be correlated with either public burn or original actor");
                var admitted = returns.Single(r => r.HasFullEntity && r.FullEntity.Entity == wire).FullEntity;
                Check(admitted.Name == "" && admitted.Tags.All(t => t.Name == (int)GameTag.ENTITY_ID
                    || t.Name == (int)GameTag.CONTROLLER || t.Name == (int)GameTag.ZONE || t.Name == (int)GameTag.ZONE_POSITION),
                    "fresh return actor exposes no original link, premium or definition");
                Check(!returns.Any(r => r.HasTagChange && (r.TagChange.Entity == first.Id || r.TagChange.Entity == second.Id)),
                    "return does not touch either burn-correlated original");
            }
            Check(returns.Any(r => r.HasPowerStart && r.PowerStart.Source == owner.Id && r.PowerStart.EffectIndex == -1),
                "return lifecycle block survives empty-trigger suppression without invented effect selector");
            Check(returned.Cost == returned.Card.Cost - 1 && owner.NumCardsDrawnThisTurn == draws
                && owner.DeckZone.Count == deck - 2 && effect[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1,
                "Godfrey returns automatically with discount, without drawing or consuming deck");
            foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
            int fatigue = owner.Hero.Fatigue;
            Generic.Draw(owner); Flush();
            Check(owner.Hero.Fatigue == fatigue + 1 && effect[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1,
                "retained pool does not replace empty-deck fatigue");
            owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
            Check(effect[GameTag.TAG_SCRIPT_DATA_NUM_2] == 0 && first.Zone == owner.HandZone && second.Zone == owner.HandZone,
                "last retained card returns even with empty deck; tooltip count clears");
            Check(owner.Hero[(GameTag)4717] == 1, "empty backlog does not switch off permanent deck portal");
            if (enemy) { AssertHiddenDraw(capture, match.WireReturnId(first.Id), "Godfrey pool"); AssertHiddenDraw(capture, match.WireReturnId(second.Id), "Godfrey pool"); }
            var oldHero = owner.Hero;
            var replacement = (SabberStoneCore.Model.Entities.Hero)EngineEntity.FromCard(owner, Cards.FromId("HERO_07"));
            Generic.PlayHero(owner, game, replacement, null, 0); Flush();
            Check(oldHero[(GameTag)4717] == 0 && replacement[(GameTag)4717] == 1,
                "player's persistent portal migrates on hero replacement");
            capture.Packets.Clear();
            effect.Remove(); Flush();
            Check(replacement[(GameTag)4717] == 0 && DrawRows(capture).Any(r => r.HasTagChange
                && r.TagChange.Entity == replacement.Id && r.TagChange.Tag == 4717 && r.TagChange.Value == 0),
                "removing protection requests portal Death on either seat");
            capture.SerializeAll();
        }
        GodfreySprintZones();
        GodfreyTerminalPortals();
        GodfreyPremiumBurns();
        GodfreyReturnReconstruction();
        Console.WriteLine("PASS Godfrey player attachment, public count, retained return, fatigue and privacy on both seats");
    }

    private static void GodfreySprintZones()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var herald = new[] { "TLC_451", "CORE_UNG_205", "CORE_UNG_205", "CATA_496", "CATA_496", "CATA_725", "CATA_725",
                "CORE_CFM_790", "CORE_CFM_790", "CORE_ICC_055", "CORE_ICC_055", "JAIL_515", "CAP_405", "CORE_CS2_062", "CORE_CS2_062",
                "JAIL_399", "JAIL_399", "JAIL_513", "JAIL_513", "CATA_492", "CATA_492", "JAIL_509", "JAIL_511", "JAIL_511",
                "CATA_497", "JAIL_510", "JAIL_510", "CATA_190h", "JAIL_514", "JAIL_514" }.Select(Cards.FromId).ToList();
            var filler = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList();
            var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig { RandomSeed = 1210897780, StartPlayer = seat,
                History = true, Logging = false, SkipMulligan = false, Player1Deck = seat == 1 ? herald : filler,
                Player2Deck = seat == 2 ? herald : filler, Player1HeroClass = CardClass.WARLOCK, Player2HeroClass = CardClass.WARLOCK });
            game.StartGame();
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear();
            // Apply the live fixture at its real boundary, after mulligans and before the first draw.
            foreach (var player in new[] { game.Player1, game.Player2 }.Where(p => p.Choice != null))
            {
                Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(player, new System.Collections.Generic.List<int>())), "Sprint fixture mulligan");
                SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear();
            }
            string prefix = seat == 1 ? "human" : "npc";
            DebugScenario.Parse(prefix + "Hand=CS2_077,CS2_231,CS2_231,CS2_231,CS2_231\n"
                + prefix + "DeckTop=CS2_182,CS2_200,CS2_179,CS2_029,CS2_024\n" + prefix + "Mana=10").Apply(game);
            game.MainBegin();
            SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear();
            var oracle = new TransportInvariants.ZoneStateOracle();
            int cursor = 0;
            void Verify(string step)
            {
                foreach (var packet in capture.Packets.Skip(cursor).Where(p => p.Id == 19))
                    oracle.Apply((PegasusGame.PowerHistory)packet.Message);
                cursor = capture.Packets.Count;
                var errors = oracle.Compare(game, match.WireReturnId).ToArray();
                Check(errors.Length == 0, "Sprint seat " + seat + " " + step + ": " + string.Join("; ", errors));
            }
            var owner = seat == 1 ? game.Player1 : game.Player2;
            Check(owner.HandZone.Count == 9 && owner.DeckZone.Count == 31, "exact live opening: hand 9 deck 31");
            Verify("opening");
            var sprint = owner.HandZone.Single(c => c.Card.Id == "CS2_077");
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner, sprint)), "Sprint accepted");
            SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear();
            Check(owner.HandZone.Count == 10 && owner.DeckZone.Count == 27
                && owner.SetasideZone.Where(c => c.Card.Id == "CS2_029" || c.Card.Id == "CS2_024").Count() == 2,
                "exact live overdraw: hand 10 deck 27, Fireball/Frostbolt retained");
            Verify("overdraw");
            for (int i = 0; i < 2; i++)
            {
                var wisp = owner.HandZone.First(c => c.Card.Id == "CS2_231");
                Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Minion(owner, wisp)), "Wisp accepted");
                SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear();
                Verify("Wisp " + (i + 1));
                Check(owner.HandZone.Count == 10 && owner.DeckZone.Count == 27
                    && owner.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_509e")[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1 - i,
                    "each Wisp returns exactly one retained card without drawing");
                if (seat == 2)
                    foreach (var card in owner.HandZone.Where(c => c.Card.Id == "CS2_029" || c.Card.Id == "CS2_024"))
                        AssertHiddenDraw(capture, match.WireReturnId(card.Id), "Sprint anonymous return");
            }
        }
    }

    private static void GodfreyTerminalPortals()
    {
        foreach (bool concede in new[] { false, true })
        {
            var (match, capture) = SogStart(new[] { "JAIL_509" }, new[] { "JAIL_509" });
            SogComplete(match, capture);
            capture.Packets.Clear(); match.Game.PowerHistory.Last.Clear();
            if (concede) match.Concede();
            else {
                var player = match.Game.CurrentPlayer;
                player.BaseMana = 10; player.UsedMana = 0; player.Opponent.Hero.Health = 6;
                var spell = Generic.DrawCard(player, Cards.FromId("CS2_029"));
                Check(match.Game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.SpellTarget(player, spell, player.Opponent.Hero)),
                    "lethal Fireball accepted through real player action");
                SogSendHistory(match, match.Game.PowerHistory.Last);
            }
            Check(match.Game.State == State.COMPLETE, "portal shutdown fixture reaches terminal state");
            foreach (var player in new[] { match.Game.Player1, match.Game.Player2 })
                Check(player.Hero[(GameTag)4717] == 0 && DrawRows(capture).Any(r => r.HasTagChange
                    && r.TagChange.Entity == player.Hero.Id && r.TagChange.Tag == 4717 && r.TagChange.Value == 0),
                    "both heroes request Death after " + (concede ? "concession" : "lethal"));
            capture.SerializeAll();
        }
    }

    private static void GodfreyPremiumBurns()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (int premium in new[] { 1, 2, 3 })
        {
            var (match, capture) = SogStart(enemy ? Array.Empty<string>() : new[] { "JAIL_509" },
                enemy ? new[] { "JAIL_509" } : Array.Empty<string>());
            SogComplete(match, capture);
            var owner = enemy ? match.Game.Player2 : match.Game.Player1;
            while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            var card = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            ((System.Collections.Generic.Dictionary<int, int>)typeof(NativeMatch).GetField("entityPremiums",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match))[card.Id] = premium;
            SogSendHistory(match, match.Game.PowerHistory.Last); match.Game.PowerHistory.Last.Clear(); capture.Packets.Clear();
            Generic.Draw(owner, card); SogSendHistory(match, match.Game.PowerHistory.Last);
            var shown = DrawRows(capture).Single(r => r.HasShowEntity && r.ShowEntity.Tags.Any(t => t.Name == 4722)).ShowEntity;
            Check(TagValue(shown.Tags, GameTag.PREMIUM) == premium
                && TagValue(shown.Tags, (GameTag)1932) == (premium == 2 ? 1 : 0)
                && TagValue(shown.Tags, (GameTag)2589) == (premium == 3 ? 1 : 0),
                "burn actor inherits resolved owned premium " + premium + " on either seat");
            Check(shown.Tags.All(t => t.Name != (int)GameTag.LINKED_ENTITY && t.Name != (int)GameTag.CREATOR),
                "premium inheritance publishes no original-ID link");
        }
    }

    private static void GodfreyReturnReconstruction()
    {
        var (match, capture) = SogStart(Array.Empty<string>(), new[] { "JAIL_509" });
        SogComplete(match, capture);
        var game = match.Game; var owner = game.Player2;
        void Flush() { SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear(); }
        game.PowerHistory.Last.Clear();
        while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
        var cards = new[] { "CS2_182", "CS2_106" }.Select(id => EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone)).ToArray();
        Flush();
        foreach (var card in cards) Generic.Draw(owner, card);
        Flush();
        owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
        var returned = cards.Single(c => c.Zone == owner.HandZone);
        int alias = match.WireReturnId(returned.Id);
        void SnapshotPrivacy(NativeMatch viewer, Capture output)
        {
            var fulls = DrawRows(output).Where(r => r.HasFullEntity).Select(r => r.FullEntity).ToArray();
            foreach (var original in cards)
            {
                var old = fulls.Single(e => e.Entity == original.Id);
                Check(old.Name == "" && TagValue(old.Tags, GameTag.ZONE) == (int)EngineZone.SETASIDE,
                    "reset/reconnect preserves both burn-correlated originals identically in SETASIDE");
            }
            Check(fulls.Single(e => e.Entity == cards[0].Id).Tags.Select(t => t.Name).OrderBy(t => t)
                .SequenceEqual(fulls.Single(e => e.Entity == cards[1].Id).Tags.Select(t => t.Name).OrderBy(t => t)),
                "snapshot tag shape cannot distinguish retired actor from retained card");
            var hand = fulls.Single(e => e.Entity == viewer.WireReturnId(returned.Id));
            Check(hand.Entity != returned.Id && hand.Name == "" && TagValue(hand.Tags, GameTag.ZONE) == (int)EngineZone.HAND,
                "reset/reconnect reconstructs anonymous hand through alias");
            Check(fulls.Select(e => e.Entity).SequenceEqual(fulls.Select(e => e.Entity).OrderBy(id => id)),
                "snapshot ordering does not disclose alias-to-original mapping");
            var create = DrawRows(output).Single(r => r.HasResetGame).ResetGame.CreateGame;
            Check(create.Players.Single(player => player.Id == owner.PlayerId).Entity.Tags.Where(t => t.Name == (int)GameTag.LAST_CARD_DRAWN)
                .All(t => t.Value == 0), "reset/reconnect historical draw references cannot relink a known burn to its return alias: "
                + string.Join(",", create.Players.SelectMany(player => player.Entity.Tags.Where(t => t.Name == (int)GameTag.LAST_CARD_DRAWN).Select(t => player.Id + "=" + t.Value)))
                + " originals=" + string.Join(",", cards.Select(card => card.Id)));
            output.SerializeAll();
        }
        capture.Packets.Clear(); SogReset(match); SnapshotPrivacy(match, capture);
        Check(match.WireReturnId(returned.Id) == alias, "reset preserves live anonymous return identity");
        var reconnectCapture = new Capture();
        var reconnect = new NativeMatch(reconnectCapture.Send);
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(reconnect, SabberStoneCore.Model.Game.LoadSnapshot(game.SaveSnapshot()));
        SogReset(reconnect); SnapshotPrivacy(reconnect, reconnectCapture);
        var replayCapture = new Capture();
        var replay = new NativeMatch(replayCapture.Send);
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(replay, game);
        SogSendHistory(replay, game.PowerHistory.Full);
        var replayRows = DrawRows(replayCapture).ToArray();
        var pulse = replayRows.Single(r => r.HasSubSpellStart && r.SubSpellStart.SpellPrefabGuid == DrawReplacementRules.ReturnPrefab);
        Check(pulse.SubSpellStart.TargetEntityIds.Single() == replay.WireReturnId(returned.Id)
            && !cards.Any(c => c.Id == pulse.SubSpellStart.TargetEntityIds.Single()), "full replay rotates private return identity too");
        replayCapture.SerializeAll();
        capture.Packets.Clear();
        owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0])); Flush();
        var secondPulse = DrawRows(capture).Single(r => r.HasSubSpellStart && r.SubSpellStart.SpellPrefabGuid == DrawReplacementRules.ReturnPrefab);
        Check(secondPulse.SubSpellStart.TargetEntityIds.Single() != alias
            && !cards.Any(c => c.Id == secondPulse.SubSpellStart.TargetEntityIds.Single()), "second return after reset gets independent anonymous actor");
        returned = cards[0]; alias = match.WireReturnId(returned.Id);
        if (game.CurrentPlayer != owner) Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(game.CurrentPlayer)), "NPC turn begins");
        owner.BaseMana = 10; owner.UsedMana = 0;
        capture.Packets.Clear();
        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Minion(owner, returned)), "returned minion plays normally"); Flush();
        Check(DrawRows(capture).Any(r => r.HasPowerStart && r.PowerStart.Source == alias)
            && DrawRows(capture).Any(r => r.HasShowEntity && r.ShowEntity.Entity == alias && r.ShowEntity.Name == returned.Card.Id),
            "later public play reveals only the established alias");
        var weapon = cards[1];
        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner, weapon)), "returned weapon plays normally"); Flush();
        Check(DrawRows(capture).Any(r => r.HasTagChange && r.TagChange.Entity == owner.Hero.Id
            && r.TagChange.Tag == (int)GameTag.WEAPON && r.TagChange.Value == match.WireReturnId(weapon.Id)),
            "equipped weapon pointer uses return alias");
        var equippedCapture = new Capture();
        var equippedReconnect = new NativeMatch(equippedCapture.Send);
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(equippedReconnect,
            SabberStoneCore.Model.Game.LoadSnapshot(game.SaveSnapshot()));
        SogReset(equippedReconnect);
        var equippedSnapshots = DrawRows(equippedCapture).Where(r => r.HasFullEntity).Select(r => r.FullEntity).ToArray();
        int equippedAlias = equippedReconnect.WireReturnId(weapon.Id);
        Check(equippedAlias != weapon.Id && equippedSnapshots.Single(e => e.Entity == equippedAlias).Name == weapon.Card.Id
            && TagValue(equippedSnapshots.Single(e => e.Entity == owner.Hero.Id).Tags, GameTag.WEAPON) == equippedAlias,
            "reconnect after equip preserves hero pointer to publicly visible returned weapon");
        equippedCapture.SerializeAll();
        capture.Packets.Clear();
        var helper = SabberStoneCore.Model.Entities.Enchantment.GetInstance(owner, returned, returned, Cards.FromId("CS2_092e")); Flush();
        Check(DrawRows(capture).SelectMany(r => new[] { r.FullEntity, r.ShowEntity }).Any(e => e?.Entity == helper.Id
            && TagValue(e.Tags, GameTag.ATTACHED) == alias)
            && match.SnapshotVisibleTransportTags(helper)[(int)GameTag.ATTACHED] == alias,
            "public attachment and live audit both reference anonymous return alias");
        Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(owner)), "human turn begins"); Flush();
        var human = game.Player1; human.BaseMana = 10; human.UsedMana = 0;
        var fireball = Generic.DrawCard(human, Cards.FromId("CS2_029")); Flush();
        typeof(NativeMatch).GetMethod("SendHumanOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(match, null);
        var options = (PegasusGame.AllOptions)capture.Packets.Last(p => p.Id == 14).Message;
        int index = options.Options.FindIndex(o => o.MainOption?.Id == fireball.Id);
        Check(index >= 0 && options.Options[index].MainOption.Targets.Any(t => t.Id == alias)
            && match.ValidateOption(options.Id, index, alias, -1, 0).Target.Id == returned.Id,
            "wire option target maps back to correct engine entity");
        capture.SerializeAll();
    }
}
