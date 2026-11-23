extern alias TransportFixtures;
using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // The client draws the hero Deathrattle skull from hero tag PLAYER_DEATHRATTLE (4398); its value is the entity id of the
    // set-aside "Eternal Life" card shown on hover. Husk, Eternal Reaper used to send only the player attachment (#191).
    [GroupProof("husk-hero-deathrattle", ProofKind.Native)]
    private static void HuskHeroDeathrattleHistory()
    {
        const int skull = 4398;
        foreach (bool enemy in new[] { false, true })
        {
            var (match, capture) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            SogComplete(match, capture);
            var game = match.Game;
            var owner = enemy ? game.Player2 : game.Player1;
            if (game.CurrentPlayer != owner) { game.Process(EndTurnTask.Any(game.CurrentPlayer)); }
            game.PowerHistory.Last.Clear();
            void Flush() { SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear(); }
            Flush();
            capture.Packets.Clear();
            owner.BaseMana = 10; owner.UsedMana = 0;
            var husk = EngineEntity.FromCard(owner, Cards.FromId("TIME_618"), zone: owner.HandZone);
            Check(game.Process(PlayCardTask.Any(owner, husk)), "Husk plays");
            Flush();
            var rows = DrawRows(capture).ToList();
            var grant = owner.AppliedEnchantments.Single(e => e.Card.Id == "TIME_618e");
            var life = game.IdEntityDic[owner.Hero[(GameTag)skull]];
            Check(life.Card.Id == "TIME_618t" && life.Zone == owner.SetasideZone, "Husk creates the set-aside Eternal Life");
            var entities = rows.SelectMany(r => new[] { r.FullEntity, r.ShowEntity, r.ChangeEntity }).Where(e => e != null).ToList();
            Check(entities.Any(e => e.Entity == grant.Id && e.Name == "TIME_618e"
                && e.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == owner.Id)
                && e.Tags.Any(t => t.Name == 4058 && t.Value == 1)),
                "viewer receives the player attachment with CORPSE_SPENDER, so the Corpse counter shows for any class");
            Check(entities.Any(e => e.Entity == life.Id && e.Name == "TIME_618t"), "viewer receives Eternal Life with its card id");
            Check(rows.Any(r => r.HasTagChange && r.TagChange.Entity == owner.Hero.Id
                && r.TagChange.Tag == skull && r.TagChange.Value == life.Id), "viewer receives the hero skull tag");
            Check(owner[GameTag.CORPSES] == 0 || rows.Any(r => r.HasTagChange && r.TagChange.Entity == owner.Id
                && r.TagChange.Tag == (int)GameTag.CORPSES), "corpses reach the viewer");
            // A hero card keeps the skull on the new hero.
            capture.Packets.Clear();
            var oldHero = owner.Hero;
            owner.BaseMana = 10; owner.UsedMana = 0;
            var card = EngineEntity.FromCard(owner, Cards.FromId("ICC_833"), zone: owner.HandZone);
            Check(game.Process(PlayCardTask.Any(owner, card)), "hero card plays");
            Flush();
            rows = DrawRows(capture).ToList();
            Check(owner.Hero != oldHero && owner.Hero[(GameTag)skull] == life.Id && oldHero[(GameTag)skull] == 0
                && (rows.Any(r => r.HasTagChange && r.TagChange.Entity == owner.Hero.Id && r.TagChange.Tag == skull
                    && r.TagChange.Value == life.Id) || rows.SelectMany(r => new[] { r.FullEntity, r.ShowEntity, r.ChangeEntity })
                    .Any(e => e?.Entity == owner.Hero.Id && e.Tags.Any(t => t.Name == skull && t.Value == life.Id))),
                "the skull moves to the new hero on the wire");
            // The revive clears it.
            capture.Packets.Clear();
            SabberStoneCore.Actions.CorpseRules.Gain(owner, 5);
            owner.Hero.Destroy(); game.DeathProcessingAndAuraUpdate();
            Flush();
            Check(owner.Hero.Health == 5 && owner.Hero[(GameTag)skull] == 0
                && DrawRows(capture).Any(r => r.HasTagChange && r.TagChange.Entity == owner.Hero.Id
                    && r.TagChange.Tag == skull && r.TagChange.Value == 0), "the revive clears the skull on the wire");
        }
    }
}
