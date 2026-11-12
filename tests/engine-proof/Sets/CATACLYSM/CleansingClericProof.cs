using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CleansingClericProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_216", Unpinned_Cleric);
    private static void Unpinned_Cleric(Game g, Controller p, string id)
    {
        var first = (Minion)Play(g, p, id); var second = (Minion)Play(g, p, id);
        first.Silence(); Kill(g, first); Kill(g, second);
        Core2026Proof.Check(HealingRules.Bonus(p) == 4 && HealingRules.Bonus(p.Opponent) == 0, "Unpinned_Cleric game grants outlive bodies");
        var spell = Add(p, "CS1_130"); p.Hero.Damage = 25;
        Summon(p, "EX1_350"); p.Hero.TakeHeal(spell, 3);
        Core2026Proof.Check(p.Hero.Damage == 11, "Unpinned_Cleric (3+2+2)*2 = 14");
        ParkBoard(p); ParkBoard(p.Opponent);
        // A simultaneous Lifesteal operation accumulates five one-damage hits before one +2 bonus.
        foreach (var grant in p.AppliedEnchantments.ToArray().Skip(1)) grant.Remove();
        var lash = Add(p, "ICC_802");
        var victims = Enumerable.Range(0, 5).Select(_ => Summon(p.Opponent, "CS2_182")).Cast<IPlayable>().ToArray();
        p.Hero.Damage = 20;
        new DamageTask(1, EntityType.STACK).Process(g, p, lash, null, new TaskStack { Playables = victims });
        Core2026Proof.Check(p.Hero.Damage == 13, $"Unpinned_Cleric simultaneous five hits produce one 5+2 heal: damage={p.Hero.Damage}, bonus={HealingRules.Bonus(p)}, double={p.ControllerAuraEffects[GameTag.SPELL_HEALING_DOUBLE]}, life={lash.HasLifeSteal}");
        p.Hero.Damage = 20;
        for (int i = 0; i < 4; i++) new DamageTask(1, EntityType.TARGET).Process(g, p, lash, victims[0]);
        Core2026Proof.Check(p.Hero.Damage == 8, "Unpinned_Cleric four separate hits produce four 1+2 heals");
        g.DeathProcessingAndAuraUpdate();
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            Core2026Proof.Check(HealingRules.Bonus(next.Player1) == 2, "Cleric persistent grants survive clone/snapshot");
    }
}
