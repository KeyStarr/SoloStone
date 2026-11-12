using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class GladesongSirenProof
{
    internal static void Cast(Game g, Controller p, string id, HistoryCastKind kind, ICharacter target = null)
    {
        var spell = Entity.FromCard(p, Cards.FromId(id));
        new EffectContext(g, p.Id, p.Hero.Id).CastSpell(spell.Id, kind, target?.Id);
        g.DeathProcessingAndAuraUpdate();
    }

    internal static void Run() => StandardSetsProof.Case("TLC_819", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
        var held = (Minion)Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 6 && held.HasLifeSteal && held.Card.IsRace(Race.NAGA), "base Siren cost/Lifesteal/Naga");
        var holyOnly = g.Clone();
        Play(holyOnly, holyOnly.Player1, "CORE_CS2_093");
        var holyOnlySiren = (IPlayable)holyOnly.IdEntityDic[held.Id];
        Core2026Proof.Check(holyOnlySiren.Cost == 6 && holyOnlySiren[GameTag.COST] == 6,
            "Holy alone keeps Siren COST six");
        Cast(g, p, "CORE_CS2_093", HistoryCastKind.Effect);
        Cast(g, p, "CORE_CS2_093", HistoryCastKind.Repeat);
        Play(g, p, "CORE_EX1_302", Summon(p.Opponent, "CS2_231"));
        Core2026Proof.Check(held.Cost == 6, "Shadow alone and automatic/repeated Holy do not qualify");
        var previousShadow = g.Clone();
        End(previousShadow); End(previousShadow);
        Play(previousShadow, previousShadow.Player1, "CORE_CS2_093");
        var splitTurnSiren = (IPlayable)previousShadow.IdEntityDic[held.Id];
        Core2026Proof.Check(splitTurnSiren.Cost == 6 && splitTurnSiren[GameTag.COST] == 6,
            "previous-turn Shadow and current-turn Holy keep Siren COST six");
        End(g); Play(g, p.Opponent, "CORE_CS2_093");
        Play(g, p.Opponent, "CORE_EX1_302", Summon(p, "CS2_231"));
        Core2026Proof.Check(held.Cost == 6, "opponent casts excluded"); End(g);
        TriggerManager.TriggerHandler counter = entity => ((Spell)entity).IsCountered = true;
        g.TriggerManager.CastSpellTrigger += counter;
        Play(g, p, "CORE_CS2_093"); g.TriggerManager.CastSpellTrigger -= counter;
        Play(g, p, "CORE_EX1_302", Summon(p.Opponent, "CS2_231"));
        Core2026Proof.Check(held.Cost == 6, "countered Holy not completed");
        var clone = g.Clone(); Play(clone, clone.Player1, "CORE_CS2_093");
        Core2026Proof.Check(((IPlayable)clone.IdEntityDic[held.Id]).Cost == 1 && held.Cost == 6, "clone completed Holy changes only clone");
        Play(g, p, "CORE_CS2_093"); Core2026Proof.Check(held.Cost == 1 && held[GameTag.COST] == 1, "both own completed schools set visible cost one");
        var late = Add(p, id); g.AuraUpdate(); Core2026Proof.Check(late.Cost == 1, "both casts before draw count");
        p.Hero.Damage = 10; var victim = Summon(p.Opponent, "CS2_182");
        PlayEntity(g, p, held); Attack(g, held, victim);
        Core2026Proof.Check(p.Hero.Damage == 6, "native Lifesteal heals actual four damage");
        End(g); Core2026Proof.Check(late.Cost == 6, "previous turn schools expire");
    });
}
