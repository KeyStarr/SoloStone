using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class ClutchOfCorruptionProof
{
    internal static void Run() => Unpinned_EggSnapshot();
    private static void Unpinned_EggSnapshot() => StandardSetsProof.Case("EDR_454", (g, p, id) => {
        var dragon = Summon(p, "EX1_043"); dragon.AttackDamage = 9; dragon.BaseHealth = 10; dragon.Damage = 2;
        var ordinary = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "EX1_043");
        var location = LocationProofs.Play(p, id);
        Core2026Proof.Check(!LocationRules.CanActivate(p, location, ordinary) && !LocationRules.CanActivate(p, location, enemy)
            && LocationRules.CanActivate(p, location, dragon), "friendly Dragon target only");
        LocationProofs.Use(location, dragon); var egg = p.BoardZone.Single(m => m.Card.Id == "EDR_454t");
        var copy = (Minion)Generic.Copy(p, p.Hero, egg, Zone.PLAY);
        dragon.AttackDamage = 20; Kill(g, dragon);
        Kill(g, egg); Kill(g, copy); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_043" && m.AttackDamage == 9 && m.BaseHealth == 10 && m.Damage == 2) == 2,
            "captured Dragon state and independent copied Egg survive source changes/death");
    });
}
