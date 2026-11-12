using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class BladeOfTheBlackEmpireProof
{
    // data/rules-evidence/timers/blade-shadow-window.yaml (C; owner-approved current owner-turn cast window).
    internal static void Run() => StandardSetsProof.Case("BE_227", Unpinned_BladeOfTheBlackEmpire);
    private static void Unpinned_BladeOfTheBlackEmpire(Game g, Controller p, string id)
    {
        var blade = Play(g, p, id); End(g);
        Core2026Proof.Check(p.Hero.Fatigue == 1 && p.Hero.Health == 29 && p.Opponent.Hero.Fatigue == 0, "no Shadow: owner takes first fatigue despite nonempty deck");
        End(g); // Opponent turn does not trigger.
        Core2026Proof.Check(p.Hero.Fatigue == 1, "no trigger at opponent end");
        Play(g, p, "CS2_023"); // Arcane, not Shadow.
        PoolPilotProof.Resume(g, next => End(next));
        Core2026Proof.Check(p.Hero.Fatigue == 2 && p.Opponent.Hero.Fatigue == 0, "non-Shadow still fatigues owner");
        End(g);
        Play(g, p, "CORE_RLK_087");
        End(g);
        Core2026Proof.Check(p.Opponent.Hero.Fatigue == 1 && p.Hero.Fatigue == 2, "Shadow player cast fatigues enemy");
        End(g); End(g);
        Core2026Proof.Check(p.Hero.Fatigue == 3 && p.Opponent.Hero.Fatigue == 1, "earlier-turn Shadow does not qualify");
        End(g);
        var spell = Entity.FromCard(p, Cards.FromId("CORE_RLK_087"), zone: p.SetasideZone);
        new EffectContext(g, p.Id, blade.Id).CastSpell(spell.Id, HistoryCastKind.Effect);
        End(g);
        Core2026Proof.Check(p.Opponent.Hero.Fatigue == 2, "effect-cast Shadow qualifies");
        End(g); Play(g, p, "CS2_106"); End(g);
        Core2026Proof.Check(p.Hero.Fatigue == 3 && p.Opponent.Hero.Fatigue == 2, "replaced weapon stops trigger");
        End(g); Play(g, p, "CORE_RLK_087"); Play(g, p, id); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Fatigue == 3, "Shadow before equipping qualifies");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
    }
}
