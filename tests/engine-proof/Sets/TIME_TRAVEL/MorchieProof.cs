using System.Reflection;
using SabberStoneCore.Model.Snapshots;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class MorchieProof
{
    internal static void Run() => StandardSetsProof.Case("END_036", (g, p, id) => {
        DefinitionCompatibility();
        var quiet = new Game(new GameConfig { RandomSeed = g.Seed, SkipMulligan = true, History = false, Logging = false });
        quiet.StartGame();
        var quietSource = Summon(quiet.Player1, id);
        Core2026Proof.Check(Morchie.KeepsBoth(quiet.Player1), "controller marker is rules state without history");
        Kill(quiet, quietSource);
        Core2026Proof.Check(!Morchie.KeepsBoth(quiet.Player1), "controller marker removed without history");
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, RewindSliceProof.Rewind, 121202);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(Morchie.KeepsBoth(p) && !Morchie.KeepsBoth(p.Opponent) &&
            p.AppliedEnchantments.Single(e => e.Card.Id == "END_036e").Target == p, "controller carries Morchie enchantment");
        if (candidates.Count == 0) Core2026Proof.Check(p.Choice == null, "empty admitted Rewind pool does not suspend");
        else RewindSliceProof.Answer(g);
        RewindSliceProof.CheckCopies(g, next => {
            Core2026Proof.Check(Morchie.KeepsBoth(next.Player1), "active aura survives clone/load");
            ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!Morchie.KeepsBoth(next.Player1), "silence removes controller enchantment");
        });
        StandardSetsProof.ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        var first = Summon(p, id); var second = Summon(p, id);
        Kill(g, first); Core2026Proof.Check(Morchie.KeepsBoth(p), "second Morchie keeps its own aura");
        RewindSliceProof.CheckCopies(g, next => {
            Kill(next, (Minion)next.IdEntityDic[second.Id]);
            Core2026Proof.Check(!Morchie.KeepsBoth(next.Player1), "last Morchie death removes aura");
        });
        var bounced = Summon(p, id);
        SabberStoneCore.Actions.Generic.RemoveFromZone(p, bounced);
        SabberStoneCore.Actions.Generic.AddHandPhase(p, bounced); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!Morchie.KeepsBoth(p), "leaving board removes aura");
        var stolen = Summon(p, id);
        new ControlTask(EntityType.TARGET)
            .Process(g, p.Opponent, p.Opponent.Hero, stolen);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!Morchie.KeepsBoth(p) && Morchie.KeepsBoth(p.Opponent), "aura follows current controller");
        Kill(g, stolen);
        Core2026Proof.Check(!Morchie.KeepsBoth(p.Opponent), "stolen source death cleans new controller");
        PoolPilotProof.EmptyHand(p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        RewindSliceProof.WithRewindPool(g, () => source = (Minion)Play(g, p, id));
        Core2026Proof.Check(p.Choice != null && g.Resolution.HasSuspendedEffect && p.BoardZone.IsFull, "Battlecry waits on full-board Discover");
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.All(c => c.Card[(GameTag)3934] == 1 && c.Card.Id != "END_036" && c.Card.Id != "TIME_035"),
            "three actual Rewind offers, referenced tags excluded");
        int chosen = offers[0].Id;
        int discoveries = g.HistoryFacts.Query.Discoveries().Count();
        var rng = g.Random.CaptureState();
        PoolPilotProof.Resume(g, next => {
            Core2026Proof.Check(RewindSliceProof.Answer(next, resume: false) == chosen &&
                next.Player1.HandZone.Single().Id == chosen && next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect &&
                next.HistoryFacts.Query.Discoveries().Count() == discoveries + 1 &&
                next.HistoryFacts.Query.Discoveries().Last().SelectedEntityIds.SequenceEqual(new[] { chosen }),
                "pending Morchie original/clone/load grants selected entity exactly once");
            Core2026Proof.Check(Morchie.KeepsBoth(next.Player1) && !Morchie.KeepsBoth(next.Player2) &&
                next.Player1.AppliedEnchantments.Count(e => e.Card.Id == "END_036e") == 1 && rng.Equals(next.Random.CaptureState()),
                "pending Morchie resume retains single controller aura and RNG");
        });
        StandardSetsProof.ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        RewindSliceProof.WithRewindPool(g, () => Play(g, p, id));
        PoolPilotProof.FillHand(p);
        rng = g.Random.CaptureState(); chosen = p.Choice.Choices[0];
        PoolPilotProof.Resume(g, next => {
            Core2026Proof.Check(RewindSliceProof.Answer(next, resume: false) == chosen && next.Player1.HandZone.Count == 10 &&
                next.IdEntityDic[chosen].Zone.Type == Zone.GRAVEYARD && Morchie.KeepsBoth(next.Player1) && rng.Equals(next.Random.CaptureState()),
                "full hand burns chosen card once after original/clone/load resume; aura and RNG unchanged");
        });
    });

    private static void DefinitionCompatibility()
    {
        // Captured from pre-slice 3d5c2d4 in a separate build; never renew with this slice's engine.
        const string fixtures = "tests/engine-proof/Sets/TIME_TRAVEL/Fixtures/";
        var types = (IEnumerable<System.Type>)typeof(SnapshotSchema).GetMethod("RegisteredDefinitionTypes", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        // Other branches may legitimately append schema types; renumbering is caught by the composed-definition bytes below.
        var current = new HashSet<string>(types.Select(t => t.FullName), StringComparer.Ordinal);
        Core2026Proof.Check(File.ReadAllLines(fixtures + "pre-s1-definition-types.txt").All(current.Contains),
            "every pre-slice snapshot definition type stays registered");
        var composed = Card.CreateZombeastCard(Cards.FromId("CS2_182"), Cards.FromId("CS2_120"), true);
        Core2026Proof.Check(new HistoryDefinition(composed).Data == File.ReadAllText(fixtures + "pre-s1-composed-definition.b64").Trim(),
            "composed HistoryDefinition.Data remains byte-identical to pre-slice engine");
    }
}
