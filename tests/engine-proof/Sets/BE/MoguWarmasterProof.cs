using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MoguWarmasterProof
{
    internal static void Run() => StandardSetsProof.Case("BE_008", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var spell = Add(p, "CS2_029"); var second = Add(p, "CS2_029");
        Core2026Proof.Check(spell.Cost == 4, "own play grants no discount");
        Summon(p, "CS2_182"); Play(g, p, "CS2_033"); Core2026Proof.Check(spell.Cost == 4, "summon and class minion play grant no discount");
        Play(g, p, "CS2_182");
        Core2026Proof.Check(spell.Cost == 3 && second.Cost == 3 && spell[GameTag.COST] == 3, "neutral play discounts spells with visible COST");
        var clone = g.CloneExact(history: true); PlayEntity(clone, clone.Player1, clone.IdEntityDic[spell.Id], clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[second.Id].Cost == 4 && spell.Cost == 3 && second.Cost == 3, "clone consumes discount independently");
        Play(g, p, "CS2_182"); Core2026Proof.Check(spell.Cost == 2, "neutral plays stack reductions");
        source.Silence(); Core2026Proof.Check(spell.Cost == 2, "earned controller discounts survive source Silence");
        PlayEntity(g, p, spell, p.Opponent.Hero); Core2026Proof.Check(second.Cost == 4, "next friendly spell consumes all discounts");
        Play(g, p, "CS2_182"); Core2026Proof.Check(second.Cost == 4, "silenced source grants no new discount");
        ClearBoard(g, p); source = Summon(p, id); Play(g, p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(second.Cost == 3, "earned discount survives source leaving play");
        End(g); Core2026Proof.Check(second.Cost == 4, "unused discount expires at end of turn");
        source = Summon(p, id);
        Play(g, p.Opponent, "CS2_182"); Core2026Proof.Check(second.Cost == 4, "opponent neutral play grants no discount");
        source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack()); g.AuraUpdate();
        Play(g, p.Opponent, "CS2_008", p.Hero);
        Core2026Proof.Check(second.Cost == 3, "opponent spell cannot consume a friendly controller discount");
        End(g); Core2026Proof.Check(second.Cost == 4, "discount expires on either turn end");
        Play(g, p, "CS2_182"); var zero = Add(p, "CS2_008");
        Core2026Proof.Check(zero.Cost == 0, "discount floors at zero"); PlayEntity(g, p, zero, p.Opponent.Hero); Core2026Proof.Check(second.Cost == 4, "zero-Cost spell consumes discount");

        // Keep the silenced-target regression if BE_196 is enabled after a core copy-initialization fix.
        if (!StandardSets.CollectibleIds.Contains("BE_196"))
        {
            Core2026Proof.Check(!EffectModules.ByDbfId.ContainsKey(130092), "unsupported silenced-target copier remains unregistered");
            return;
        }
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        Summon(p, "BE_196");
        var silenced = Summon(p.Opponent, id); silenced.Silence();
        End(g); End(g);
        var copy = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(copy.IsSilenced && copy.Damage == 2 && silenced.Damage == 2, "silenced Warmaster copied and forced combat resolved");
        var undiscounted = Add(p, "CS2_029"); Play(g, p, "CS2_182");
        Core2026Proof.Check(undiscounted.Cost == 4, "copying a silenced enemy Warmaster must not restore its discount trigger");
    });
}
