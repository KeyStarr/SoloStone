using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-play-locks", ProofKind.PoweredUp)]
    private static void RunB15PlayLocksGlow()
    {
        foreach (string id in new[] { "CATA_215", "EDR_234", "EDR_526", "JAIL_987", "MEND_044" })
        {
            Check(Cards.FromId(id).Implemented, id + " registered");
            var fixture = new Fixture();
            var card = fixture.Draw(id);
            fixture.Flush();
            fixture.Expected(card, 0, id + " has no conditional bonus");
        }
    }

    [GroupProof("b15-play-locks-native", ProofKind.Native)]
    private static void RunB15PlayLocksNative()
    {
        var fixture = new Fixture();
        var p = fixture.Player;
        var own = fixture.Draw("CS2_182");
        PlayLockRules.LockHand(own, p.Hero, "EDR_234e2", 2);
        fixture.Flush();
        Check(fixture.Match.SnapshotVisibleTransportTags(own)[(int)GameTag.CANT_PLAY] == 1,
            "visible hand lock publishes ordinary CANT_PLAY");
        Check(fixture.Entries.Any(e => e.HasShowEntity && e.ShowEntity.Name == "EDR_234e2"
            && e.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == own.Id)),
            "visible lock has exact exported attachment");

        var sleeper = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CS2_182"));
        Generic.SummonBlock(fixture.Game, sleeper, -1, p.Hero);
        var clearing = (BoardObject)fixture.Draw("MEND_044");
        fixture.Process(PlayCardTask.Any(p, clearing));
        Check(fixture.Game.Process(new ActivateLocationTask(p, clearing, sleeper)), "native sleep activation");
        fixture.Flush();
        var tags = fixture.Match.SnapshotVisibleTransportTags(sleeper);
        Check(tags[(int)GameTag.CANT_ATTACK] == 1 && tags[(int)GameTag.TAUNT] == 1
            && tags[(int)GameTag.HEALTH] == sleeper.Card.Health + 2 && sleeper[GameTag.DORMANT] == 0,
            "Unpinned_Tranquil publishes attack restriction, separate Health/Taunt, no Dormant");
        sleeper.Silence(); fixture.Flush();
        Check(fixture.Match.SnapshotVisibleTransportTags(sleeper)[(int)GameTag.CANT_ATTACK] == 0,
            "Unpinned_Tranquil Silence publishes wake immediately");
        fixture.Mana(10);
        var bounceLocation = (BoardObject)fixture.Draw("MEND_044");
        fixture.Process(PlayCardTask.Any(p, bounceLocation));
        Check(fixture.Game.Process(new ActivateLocationTask(p, bounceLocation, sleeper)), "native bounce fixture sleep");
        var strength = sleeper.AppliedEnchantments.Single(e => e.Card.Id == "MEND_044e4");
        Check(Generic.ReturnToHandBlock(p, sleeper), "native bounce fixture");
        fixture.Flush();
        Check(!sleeper.AppliedEnchantments.Contains(strength)
            && fixture.Entries.Any(e => e.HasHideEntity && e.HideEntity.Entity == strength.Id),
            "bounce retires strength actor rather than retaining a stale hand buff");

        foreach (string id in new[] { "EDR_234", "JAIL_987" })
        {
            var hidden = new Fixture();
            var enemy = hidden.Game.Player2;
            Check(hidden.Game.Process(EndTurnTask.Any(hidden.Player)), "hidden effect opponent turn");
            enemy.BaseMana = 10;
            var source = Generic.DrawCard(enemy, Cards.FromId(id));
            Check(hidden.Game.Process(PlayCardTask.Any(enemy, source)), id + " opponent play");
            if (source is BoardObject location)
                Check(hidden.Game.Process(new ActivateLocationTask(enemy, location)), "opponent Wing activation");
            var locked = enemy.HandZone.Where(c => c[GameTag.CANT_PLAY] == 1).ToArray();
            Check(locked.Length == (id == "EDR_234" ? 2 : 1), id + " hidden outputs fixture");
            var ids = locked.Select(c => c.Id).Concat(locked.SelectMany(c => c.AppliedEnchantments.Select(e => e.Id))).ToArray();
            hidden.Flush();
            foreach (var entry in hidden.Entries)
            {
                var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
                if (entity != null && ids.Contains(entity.Entity))
                    Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t =>
                        t.Name == (int)GameTag.ENTITY_ID || t.Name == (int)GameTag.CONTROLLER || t.Name == (int)GameTag.ZONE || t.Name == (int)GameTag.ZONE_POSITION),
                        id + " opponent receives structural card-back data only, including helpers");
                if (entry.HasTagChange && ids.Contains(entry.TagChange.Entity))
                    Check(entry.TagChange.Tag == (int)GameTag.ZONE || entry.TagChange.Tag == (int)GameTag.ZONE_POSITION
                        || entry.TagChange.Tag == (int)GameTag.CONTROLLER || entry.TagChange.Tag == (int)GameTag.ENTITY_ID,
                        id + " opponent receives no lock/card identity tags");
            }
            AssertNoPlayLockPrivateTags(hidden);
        }
        AssertNoPlayLockPrivateTags(fixture);
        Check(fixture.Game.GetPendingDecisions().All(d => d.Kind == DecisionKind.Options)
            && fixture.Player.Options().Any(t => t is EndTurnTask), "locks leave ordinary action decisions answerable");
    }

    private static void AssertNoPlayLockPrivateTags(Fixture fixture)
    {
        foreach (var entry in fixture.Entries)
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            Check(entity == null || entity.Tags.All(t => !PlayLockRules.IsPrivateTag((GameTag)t.Name)), "no internal play-lock entity tag on wire");
            Check(!entry.HasTagChange || !PlayLockRules.IsPrivateTag((GameTag)entry.TagChange.Tag), "no internal play-lock TAG_CHANGE on wire");
        }
    }
}
