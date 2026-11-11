using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AgentCoinChoiceProof
{
    [GroupProof("agent-coin-choice")]
    internal static void Run()
    {
        foreach (string coin in new[] { "JAIL_504t", "JAIL_504t2", "JAIL_504t3" })
        foreach (string target in new[] { "CS2_182", "CS2_029", "CS2_106" })
        {
            var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
            Play(g, p, "JAIL_504");
            PlayerDecisionProof.Answer(g, p.Choice.Choices.Single(id => g.IdEntityDic[id].Card.Id == coin));
            var held = Add(p, target);
            Play(g, p, "CATA_200");
            var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            foreach (var game in new[] { g, clone, loaded })
            {
                var rng = game.Random.CaptureState();
                PlayerDecisionProof.Answer(game, held.Id);
                Core2026Proof.Check(game.IdEntityDic[held.Id].Card.Id == coin && game.Player1.Choice == null
                    && !game.Resolution.HasSuspendedEffect && game.Random.CaptureState().Equals(rng),
                    "Aya/Agent transforms once without executing counterfeit Power or consuming RNG");
            }
            Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(loaded), "Aya/Agent continuation survives snapshot");
        }
        // A transform installs a definition; it never casts spells or runs the new Battlecry/Discover.
        foreach (string definition in new[] { "CATA_200", "JAIL_504", "TLC_515", "CORE_WON_096", "JAIL_504t3" })
        {
            var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
            var held = Add(p, "CS2_182");
            Generic.ChangeEntityBlock(p, held, Cards.FromId(definition), true);
            Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "hand transform does not execute new Power: " + definition);
        }
        var mapped = DiscoverRealProof.New(); var owner = mapped.Player1;
        var deck = DiscoverRealProof.Deck(owner, "CATA_200", "TLC_515", "CATA_203");
        var heldTarget = Add(owner, "CS2_182");
        var map = Play(mapped, owner, "TLC_515");
        PlayerDecisionProof.Answer(mapped, owner.Choice.Choices.Single(id => mapped.IdEntityDic[id][DiscoverRealRules.BoundEntity] == deck[0].Id));
        Core2026Proof.Check(mapped.Process(PlayCardTask.Any(owner, deck[0])), "play Map's Agent");
        PlayerDecisionProof.Answer(mapped, heldTarget.Id);
        Core2026Proof.Check(mapped.IdEntityDic[owner.Choice.SourceId][GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME,
            "legitimate bonus recipe retains removed watcher as engine source");
        PegasusGame.EntityChoices native = null;
        var projection = new NativeMatch((id, packet) => { if (id == 17) native = (PegasusGame.EntityChoices)packet; });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(projection, mapped);
        typeof(NativeMatch).GetMethod("SendHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(projection, new object[] { mapped.PowerHistory.Full });
        typeof(NativeMatch).GetMethod("SendHumanChoice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(projection, null);
        Core2026Proof.Check(native.Source == map.Id && native.Entities.SequenceEqual(owner.Choice.Choices),
            "production projection retains engine offers but replaces removed source with printed Map");
        Console.WriteLine("PASS Aya/Agent engine: 9 counterfeit/type cases, clone/load, transform Power boundaries");
    }
}
