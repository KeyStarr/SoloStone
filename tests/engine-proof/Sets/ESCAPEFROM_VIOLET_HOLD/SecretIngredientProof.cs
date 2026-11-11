using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class SecretIngredientProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_201", (g, p, id) => {
        // Tier D: pinned 37.0 export gives exact JAIL_201a/b branches and JAIL_201ae1 enchantment.
        // Native Choose One is an atomic pre-play decision (DecisionProof.AtomicOptions), not a Battlecry prompt.
        var source = Add(p, id);
        Core2026Proof.Check(source.ChooseOne && source.ChooseOnePlayables.Select(c => c.Card.Id).SequenceEqual(new[] { "JAIL_201a", "JAIL_201b" })
            && source.ChooseOnePlayables.Select(c => c.Card.AssetId).SequenceEqual(new[] { 130019, 130020 })
            && source.ChooseOnePlayables.All(c => c.Card.Implemented && c.Controller == p && c.Zone == p.SetasideZone && c.Cost == 0), "exact registered fixed options");
        var tasks = p.Options().Where(t => t.Source?.Id == source.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(tasks.Select(t => t.ChooseOne).OrderBy(n => n).SequenceEqual(new[] { 1, 2 }), "bots see both branches");
        long cursor = g.Random.DrawCount;
        Resume(g, next => Answer(next, source.Id, 1));
        Core2026Proof.Check(p.Hero.AttackDamage == 2 && p.HandZone.Count == 0 && p.Opponent.Hero.AttackDamage == 0
            && g.Random.DrawCount == cursor, "attack branch only affects own hero and consumes no RNG");
        Core2026Proof.Check(p.Hero.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_201ae1" && e.Card.AssetId == 132947), "exact attack enchantment");
        Resume(g, next => End(next));
        Core2026Proof.Check(p.Hero.AttackDamage == 0, "hero Attack expires at turn end");
        End(g); EmptyHand(p);
        source = Add(p, id);
        var expected = OfflineCardPools.Pick(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, source.Card.AssetId);
        Resume(g, next => Answer(next, source.Id, 2));
        var result = HandResults(g, source, 1).Single();
        Core2026Proof.Check(result.Card.Id == expected.Id && p.Hero.AttackDamage == 0, "seeded exact-Druid random branch without attack buff");
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            EmptyHand(p); source = Add(p, id); long before = g.Random.DrawCount;
            Answer(g, source.Id, 2); HandResults(g, source, count);
            Core2026Proof.Check(g.Random.DrawCount == before + count, "empty/singleton Druid pool RNG");
        });
        EmptyHand(p); source = Add(p, id); FillHand(p);
        Answer(g, source.Id, 2); HandResults(g, source, 1);
        Core2026Proof.Check(p.HandZone.Count == 10, "random branch uses freed hand slot");
        EmptyHand(p); FillHand(p);
        int graveyard = p.GraveyardZone.Count; cursor = g.Random.DrawCount;
        Cards.FromId("JAIL_201b").Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Count == graveyard + 1 && g.Random.DrawCount == cursor + 1,
            "full hand burns a directly generated card");
        EmptyHand(p); LocationProofs.Location(p);
        var fandral = Summon(p, "CORE_OG_044");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        source = Add(p, id);
        Core2026Proof.Check(p.ChooseBoth && p.Options().Any(t => t.Source?.Id == source.Id && t.ChooseOne == -1), "Choose Both exposes one bot option");
        Resume(g, next => Answer(next, source.Id, -1));
        HandResults(g, source, 1);
        Core2026Proof.Check(p.Hero.AttackDamage == 2 && p.BoardZone.SlotCount == 7, "Choose Both performs both branches on full physical board");
        fandral.Silence(); End(g);
        Core2026Proof.Check(p.Hero.AttackDamage == 0, "combined branch attack expires");
    });

    private static void Answer(Game g, int entityId, int branch)
    {
        var view = g.GetPendingDecisions().Single();
        var option = view.Options.Single(o => o.EntityId == entityId && o.TaskType == PlayerTaskType.PLAY_CARD && o.BranchIndex == branch);
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { option.OptionId }), out var error), error);
        Core2026Proof.Check(g.Player1.Choice == null && !g.Resolution.HasSuspendedEffect, "atomic Choose One completes");
    }
}
