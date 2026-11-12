using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void RevealAnimationHistory()
    {
        // Each case exercises real SendHistory; omitting either engine annotations or public projection must fail.
        WatcherBurnHistory();
        SpellAnimationHistory();
        DeckBurnControls();
        ShatterOverdrawHistory();
        SpellAnimationControls();
        DecisionSpellAnimationHistory();
        Console.WriteLine("PASS deck destruction reveals and spell animation metadata");
    }

    private static (NativeMatch Match, Capture Capture) AnimationMatch(bool botTurn = false, string deck = "mage")
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, deck, deck, 8427);
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        if (botTurn)
        {
            InvokeProcess(match, match.Game.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            FlushHistory(match);
        }
        match.Game.PowerHistory.Last.Clear();
        match.Game.CurrentPlayer.BaseMana = 10;
        match.Game.CurrentPlayer.UsedMana = 0;
        FlushHistory(match);
        match.Game.PowerHistory.Last.Clear();
        return (match, capture);
    }

    private static void WatcherBurnHistory()
    {
        foreach (bool bot in new[] { true, false })
        foreach (int deckSize in new[] { 6, 0, 1, 2, 3, -1 })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var owner = bot ? game.Player2 : game.Player1;
            foreach (var card in owner.DeckZone.Skip(deckSize < 0 ? 6 : deckSize).ToArray())
                owner.SetasideZone.Add(owner.DeckZone.Remove(card));
            var watcher = (Minion)EngineEntity.FromCard(owner, Cards.FromId("TLC_621"), zone: owner.BoardZone);
            if (deckSize < 0) watcher.Silence();
            FlushHistory(match);
            var victims = owner.DeckZone.Reverse().Take(deckSize < 0 ? 0 : 3).ToArray();
            var survivors = owner.DeckZone.Where(c => !victims.Contains(c)).Select(c => c.Id).ToArray();
            var hidden = game.Player1.DeckZone.Concat(game.Player2.DeckZone).Concat(game.Player2.HandZone)
                .Where(c => !victims.Contains(c)).Select(c => c.Id).ToHashSet();
            int hand = owner.HandZone.Count, fatigue = owner.Hero.Fatigue;
            Check(victims.Length == (deckSize < 0 ? 0 : Math.Min(3, deckSize)), "Watcher fixture victim count");
            Check(!History(capture, 0).Any(e => victims.Any(v => v.Id == SnapshotEntity(e)) &&
                !string.IsNullOrEmpty(SnapshotName(e))), "Deck identities hidden before destruction");
            game.PowerHistory.Last.Clear();
            int cursor = capture.Packets.Count;
            watcher.ToBeDestroyed = true;
            game.DeathProcessingAndAuraUpdate();
            FlushHistory(match);
            var data = History(capture, cursor);
            AssertBurns(data, victims);
            Check(!data.Any(e => hidden.Contains(SnapshotEntity(e)) && !string.IsNullOrEmpty(SnapshotName(e))),
                "Watcher revealed an unrelated deck/hand card");
            Check(owner.DeckZone.Select(c => c.Id).SequenceEqual(survivors) &&
                victims.All(c => owner.GraveyardZone.Contains(c)) && owner.GraveyardZone.Contains(watcher),
                "Watcher preserves survivor order and moves exact victims plus dying minion");
            Check(owner.HandZone.Count == hand && owner.Hero.Fatigue == fatigue, "Burn must not draw or fatigue");
            capture.SerializeAll();
        }
    }

    private static void AssertBurns(List<PowerHistoryData> data, IPlayable[] victims)
    {
        var reveals = data.Where(e => e.HasShowEntity && victims.Any(v => v.Id == e.ShowEntity.Entity)).ToArray();
        Check(reveals.Select(e => e.ShowEntity.Entity).SequenceEqual(victims.Select(v => v.Id)),
            "Missing or duplicate ordered public burn SHOW_ENTITY");
        foreach (var victim in victims)
        {
            int index = data.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == victim.Id);
            var show = data[index].ShowEntity;
            Check(show.Name == victim.Card.Id && TagValue(show.Tags, GameTag.ZONE) == (int)EngineZone.GRAVEYARD &&
                TagValue(show.Tags, GameTag.CONTROLLER) == victim.Controller.PlayerId &&
                TagValue(show.Tags, GameTag.CARDTYPE) == (int)victim.Card.Type,
                "Burn SHOW must carry actual identity/public tags and destination GRAVEYARD");
            Check(!data.Skip(index + 1).Any(e => IsZone(e, victim.Id, EngineZone.GRAVEYARD)),
                "Burn SHOW owns the graveyard transition without a duplicate zone task");
            Check(data.Take(index).Count(e => e.HasPowerStart) > data.Take(index).Count(e => e.HasPowerEnd),
                "Burn reveal belongs inside resolving effect");
        }
    }

    private static bool IsZone(PowerHistoryData entry, int entity, EngineZone zone) => entry.HasTagChange &&
        entry.TagChange.Entity == entity && entry.TagChange.Tag == (int)GameTag.ZONE && entry.TagChange.Value == (int)zone;

    private static void SpellAnimationHistory()
    {
        var (match, capture) = AnimationMatch(botTurn: true);
        var game = match.Game;
        var bot = game.Player2;
        var victim = (Minion)EngineEntity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone: game.Player1.BoardZone);
        var skipper = EngineEntity.FromCard(bot, Cards.FromId("TLC_427"), zone: bot.HandZone);
        FlushHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == skipper.Id));
        FlushHistory(match);
        var rock = bot.HandZone.Single(c => c.Card.Id == "WW_001t");
        var generated = History(capture, cursor).Where(e => SnapshotEntity(e) == rock.Id).ToArray();
        Check(generated.Any(e => e.HasFullEntity) && generated.All(e => string.IsNullOrEmpty(SnapshotName(e)) &&
            SnapshotTag(e, GameTag.CARDTYPE) == 0 && SnapshotTag(e, GameTag.COST) == 0),
            "Generated bot Rock must exist but remain private in hand");
        var silent = game.CloneExact(history: false);
        Check(silent.Process(silent.Player2.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD &&
            t.Source?.Id == rock.Id && t.Target?.Id == victim.Id)), "History-disabled Rock cast accepted");
        Check(silent.PowerHistory.Full.Count == 0 && ((Minion)silent.IdEntityDic[victim.Id]).Damage == 3,
            "History-disabled cast changes damage without producing annotations");
        cursor = capture.Packets.Count;
        InvokeProcess(match, bot.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD &&
            t.Source?.Id == rock.Id && t.Target?.Id == victim.Id));
        FlushHistory(match);
        var data = History(capture, cursor);
        AssertSpellMetadata(data, rock.Id, victim.Id, 3);
        var hitOrder = data.Where(e => e.HasMetaData || e.HasTagChange && e.TagChange.Entity == victim.Id &&
                (e.TagChange.Tag == (int)GameTag.PREDAMAGE || e.TagChange.Tag == (int)GameTag.DAMAGE))
            .Select(e => e.HasMetaData ? "meta:" + (int)e.MetaData.Type + ":" + e.MetaData.Data
                : "tag:" + e.TagChange.Tag + ":" + e.TagChange.Value).ToArray();
        Check(hitOrder.SequenceEqual(new[] { "meta:0:" + rock.Id, "tag:318:3", "tag:318:0", "meta:1:3", "tag:44:3" }),
            "Rock must serialize TARGET, PREDAMAGE/reset, DAMAGE metadata, then cumulative DAMAGE in native order");
        int reveal = data.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == rock.Id && e.ShowEntity.Name == "WW_001t");
        int play = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == rock.Id && (int)e.PowerStart.Type == (int)BlockType.PLAY);
        Check(reveal >= 0 && reveal < play && TagValue(data[reveal].ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND,
            "Bot Rock identity must precede PLAY and retain historical hand zone");
        Check(victim.Damage == 3 && rock.Zone == bot.GraveyardZone, "Rock deals three and resolves to graveyard");
        Check(!data.Any(e => bot.HandZone.Concat(bot.DeckZone).Any(c => c.Id == SnapshotEntity(e)) &&
            !string.IsNullOrEmpty(SnapshotName(e))), "Spell cast revealed unrelated bot cards");
        capture.SerializeAll();
    }

    private static void DeckBurnControls()
    {
        foreach (string cardId in new[] { "CATA_720", "CORE_ICC_407", "END_035" })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                foreach (var old in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(old));
                if (cardId == "END_035" && owner == game.Player1) continue;
                foreach (string id in new[] { "CS2_182", "CS2_231", "CS2_029", "CS2_168", "CS2_231", "CS2_182" })
                    EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            }
            var source = EngineEntity.FromCard(game.Player1, Cards.FromId(cardId), zone: game.Player1.HandZone);
            var victims = cardId == "CATA_720"
                ? game.Player1.DeckZone.Concat(game.Player2.DeckZone).Where(c => c.Cost <= 2).ToArray()
                : game.Player2.DeckZone.Reverse().Take(cardId == "END_035" ? 5 : 1).ToArray();
            var hidden = game.Player1.DeckZone.Concat(game.Player2.DeckZone).Concat(game.Player2.HandZone)
                .Where(c => !victims.Contains(c)).Select(c => c.Id).ToHashSet();
            FlushHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, game.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushHistory(match);
            var data = History(capture, cursor);
            AssertBurns(data, victims);
            Check(victims.All(c => c.Zone == c.Controller.GraveyardZone), cardId + " destroys exact cards");
            Check(!data.Any(e => hidden.Contains(SnapshotEntity(e)) && !string.IsNullOrEmpty(SnapshotName(e))),
                cardId + " reveals no unrelated cards");
            capture.SerializeAll();
        }
        // Ordinary draws remain private; full-hand draw uses the same public deck exit.
        foreach (bool fullHand in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: true);
            var game = match.Game;
            var bot = game.Player2;
            var draw = EngineEntity.FromCard(bot, Cards.FromId("CS2_023"), zone: bot.HandZone);
            if (fullHand) while (!bot.HandZone.IsFull) EngineEntity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.HandZone);
            var top = bot.DeckZone.Reverse().Take(2).ToArray();
            FlushHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == draw.Id));
            // Mutating after resolution must not overwrite the frozen burn snapshot.
            int burnCost = top[1].Cost;
            if (fullHand) top[1][GameTag.COST] = burnCost + 7;
            FlushHistory(match);
            var data = History(capture, cursor);
            if (fullHand)
            {
                AssertBurns(data, new[] { top[1] });
                Check(TagValue(data.Single(e => e.HasShowEntity && e.ShowEntity.Entity == top[1].Id).ShowEntity.Tags,
                    GameTag.COST) == burnCost, "Burn retains transition-time cost");
            }
            Check(!data.Any(e => (SnapshotEntity(e) == top[0].Id || !fullHand && SnapshotEntity(e) == top[1].Id) &&
                !string.IsNullOrEmpty(SnapshotName(e))), "Successful bot draw remains private");
            capture.SerializeAll();
        }
    }

    private static void ShatterOverdrawHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray())
                owner.SetasideZone.Add(old.Zone.Remove(old));
            var draw = EngineEntity.FromCard(owner, Cards.FromId("CS2_023"), zone: owner.HandZone);
            for (int i = 0; i < 8; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            // Intellect leaves eight cards and draws a ninth, then the parent into the one-space Shatter case
            // (Unpinned_ShatterCapacity counts space before entry, 49c82af4d).
            var parent = EngineEntity.FromCard(owner, Cards.FromId("CATA_489"), zone: owner.DeckZone);
            EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            FlushHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == draw.Id));
            FlushHistory(match);
            var burned = owner.GraveyardZone.Where(c => c.Card.Id == "CATA_489t2").ToArray();
            Check(burned.Length == 1 && owner.HandZone.Count == 10 && owner.HandZone.Any(c => c.Card.Id == "CATA_489t"),
                "Shatter overdraw retains left half and burns created right half");
            var data = History(capture, cursor);
            // Same packet assertions as ordinary Intellect overdraw in DeckBurnControls.
            AssertBurns(data, burned);
            Check(!data.Any(e => IsZone(e, burned[0].Id, EngineZone.HAND)), "Burned half never enters hand");
            Check(!data.Any(e => owner.HandZone.Any(c => c.Id == SnapshotEntity(e)) && bot &&
                !string.IsNullOrEmpty(SnapshotName(e))), "Shatter burn reveals no retained bot hand cards");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Shatter overdraw packet parity: both owners");
    }

    private static void SpellAnimationControls()
    {
        foreach (var scenario in new[] {
            (Card: "WW_001t", Hero: false, Armor: 0, Shield: false, Immune: false, SpellPower: false, Hit: 3),
            (Card: "WW_001t", Hero: false, Armor: 0, Shield: false, Immune: false, SpellPower: true, Hit: 4),
            (Card: "CS2_029", Hero: true, Armor: 2, Shield: false, Immune: false, SpellPower: false, Hit: 6),
            (Card: "CS2_029", Hero: true, Armor: 10, Shield: false, Immune: false, SpellPower: false, Hit: 6),
            (Card: "WW_001t", Hero: false, Armor: 0, Shield: true, Immune: false, SpellPower: false, Hit: 0),
            (Card: "WW_001t", Hero: true, Armor: 0, Shield: false, Immune: true, SpellPower: false, Hit: 0),
            (Card: "EDR_261", Hero: false, Armor: 0, Shield: false, Immune: false, SpellPower: false, Hit: 0) })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var source = EngineEntity.FromCard(game.Player1, Cards.FromId(scenario.Card), zone: game.Player1.HandZone);
            Character victim = scenario.Hero ? game.Player1.Hero :
                (Character)EngineEntity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone: game.Player1.BoardZone);
            victim.Damage = 1;
            victim.HasDivineShield = scenario.Shield;
            victim.IsImmune = scenario.Immune;
            game.Player1.Hero.Armor = scenario.Armor;
            if (scenario.SpellPower) EngineEntity.FromCard(game.Player1, Cards.FromId("CS2_142"), zone: game.Player1.BoardZone);
            game.DeathProcessingAndAuraUpdate();
            FlushHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, game.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD &&
                t.Source?.Id == source.Id && t.Target?.Id == victim.Id));
            FlushHistory(match);
            AssertSpellMetadata(History(capture, cursor), source.Id, victim.Id, scenario.Hit, scenario.Shield);
            Check(victim.Damage == 1 + Math.Max(0, scenario.Hit - scenario.Armor), "Damage state matches per-hit metadata");
            Check(game.Player1.Hero.Armor == Math.Max(0, scenario.Armor - scenario.Hit), "Armor absorbs only this hit");
            capture.SerializeAll();
        }
        var (control, packets) = AnimationMatch();
        var g = control.Game;
        var coin = EngineEntity.FromCard(g.Player1, Cards.FromId("GAME_005"), zone: g.Player1.HandZone);
        var attacker = (Minion)EngineEntity.FromCard(g.Player1, Cards.FromId("CS2_182"), zone: g.Player1.BoardZone);
        var defender = (Minion)EngineEntity.FromCard(g.Player2, Cards.FromId("CS2_200"), zone: g.Player2.BoardZone);
        attacker.IsExhausted = false;
        FlushHistory(control);
        int start = packets.Packets.Count;
        InvokeProcess(control, g.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == coin.Id));
        FlushHistory(control);
        InvokeProcess(control, g.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK &&
            t.Source?.Id == attacker.Id && t.Target?.Id == defender.Id));
        FlushHistory(control);
        var combat = History(packets, start);
        Check(!combat.Any(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.TARGET),
            "Untargeted Coin and ordinary attack have no TARGET metadata");
        var hits = combat.Where(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE).ToArray();
        Check(hits.Length == 2 && hits[0].MetaData.Data == attacker.AttackDamage && hits[0].MetaData.Info.SequenceEqual(new[] { defender.Id })
            && hits[1].MetaData.Data == defender.AttackDamage && hits[1].MetaData.Info.SequenceEqual(new[] { attacker.Id }),
            "Ordinary attack emits one DAMAGE metadata per combatant's hit");
        Check(defender.Damage == attacker.AttackDamage, "Attack damage still resolves normally");
        packets.SerializeAll();
    }

    private static void DecisionSpellAnimationHistory()
    {
        var (match, capture) = AnimationMatch();
        var game = match.Game;
        var first = EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        var second = EngineEntity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        EngineEntity.FromCard(game.Player1, Cards.FromId("CATA_484"), zone: game.Player1.HandZone);
        var spell = (SabberStoneCore.Model.Entities.Spell)EngineEntity.FromCard(game.Player1, Cards.FromId("CATA_554"), zone: game.Player1.HandZone);
        FlushHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, game.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD &&
            t.Source?.Id == spell.Id && t.Target?.Id == first.Id));
        FlushHistory(match);
        Check(game.Player1.Choice != null, "Targeted decision spell pauses for second target");
        InvokeProcess(match, ChooseTask.Pick(game.Player1, second.Id));
        FlushHistory(match);
        var data = History(capture, cursor);
        var starts = data.Select((entry, index) => (entry, index)).Where(p => p.entry.HasPowerStart &&
            p.entry.PowerStart.Source == spell.Id && (int)p.entry.PowerStart.Type == (int)BlockType.POWER).ToArray();
        Check(starts.Length == 1, "Decision spell keeps one POWER across its answer");
        foreach (var start in starts)
        {
            var meta = data[start.index + 1];
            Check(meta.HasMetaData && (int)meta.MetaData.Type == (int)MetaDataType.TARGET &&
                meta.MetaData.Data == spell.Id && meta.MetaData.Info.SequenceEqual(new[] { first.Id }),
                "Decision POWER carries its original effective target metadata through the answer");
        }
        Check(((Minion)first).Health == 1 && ((Minion)second).Health == 1, "Decision spell rules unchanged");
        // Effect-cast decision spells use TaskQueue.Execute rather than the hand-cast continuation path.
        game.PowerHistory.Last.Clear();
        cursor = capture.Packets.Count;
        SabberStoneCore.Actions.Generic.CastSpell(game.Player1, game, spell, (Minion)first, 0);
        FlushHistory(match);
        data = History(capture, cursor);
        int power = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == spell.Id && (int)e.PowerStart.Type == (int)BlockType.POWER);
        Check(power >= 0 && data[power + 1].HasMetaData &&
            data[power + 1].MetaData.Info.SequenceEqual(new[] { first.Id }), "Immediate spell POWER also carries TARGET metadata");
        capture.SerializeAll();
    }

    private static void AssertSpellMetadata(List<PowerHistoryData> data, int source, int target, int damage, bool shield = false)
    {
        int play = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source && (int)e.PowerStart.Type == (int)BlockType.PLAY);
        int power = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source && (int)e.PowerStart.Type == (int)BlockType.POWER);
        Check(play >= 0 && power > play && data[play].PowerStart.Target == target && data[power].PowerStart.Target == target,
            "Targeted PLAY must contain targeted POWER");
        int end = power + 1, depth = 1;
        for (; end < data.Count; end++)
        {
            if (data[end].HasPowerStart) depth++;
            if (data[end].HasPowerEnd && --depth == 0) break;
        }
        var body = data.Skip(power + 1).Take(end - power - 1).ToList();
        var targets = body.Where(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.TARGET).ToArray();
        Check(targets.Length == 1 && ReferenceEquals(body[0], targets[0]) && targets[0].MetaData.Data == source &&
            targets[0].MetaData.Info.SequenceEqual(new[] { target }), "POWER must open with one TARGET metadata/source");
        var hits = body.Where(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE).ToArray();
        Check(hits.Length == (damage > 0 || shield ? 1 : 0), "Resolved hit must emit exactly one DAMAGE metadata");
        if (damage > 0 || shield)
        {
            Check(hits[0].MetaData.Data == damage && hits[0].MetaData.Info.SequenceEqual(new[] { target }),
                "DAMAGE metadata must describe per-hit amount and victim");
            int hit = body.IndexOf(hits[0]);
            int update = body.FindIndex(e => e.HasTagChange && e.TagChange.Entity == target &&
                (e.TagChange.Tag == (int)GameTag.DAMAGE || e.TagChange.Tag == (int)GameTag.ARMOR || shield && e.TagChange.Tag == (int)GameTag.DIVINE_SHIELD));
            Check(hit < update, "DAMAGE metadata must precede resolved armor/health updates");
        }
        int graveyard = data.FindIndex(e => IsZone(e, source, EngineZone.GRAVEYARD));
        Check(end < graveyard && data.Skip(graveyard + 1).Any(e => e.HasPowerEnd), "Spell leaves PLAY after POWER, inside PLAY block");
        int balance = 0;
        foreach (var entry in data)
        {
            if (entry.HasPowerStart) balance++;
            if (entry.HasPowerEnd) Check(--balance >= 0, "History block underflow");
        }
        Check(balance == 0, "History blocks balanced");
    }
}
