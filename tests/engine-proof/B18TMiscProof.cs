using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class B18TMiscProof
{
    [GroupProof("b18-t-misc")]
    internal static void Run()
    {
        Core2026Proof.Check(typeof(InfinityRules).GetMethod("MarkCost") != null, "chronological removable Infinity facility exists");
        Unpinned_InfinityDynamicBase();
        Unpinned_InfinityLayers();
        Core2026Proof.Check(typeof(BasicHeroPowerRules).GetMethod("InstallPassive") != null, "passive composition lifecycle exists");
        Unpinned_InfinityLifecycle();
        Unpinned_InfinityAuras();
        PinnedRule_PassiveLifecycle();
        Unpinned_RewindState();
    }

    private static void Unpinned_InfinityDynamicBase()
    {
        foreach (string id in new[] { "JAIL_501", "BE_142" })
        foreach (bool modified in new[] { false, true })
        {
            var g = DiscoverRealProof.New(); var p = g.Player1;
            p.BaseMana = 10; p.UsedMana = 2;
            var target = (Playable)Add(p, id);
            if (modified) CostRules.AddModifier(target, EffectOperator.SUB, 2);
            Assert(target.Cost == (modified ? 6 : 8), "dynamic Cost before mark: " + id);
            var host = Summon(p, "END_018"); InfinityRules.MarkCost(host);
            CostRules.AddModifier(target, EffectOperator.SUB, 1);
            Assert(target.Cost == int.MaxValue - 1, "later modifier follows Infinity: " + id);
            DiscoverRealProof.Saved(g, next => {
                var card = (Playable)next.IdEntityDic[target.Id];
                Kill(next, (Minion)next.IdEntityDic[host.Id]);
                Assert(card.Cost == (modified ? 5 : 7), "restored dynamic Cost at eight mana: " + id);
                var copy = Generic.Copy(next.Player1, card, card, Zone.HAND);
                next.Player1.UsedMana = 5;
                Assert(card.Cost == (modified ? 2 : 4) && copy.Cost == card.Cost,
                    "restored original and copy follow spent mana with ordered modifiers: " + id);
                next.Player1.UsedMana = 10;
                Assert(card.Cost == 0 && copy.Cost == 0, "dynamic floor preserves modifiers: " + id);
                card.Cost = 4; next.Player1.UsedMana = 0;
                Assert(card.Cost == 4, "later finite SET still overrides dynamic base: " + id);
            });
        }
    }

    internal static void Unpinned_InfinityLayers()
    {
        var g = DiscoverRealProof.New(); var p = g.Player1;
        var target = Add(p, "CS2_200");
        CostRules.AddModifier((Playable)target, EffectOperator.SUB, 1);
        var host = Play(g, p, "END_018");
        Core2026Proof.Check(target.Cost == int.MaxValue, "Infinity mark is numeric maximum");
        CostRules.AddModifier((Playable)target, EffectOperator.SUB, 2);
        Core2026Proof.Check(target.Cost == int.MaxValue - 2, "later reduction applies after mark");
        DiscoverRealProof.Saved(g, next => {
            var h = (Minion)next.IdEntityDic[host.Id]; Kill(next, h);
            Core2026Proof.Check(next.IdEntityDic[target.Id].Cost == 3, "removing SET preserves both reductions through clone/load: " + next.IdEntityDic[target.Id].Cost);
        });
    }
}
