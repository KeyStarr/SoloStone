using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class FadingMemoryProof
{
    internal static void Run()
    {
        CopiedDeathrattle();
        StandardSetsProof.Case("TIME_040", (g, p, id) => {
            HandBoundaries(g, p, id, 1, death: true);
            var source = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == 0, "no generation before death");
            Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
            Generated(g, source, p.HandZone, 1);
            EmptyHand(p); source = Summon(p, id); source.Silence(); Kill(g, source);
            Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes deathrattle");
        });
    }

    private static void CopiedDeathrattle()
    {
        var game = OfflinePoolTestRecipes.NewGame();
        var player = game.Player1;
        EmptyHand(player);
        Kill(game, Summon(player, "TIME_040"));
        EmptyHand(player);
        var undatakah = (Minion)Play(game, player, "TRL_537");
        Core2026Proof.Check(undatakah.AppliedEnchantments.Count(e => e.Card.Id == "TRL_537e" &&
            e.CapturedCard?.Id == "TIME_040") == 1, "Undatakah captured Fading Memory");
        var random = game.Random.Clone();
        var past = OfflineCardPools.Candidates(game, player.HeroClass, OfflinePoolDeclarations.PastFiveCostMinion(), undatakah.Card.AssetId)
            .Select(card=>card.Id).ToArray();
        string expected = past[random.Next(past.Length)];
        long draws = game.Random.DrawCount;
        Resume(game, next => Kill(next, (Minion)next.IdEntityDic[undatakah.Id]));
        var generated = player.HandZone.Single();
        Core2026Proof.Check(generated.Card.Id == expected && generated[GameTag.DISPLAYED_CREATOR] == undatakah.Id &&
            game.Random.DrawCount == draws + 1 && game.Random.CaptureState().Equals(random.CaptureState()),
            "copied deathrattle generates from past with runtime creator and exactly one draw");
        // Restore after generation too, then exercise future recipe draws with the copied runtime source.
        Resume(game, next => {
            var source = next.IdEntityDic[undatakah.Id];
            Cards.FromId("TIME_040").Power.DeathrattleTask.Process(next, next.Player1, source, null, new TaskStack());
        });
        Core2026Proof.Check(player.HandZone.Last().Card.Id == past[random.Next(past.Length)] &&
            game.Random.CaptureState().Equals(random.CaptureState()), "copied recipe future RNG after clone/snapshot");
    }
}
