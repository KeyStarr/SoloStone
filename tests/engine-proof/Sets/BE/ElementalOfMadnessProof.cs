using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class ElementalOfMadnessProof
{
    private static void Answer(Game g)
    {
        var pending = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { pending.Options[0].OptionId }), out var error), "Madness answer " + error);
    }
    private static void Replay(Game g)
    {
        var p = g.Player1;
        Play(g, p, "BE_870"); Answer(g); EmptyHand(p);
        var source = Add(p, "GIL_820"); Effects.Attack_N(-3).ApplyTo(source);
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && p.HandZone.Count == 1,
            "Shudderwock replay completes Discover automatically");
        var chosen = p.HandZone.Single();
        Core2026Proof.Check(chosen.Card.Type == CardType.SPELL && chosen.Card.Cost == 3
            && chosen[GameTag.DISPLAYED_CREATOR] == source.Id, "replayed Madness uses runtime Attack and creator");
        Visible(chosen, 0);
        End(g); Visible(chosen, 3);
    }
    internal static void Run() => StandardSetsProof.Case("BE_870", (g, p, id) => {
        Replay(g.CloneExact(history: true));
        Definition("BE_870e");
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var counts = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }); counts.ValidateRegistered();
        Core2026Proof.Check(counts.Cards.Single().PoolRequirements.Single(r => r.Binding == "ordinary").Count >= 3, "ordinary admission after source exclusion");
        var source = Add(p, id); PlayEntity(g, p, source);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3 && p.Choice.Choices.All(c => g.IdEntityDic[c].Card.Cost == 2 && g.IdEntityDic[c].Card.Type == CardType.SPELL), "Attack 2 snapshots any-class Cost 2 spell offers");
        var offered = (Playable)g.IdEntityDic[p.Choice.Choices.First()]; Effects.ReduceCost(1).ApplyTo(offered);
        var quiet = g.CloneExact(history: false); Answer(quiet);
        var quietSpell = quiet.Player1.HandZone.Single(); Visible(quietSpell, 0); End(quiet); Visible(quietSpell, 1);
        Resume(g, Answer);
        var chosen = (Playable)p.HandZone.Single(); Visible(chosen, 0);
        Core2026Proof.Check(chosen.AppliedEnchantments.Single().Card.Id == "BE_870e", "Madness temporary actor bound");
        var copy = Generic.Copy(p.Opponent, source, chosen, Zone.HAND); Visible(copy, 0);
        Effects.SetCost(4).ApplyTo(chosen);
        Visible(chosen, 0); // A9: temporary overlay remains above later permanent SET until expiry.
        Resume(g, End); Visible(chosen, 4); Visible(copy, 1);
        Core2026Proof.Check(chosen.AppliedEnchantments.Count == 0 && copy.AppliedEnchantments.Count == 0, "expiry retires both actors once; older reduction survives copy");
        End(g); EmptyHand(p); EmptyHand(p.Opponent);
        source = Add(p, id); Effects.Attack_N(1).ApplyTo(source); PlayEntity(g, p, source);
        Core2026Proof.Check(p.Choice.Choices.All(c => g.IdEntityDic[c].Card.Cost == 3), "buffed current Attack binds 3");
        ((Minion)source).AttackDamage = 9;
        Core2026Proof.Check(p.Choice.Choices.All(c => g.IdEntityDic[c].Card.Cost == 3), "offers retain resolved Attack binding");
        FillHand(p); Answer(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Any(c => c.Card.Type == CardType.SPELL), "full-hand chosen spell burns");
        EmptyHand(p);
        source = Add(p, id); Effects.Attack_N(100).ApplyTo(source); var random = g.Random.CaptureState();
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && random.Equals(g.Random.CaptureState()), "empty Attack binding creates no choice/card and consumes no RNG");
        End(g);
    });
}
