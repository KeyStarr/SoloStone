using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class TimeTravelS2Proof
{
    internal static void Run()
    {
        SemiStablePortalProof.Run();
        BygoneDoomspeakerProof.Run();
        InstantMultiverseProof.Run();
        MendTheTimelineProof.Run();
        DruidOfRegrowthProof.Run();
        StadiumAnnouncerProof.Run();
        CeaseToExistProof.Run();
        AeonRendProof.Run();
        WormholeProof.Run();
    }

    internal static IPlayable Resolve(Game game, Controller player, string id)
    {
        var source = Entity.FromCard(player, Cards.FromId(id));
        player.SetasideZone.Add(source);
        source.Card.Power.PowerTask.Process(game, player, source, null, new TaskStack());
        game.ProcessTasks(); game.DeathProcessingAndAuraUpdate();
        return source;
    }

    // Same transient manifest seam as TimeTravelPoolProof; never snapshot an injected catalog.
    internal static void WithCards(Game game, string[] ids, Action action)
    {
        var installed = game.OfflinePools;
        var xml = OfflinePoolFixtures.FixtureCatalog(ids);
        var rows = xml.Root.Element("Members");
        var selected = rows.Elements("Card").Where(r => ids.Contains((string)r.Attribute("cardId"))).ToArray();
        Core2026Proof.Check(selected.Length == ids.Length, "fixture identities already admitted");
        if (ids.Length == 0)
        {
            selected = rows.Elements("Card").Take(1).ToArray();
            selected[0].SetAttributeValue("scope", "Past");
        }
        rows.ReplaceNodes(selected);
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(game, OfflinePoolFixtures.Manifest(xml)); action(); }
        finally { property.SetValue(game, installed); }
    }

    internal static void EmptyPool(Game game, Controller player, string id)
    {
        WithCards(game, Array.Empty<string>(), () => {
            var random = game.Random.CaptureState();
            int hand = player.HandZone.Count, board = player.BoardZone.SlotCount;
            Resolve(game, player, id);
            Core2026Proof.Check(game.Random.CaptureState().Equals(random) && player.HandZone.Count == hand
                && player.BoardZone.SlotCount == board, "empty pool consumes no RNG and creates no output");
        });
    }
}
