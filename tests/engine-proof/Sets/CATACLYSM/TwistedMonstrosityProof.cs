using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TwistedMonstrosityProof
{
    internal static void Run() => Unpinned_TwistedMonstrosity();
    private static void Unpinned_TwistedMonstrosity() => StandardSetsProof.Case("CATA_206", (g, p, id) => {
        var m = (Minion)Add(p, id);
        Core2026Proof.Check(m.HasTaunt && m.CantBeTargetedBySpells && m[BonusEffects.Keyword1] == 1211 && m[BonusEffects.Keyword2] == 190, "initial Elusive/Taunt");
        End(g); var pair = new[] { m[BonusEffects.Keyword1], m[BonusEffects.Keyword2] };
        Core2026Proof.Check(pair.Distinct().Count() == 2 && !pair.Order().SequenceEqual(new[] {190,1211}) && BonusEffectsProof.Count(m) == 2, "owner end replaces with different distinct pair");
        var state = g.CloneExact(history:true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        End(g); Core2026Proof.Check(pair.SequenceEqual(new[] { m[BonusEffects.Keyword1], m[BonusEffects.Keyword2] }), "opponent end does not reroll");
        foreach (var next in new[] {g,state,loaded})
        {
            if (next.CurrentPlayer != next.Player1) End(next);
            var held = (Minion)next.IdEntityDic[m.Id];
            next.Player1.BaseMana = 10; next.Player1.UsedMana=0;
            Core2026Proof.Check(next.Process(PlayCardTask.Minion(next.Player1,held)), "play current pair");
            held.HasDivineShield = false;
            var copy = (Minion)Generic.Copy(next.Player1, held, held, Zone.PLAY);
            Core2026Proof.Check(copy[BonusEffects.Keyword1] == held[BonusEffects.Keyword1] && copy[BonusEffects.Keyword2] == held[BonusEffects.Keyword2]
                && BonusEffects.Pool.All(k => BonusEffects.Active(copy,k) == BonusEffects.Active(held,k)), "copy current pair and spent state");
            End(next); End(next);
            Core2026Proof.Check(pair.SequenceEqual(new[] { held[BonusEffects.Keyword1], held[BonusEffects.Keyword2] }), "board pair no longer rerolls");
            copy.Silence(); Core2026Proof.Check(BonusEffectsProof.Count(copy) == 0, "Silence removes pair");
        }
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(state) && HsSim.Canonical.State(g) == HsSim.Canonical.State(loaded), "clone/snapshot pair trigger and RNG independent");
    });
}
