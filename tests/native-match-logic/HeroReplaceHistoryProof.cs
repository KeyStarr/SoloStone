using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

// Majordomo Executus's Deathrattle (ReplaceHeroTask, a Neutral Ragnaros hero) put the new hero's FULL_ENTITY on the wire with CONTROLLER=0 and
// ENTITY_ID=0, and the client could not finish the swap. The native match sends this history, so the hero FULL must carry its owner and id, and
// the history must stay consistent for both seats. (NativeUIProbe.TestControlVisibleDiff itself needs the client libraries: the native-match
// suite, client tier.)
internal static class HeroReplaceHistoryProof
{
    internal static void Run()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 9, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
            game.StartGame();
            var me = seat == 1 ? game.Player1 : game.Player2;
            var majordomo = Entity.FromCard(me, Cards.FromId("BRM_027"), zone: me.BoardZone) as Minion;
            majordomo.Destroy();
            game.DeathProcessingAndAuraUpdate();
            var hero = me.Hero;
            if (hero.Card.Id != "BRM_027h") throw new Exception("Majordomo's Deathrattle did not replace the hero");
            var full = game.PowerHistory.Full.OfType<PowerHistoryFullEntity>().Last(entry => entry.Entity.Id == hero.Id);
            if (!full.Entity.Tags.TryGetValue(GameTag.CONTROLLER, out int controller) || controller != me.PlayerId)
                throw new Exception("the replacement hero's FULL_ENTITY has no CONTROLLER=" + me.PlayerId);
            if (!full.Entity.Tags.TryGetValue(GameTag.ENTITY_ID, out int id) || id != hero.Id)
                throw new Exception("the replacement hero's FULL_ENTITY has no ENTITY_ID=" + hero.Id);
            Console.WriteLine("PASS hero replacement history: Ragnaros FULL_ENTITY carries CONTROLLER and ENTITY_ID, seat " + seat);
        }
    }
}
