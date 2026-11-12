using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FrontlineIronbornProof
{
    internal static void Run() => Unpinned_FrontlineIronborn();
    private static void Unpinned_FrontlineIronborn() => StandardSetsProof.Case("BE_996", (g, p, id) => {
        var m = (Minion)Play(g, p, id); var friendly = Summon(p, "CS2_182");
        Kill(g, friendly); Core2026Proof.Check(m.AttackDamage == 3 && BonusEffectsProof.Count(m) == 0, "friendly death excluded");
        Kill(g, Summon(p.Opponent, "CS2_182"));
        Core2026Proof.Check(m.AttackDamage == 5 && m.BaseHealth == 7 && BonusEffectsProof.Count(m) == 1, "enemy death on owner turn grants stats and keyword");
        End(g); Kill(g, Summon(p.Opponent, "CS2_182"));
        Core2026Proof.Check(m.AttackDamage == 5 && m.BaseHealth == 7, "enemy-turn death excluded"); End(g);
        BonusEffects.GiveRandom(m, p.Hero, 8); int before = m.AttackDamage;
        Kill(g, Summon(p.Opponent, "CS2_182"));
        Core2026Proof.Check(m.AttackDamage == before + 2 && BonusEffects.Eligible(m).Length == 0, "saturated Ironborn still gains stats");
        m.Silence(); Core2026Proof.Check(m.AttackDamage == 3 && m.BaseHealth == 5 && BonusEffectsProof.Count(m) == 0, "Silence clears stats and keywords");
    });
}
