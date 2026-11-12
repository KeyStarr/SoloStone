using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class MirrexHeldFormProof
{
    internal static void Unpinned_Mirrex(Game g, Controller p, string id)
    {
        End(g); Play(g, p.Opponent, "CS2_182");
        var held = (Minion)Add(p, id);
        Core2026Proof.Check(held.Card.Id == id, "Unpinned_MirrexHandEntry: no retrospective copy");
        Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(held.Card.Id == id, "summons excluded");
        Play(g, p.Opponent, "CS2_200");
        Core2026Proof.Check(held.Card.Id == "CS2_200" && held.Cost == 3 && held.AttackDamage == 3 && held.BaseHealth == 4,
            "Unpinned_MirrexEnchantments: 3-Cost 3/4 definition");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var form = (Minion)next.IdEntityDic[held.Id];
            Generic.AddEnchantmentBlock(next, Cards.FromId("CS2_087e"), next.Player1.Hero, form);
            Effects.ReduceCost(2).ApplyTo(form);
            Play(next, next.Player2, "CS2_182");
            Core2026Proof.Check(form.Card.Id == "CS2_182" && form.Cost == 3 && form.AttackDamage == 3 && form.BaseHealth == 4,
                "second transform removes old buffs/discounts, retains recurrence after clone/load");
            End(next); PlayEntity(next, next.Player1, form);
            form.Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(form.AttackDamage == 4 && form.BaseHealth == 5, "Silence removes Crystalline setter");
            Generic.ReturnToHandBlock(next.Player1, form);
            End(next); Play(next, next.Player2, "CS2_200");
            Core2026Proof.Check(form.Card.Id == "CS2_182" && form.Cost == 4, "leave hand ends recurrence; bounce keeps ordinary copied identity");
        }
    }
}
