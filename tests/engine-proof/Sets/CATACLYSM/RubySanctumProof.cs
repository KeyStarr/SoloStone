using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RubySanctumProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_301", Unpinned_Ruby);
    private static void Unpinned_Ruby(Game g, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id); LocationProofs.Use(location);
        Core2026Proof.Check(location.Durability == 2 && p.AppliedEnchantments.Any(e => e.Card.Id == "CATA_301e"), "activation spends charge and grants conversion");
        var spell = Add(p, "CS1_130"); p.Hero.Damage = 4; p.Opponent.Hero.Damage = 5;
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var owner = next.Player1;
            new HealTask(3, EntityType.HEROES).Process(next, owner, (IPlayable)next.IdEntityDic[spell.Id], null);
            Core2026Proof.Check(owner.Hero.Damage == 7 && owner.Opponent.Hero.Damage == 8
                && !owner.AppliedEnchantments.Any(e => e.Card.Id == "CATA_301e"), "Unpinned_Ruby converts both recipients and consumes once");
            owner.Hero.TakeHeal((IPlayable)next.IdEntityDic[spell.Id], 3);
            Core2026Proof.Check(owner.Hero.Damage == 4, "later operation restores normally");
        }
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_301e"), spell, p);
        p.Hero.IsImmune = true; p.Hero.TakeHeal(spell, 3); p.Hero.IsImmune = false;
        Core2026Proof.Check(p.Hero.Damage == 4 && !p.AppliedEnchantments.Any(e => e.Card.Id == "CATA_301e"), "prevented converted damage consumes grant");
        var expiry = Enchantment.GetInstance(p, spell, p, Cards.FromId("CATA_301e")); expiry.ExpireAtEndOfTurn();
        End(g);
        Core2026Proof.Check(!p.AppliedEnchantments.Any(e => e.Card.Id == "CATA_301e"), "unused grant expires this turn");
    }
}
