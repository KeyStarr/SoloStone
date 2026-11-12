using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CultNeophyteProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SCH_713", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId("SCH_713e").AssetId == 60044 && Cards.FromId("SCH_713e").Text == "Your spells cost (1) more this turn.", "exact pinned tax enchantment identity and text");
        var own = Add(p, "CS2_029"); var minion = Add(p.Opponent, "CS2_182");
        var spell = Add(p.Opponent, "CS2_029"); var weapon = Add(p.Opponent, "JAIL_376");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 2, "printed 3/2");
        source.Silence(); Kill(g, source);
        Core2026Proof.Check(own.Cost == 4 && spell.Cost == 5 && minion.Cost == 4 && weapon.Cost == 1
            && p.Opponent.Hero.HeroPower.Cost == 2, "Battlecry spell tax survives source Silence/death and excludes own hand/nonspells/power");
        Core2026Proof.Check(p.Opponent.AppliedEnchantments.Single(e => e.Card.Id == "SCH_713e").Controller == p, "exact caster-owned controller enchantment");
        foreach (var branch in new[] { g.Clone(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            End(branch);
            Core2026Proof.Check(branch.IdEntityDic[spell.Id].Cost == 5, "clone/snapshot tax survives caster turn end despite pinned one-turn tag");
            End(branch);
            Core2026Proof.Check(branch.IdEntityDic[spell.Id].Cost == 4 && spell.Cost == 5, "clone/snapshot opponent-turn expiry leaves original unaffected");
        }
        End(g);
        var fresh = Add(p.Opponent, "CS2_008"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(fresh.Cost == 1 && spell[GameTag.COST] == 5, "newly added opponent spell taxed and COST visible");
        PlayEntity(g, p.Opponent, fresh, p.Hero);
        Core2026Proof.Check(p.Opponent.UsedMana == 1 && p.Hero.Damage == 1, "taxed spell pays derived cost");
        End(g);
        Core2026Proof.Check(spell.Cost == 4 && !p.Opponent.AppliedEnchantments.Any(e => e.Card.Id == "SCH_713e"), "tax expires after opponent turn");
        Play(g, p, id); Play(g, p, id); End(g);
        Core2026Proof.Check(spell.Cost == 6 && own.Cost == 4 && minion.Cost == 4, "stacked Battlecries tax opponent spells twice");
        End(g); Core2026Proof.Check(spell.Cost == 4, "stacked taxes both expire");
    });
}
