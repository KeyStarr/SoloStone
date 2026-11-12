using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ShapeshifterHeldFormProof
{
    internal static void Unpinned_Shapeshifter(Game g, Controller p, string id)
    {
        var held = (Minion)Add(p, id);
        foreach (var c in p.Opponent.HandZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(c));
        foreach (var c in p.Opponent.DeckZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.DeckZone.Remove(c));
        Add(p.Opponent, "CS2_029");
        End(g); Core2026Proof.Check(held.Card.Id == id, "opponent start does not transform"); End(g);
        Core2026Proof.Check(held.Card.Id == id, "empty minion pool preserves form");
        var target = (Minion)Add(p.Opponent, "CS2_200");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_087e"), p.Opponent.Hero, target);
        Effects.ReduceCost(4).ApplyTo(target);
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var form = (Minion)next.IdEntityDic[held.Id];
            Generic.AddEnchantmentBlock(next, Cards.FromId("CS2_087e"), next.Player1.Hero, form);
            form.HasDivineShield = true; form.HasCharge = true; form.IsFrozen = true;
            Effects.ReduceCost(1).ApplyTo(form);
            End(next); End(next);
            Core2026Proof.Check(form.Card.Id == "CS2_200" && form.Cost == 6 && form.AttackDamage == 6 && form.BaseHealth == 7,
                "Unpinned_ShapeshifterCopyState: base target only, old buffs cleared");
            Core2026Proof.Check(!form.HasDivineShield && !form.HasCharge && !form.IsFrozen, "ordinary keyword/freeze grants clear with old form");
            var enemy = next.Player2;
            enemy.SetasideZone.Add(enemy.HandZone.Remove(next.IdEntityDic[target.Id])); Add(enemy, "CS2_182");
            End(next); End(next);
            Core2026Proof.Check(form.Card.Id == "CS2_182", "recurrence survives two transformations and clone/load");
            PlayEntity(next, next.Player1, form); Generic.ReturnToHandBlock(next.Player1, form);
            End(next); End(next);
            Core2026Proof.Check(form.AppliedEnchantments?.Any(e => e.Card.Id == "TIME_876e") != true, "play/bounce does not restore recurrence");
        }
    }
}
