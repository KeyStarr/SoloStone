using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b17-infinity", ProofKind.PoweredUp)]
    private static void RunB17InfinityGlow()
    {
        Check(SabberStoneCore.Model.Cards.FromId("END_012")?.Implemented == true, "Hand registered");
        var f = new Fixture();
        foreach (string id in new[] { "END_012", "TIME_024" })
        { var card = f.Draw(id); f.Flush(); f.Expected(card, 0, id + " unconditional Battlecry"); }
    }

    [GroupProof("native-b17-infinity", ProofKind.Native)]
    private static void Unpinned_B17InfinityNative()
    {
        Check(SabberStoneCore.Model.Cards.FromId("TIME_024")?.Implemented == true, "Murozond registered");
        foreach (bool opponent in new[] { false, true })
        {
            var f = new Fixture(); var g = f.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "opponent turn");
            var p = g.CurrentPlayer;
            var card = Generic.DrawCard(p, SabberStoneCore.Model.Cards.FromId("TIME_024"));
            f.Flush();
            var hiddenSnapshots = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
                .Where(e => e != null && e.Entity == card.Id).ToArray();
            if (opponent) Check(hiddenSnapshots.Length > 0 && hiddenSnapshots.All(e => string.IsNullOrEmpty(e.Name)), "opponent hand identity private");
            p.BaseMana = 10; p.UsedMana = 0;
            Check(g.Process(PlayCardTask.Any(p, card)), "Murozond play"); f.Flush();
            Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "opponent start"); f.Flush();
            Check(((Minion)card).AttackDamage == 8, "no early infinity");
            Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "owner start"); f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(card)[(int)GameTag.ATK] == int.MaxValue, "normal numeric infinity ATK");
            Check(f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == card.Id
                && e.TagChange.Tag == (int)GameTag.ATK && e.TagChange.Value == int.MaxValue), "numeric ATK packet");
            var actors = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity }).Where(e => e != null).ToArray();
            Check(actors.Any(e => e.Name == "TIME_024e") && actors.Any(e => e.Name == "TIME_024e2"), "exported pending/resolved actors");
            var weapon = Generic.DrawCard(p, SabberStoneCore.Model.Cards.FromId("END_012"));
            p.UsedMana = 0;
            Check(g.Process(PlayCardTask.Any(p, weapon)), "Hand play"); f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(weapon)[(int)GameTag.ATK] == int.MaxValue, "weapon numeric infinity");
            Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "weapon expiry"); f.Flush();
            Check(((Weapon)weapon).AttackDamage == 4, "native weapon expiry");
            Check(f.Entries.All(e => !e.HasTagChange || e.TagChange.Tag < 30000), "no internal wire tags");
        }
        Console.WriteLine("PASS Unpinned infinity native numeric tags, actors, privacy both seats");
    }
}
