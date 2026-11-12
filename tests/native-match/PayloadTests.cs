using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void StoredPayloadProjection()
    {
        BashanaSpellNamesReachFirstReveal();
        var capture = new Capture(); var match = StartAtHumanTurn(capture); var g = match.Game; var p = g.Player1;
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var victim = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var toru = Generic.DrawCard(p, Cards.FromId("TLC_841")); p.BaseMana = 10; p.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(p, toru)), "Toru play");
        FlushHistory(match);
        var jar = p.HandZone.Single();
        Check(SentTextTag(capture, jar.Id, 2921) == Cards.FromId("CS2_182").AssetId
            && SentTextTag(capture, jar.Id, 955) == 1, "Jar reference sixth script slot and alternate text project");
        var actor = jar.AppliedEnchantments.Single(e => e.Card.Id == "TLC_841e");
        var stored = PayloadRules.Entries(actor, 1).Single();
        // Jarring's text names the jarred minion through an entity reference (builder 19): its owner gets the entry named.
        Check(NativeHistory(capture).Any(h => h.HasFullEntity && h.FullEntity.Entity == stored.Id && h.FullEntity.Name != ""), "stored card identity is named to the Jar's owner");
        p.UsedMana = 0; Check(g.Process(PlayCardTask.Any(p, jar)), "Jar play");
        var boardCopy = Generic.Copy(p, p.Hero, jar, SabberStoneCore.Enums.Zone.PLAY); g.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(SentTextTag(capture, boardCopy.Id, 2921) == Cards.FromId("CS2_182").AssetId
            && SentTextTag(capture, boardCopy.Id, 955) == 1, "board copy projects independent payload display before entry");
        var faceless = Generic.DrawCard(p, Cards.FromId("EX1_564")); p.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(p, faceless, jar)), "Faceless copies payload-bearing Jar"); FlushHistory(match);
        Check(SentTextTag(capture, faceless.Id, 2921) == Cards.FromId("CS2_182").AssetId
            && SentTextTag(capture, faceless.Id, 955) == 1, "transformed Jar projects pinned sixth script slot and alternate text");
        var copiedActor = faceless.AppliedEnchantments.Single(e => e.Card.Id == "TLC_841e");
        var copiedStored = PayloadRules.Entries(copiedActor, 1).SingleOrDefault();
        Check(copiedStored != null && copiedStored.Id != stored.Id, "transformed Jar owns independent offboard contents");
        Check(NativeHistory(capture).Any(h => h.HasFullEntity && h.FullEntity.Entity == copiedStored.Id && h.FullEntity.Name != ""), "transform-copied stored identity is named to its owner");
        ((Minion)jar).Silence(); g.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(SentTextTag(capture, jar.Id, 2921) == 0 && SentTextTag(capture, jar.Id, 955) == 0, "Silence clears payload display on board");
        Check(SentTextTag(capture, faceless.Id, 2921) == Cards.FromId("CS2_182").AssetId
            && SentTextTag(capture, faceless.Id, 955) == 1, "original Silence preserves transformed Jar display");
        ((Minion)faceless).Silence(); g.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(SentTextTag(capture, faceless.Id, 2921) == 0 && SentTextTag(capture, faceless.Id, 955) == 0,
            "transformed Jar Silence clears sixth script slot and alternate text");
        Check(NativeHistory(capture).All(h => !h.HasTagChange || !PayloadRules.IsInternalTag((GameTag)h.TagChange.Tag)), "private payload tags excluded");
        var hiddenTreant = SabberStoneCore.Model.Entities.Entity.FromCard(g.Player2, Cards.FromId("MEND_046t"), zone: g.Player2.HandZone);
        var hiddenSpell = PayloadRules.StoreCard(hiddenTreant, 1, Cards.FromId("CS2_005"));
        hiddenTreant[GameTag.TAG_SCRIPT_DATA_NUM_1] = hiddenSpell.Card.AssetId;
        FlushHistory(match);
        Check(!NativeHistory(capture).Any(h => h.HasFullEntity && (h.FullEntity.Entity == hiddenSpell.Id || h.FullEntity.Entity == hiddenTreant.Id) && h.FullEntity.Name != ""
            || h.HasShowEntity && (h.ShowEntity.Entity == hiddenSpell.Id || h.ShowEntity.Entity == hiddenTreant.Id) && h.ShowEntity.Name != ""), "carved spell identities stay hidden in opposing hand");
        Check(!ReplayNativeEntityTags(NativeHistory(capture), hiddenTreant.Id).ContainsKey(2), "opponent Treant leaks no spell dbfId script value");
        var devoured = SabberStoneCore.Actions.Generic.DrawCard(g.Player2, Cards.FromId("CS2_029"));
        FlushHistory(match);
        var isorath = SabberStoneCore.Actions.Generic.DrawCard(p, Cards.FromId("CATA_481")); p.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(p, isorath)), "Iso'rath devours both opposing hand cards"); FlushHistory(match);
        Check(!NativeHistory(capture).Any(h => h.HasShowEntity && (h.ShowEntity.Entity == devoured.Id || h.ShowEntity.Entity == hiddenTreant.Id)
            && h.ShowEntity.Name != ""), "retired devoured originals do not become public offboard");
        capture.SerializeAll();
        Console.WriteLine("PASS stored payload native projection: sixth script slot, alternate text, Silence, offboard privacy");
    }

    private static void BashanaSpellNamesReachFirstReveal()
    {
        OpponentPrivacyCheckRejectsTransientLeak();
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.DRUID),
            Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game; var player = game.Player1;
        foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        FlushHistory(match);
        int cursor = capture.Packets.Count;
        player.BaseMana = 10; player.UsedMana = 0;
        var bashana = Generic.DrawCard(player, Cards.FromId("MEND_046"));
        Check(game.Process(PlayCardTask.Any(player, bashana)), "Bashana play");
        FlushHistory(match);

        var treants = player.HandZone.Where(card => card.Card.Id == "MEND_046t").ToArray();
        Check(treants.Length == 3, "Bashana generates three Treants with hand space");
        var rows = NativeHistory(capture, cursor);
        int carvedCost = 0;
        foreach (var treant in treants)
        {
            var stored = PayloadRules.Entries(treant, 1).Single();
            carvedCost += stored.Card.Cost;
            Check(stored.Card[GameTag.SPELL_SCHOOL] == 4, "Bashana stores one Nature spell per Treant");
            // The pinned client resolves the Treant's tag 2 as an ENTITY id (CardTextBuilderType 23): the owner must
            // receive a named entity for the carved spell before the Treant is first shown in hand.
            int enter = rows.FindIndex(row => row.HasTagChange && row.TagChange.Entity == treant.Id
                && row.TagChange.Tag == (int)GameTag.ZONE && row.TagChange.Value == (int)SabberStoneCore.Enums.Zone.HAND);
            Check(enter > 0, "Treant enters the native hand");
            Check(ReplayNativeEntityTags(rows.Take(enter), treant.Id)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] == stored.Id,
                "Treant references its carved spell entity when it is first shown in hand");
            Check(rows.Take(enter).Any(row => row.HasFullEntity && row.FullEntity.Entity == stored.Id && row.FullEntity.Name == stored.Card.Id),
                "carved spell entity is named to its owner before the Treant is shown");
            Check(ReplayNativeEntityTags(rows, treant.Id)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] == stored.Id,
                "Treant final native tag references its carved spell entity, not a dbfId");
        }
        Check(carvedCost == 12, "Bashana carved Nature spells total 12 Mana");

        foreach (var card in game.Player2.HandZone.ToArray()) game.Player2.SetasideZone.Add(game.Player2.HandZone.Remove(card));
        game.Player2.Hero.Card = Cards.HeroCard(CardClass.DRUID);
        Check(game.Process(EndTurnTask.Any(player)), "pass to opposing Bashana turn");
        game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
        var opposingBashana = Generic.DrawCard(game.Player2, Cards.FromId("MEND_046"));
        FlushHistory(match);
        cursor = capture.Packets.Count;
        Check(game.Process(PlayCardTask.Any(game.Player2, opposingBashana)), "opposing Bashana play");
        FlushHistory(match);
        var opposingTreants = game.Player2.HandZone.Where(card => card.Card.Id == "MEND_046t").ToArray();
        Check(opposingTreants.Length == 3, "opposing Bashana generates three Treants");
        var opposingRows = NativeHistory(capture, cursor);
        foreach (var treant in opposingTreants)
        {
            var stored = PayloadRules.Entries(treant, 1).Single();
            Check(OpposingPayloadHistoryIsPrivate(opposingRows, treant.Id, stored.Id),
                "opposing Bashana and carved spell history remain private");
            Check(!ReplayNativeEntityTags(opposingRows, treant.Id).ContainsKey((int)GameTag.TAG_SCRIPT_DATA_NUM_1),
                "opposing Bashana Treant redacts its carved spell dbfId");
        }
        capture.SerializeAll();
    }

    private static void OpponentPrivacyCheckRejectsTransientLeak()
    {
        const int treantId = 900001, storedId = 900002;
        var history = new List<PowerHistoryData>
        {
            new PowerHistoryData { FullEntity = new PowerHistoryEntity {
                Entity = treantId, Name = "", Tags = new List<PegasusGame.Tag> { new PegasusGame.Tag {
                    Name = (int)GameTag.TAG_SCRIPT_DATA_NUM_1, Value = 121107 } } } },
            new PowerHistoryData { ShowEntity = new PowerHistoryEntity {
                Entity = treantId, Name = "", Tags = new List<PegasusGame.Tag>() } }
        };
        Check(!ReplayNativeEntityTags(history, treantId).ContainsKey((int)GameTag.TAG_SCRIPT_DATA_NUM_1),
            "transient leak fixture ends with tag 2 correctly cleared");
        Check(!OpposingPayloadHistoryIsPrivate(history, treantId, storedId),
            "opponent privacy check rejects a transient first-snapshot dbfId leak");
    }

    private static bool OpposingPayloadHistoryIsPrivate(IEnumerable<PowerHistoryData> rows, int treantId, int storedId)
    {
        bool IsPrivateEntity(int id) => id == treantId || id == storedId;
        bool Leaks(PowerHistoryEntity snapshot) => IsPrivateEntity(snapshot.Entity)
            && (!string.IsNullOrEmpty(snapshot.Name)
                || snapshot.Tags.Any(tag => tag.Name == (int)GameTag.TAG_SCRIPT_DATA_NUM_1));
        foreach (var row in rows)
        {
            if (row.HasFullEntity && Leaks(row.FullEntity)
                || row.HasShowEntity && Leaks(row.ShowEntity)
                || row.HasChangeEntity && Leaks(row.ChangeEntity))
                return false;
            if (row.HasTagChange && IsPrivateEntity(row.TagChange.Entity)
                && row.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1)
                return false;
        }
        return true;
    }
}
