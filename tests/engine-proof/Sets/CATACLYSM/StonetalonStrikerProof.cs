using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StonetalonStrikerProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_551", (g, p, id) => {
        var buffed = (Minion)Add(p, id);
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_133e"), buffed, buffed);
        buffed.HasDivineShield = true;
        var buffClone = g.CloneExact(history: true); Play(buffClone, buffClone.Player1, "CATA_111");
        var buffCopy = (Minion)buffClone.IdEntityDic[buffed.Id];
        Core2026Proof.Check(buffCopy.AttackDamage == 7 && buffCopy.Health == 7 && buffCopy.HasDivineShield
            && buffCopy.AppliedEnchantments.Any(e => e.Card.Id == "CATA_133e")
            && buffed.AttackDamage == 4 && buffed.Card.Id == id, "hand replacement preserves +1/+1 enchantment and keyword; clone isolated");
        PlayEntity(buffClone, buffClone.Player1, buffCopy);
        Core2026Proof.Check(buffCopy.AttackDamage == 7 && buffCopy.Health == 7, "buffed replacement plays with retained stats and effect");
        buffCopy.Silence(); buffClone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(buffCopy.AttackDamage == 6 && buffCopy.Health == 6 && !buffCopy.HasDivineShield, "Silence removes carried buffs and retains replacement base stats");
        p.HandZone.Remove(buffed); p.SetasideZone.Add(buffed);
        var source = Add(p, id); Play(g, p, "CS2_182");
        Core2026Proof.Check(source.Card.Id == id, "non-Dragon does not transform");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CATA_111");
        var transformed = (Minion)clone.IdEntityDic[source.Id];
        Core2026Proof.Check(transformed.Card.Id == "CATA_551t" && transformed.AttackDamage == 6 && transformed.Health == 6 && transformed.HasTaunt && transformed.Card.IsRace(Race.DRAGON) && transformed.Cost == 3 && source.Card.Id == id, "exact transformed token isolated in clone");
        End(g); Play(g, p.Opponent, "CATA_111"); End(g);
        Core2026Proof.Check(source.Card.Id == id, "opponent Dragon excluded");
        PlayEntity(g, p, source); Core2026Proof.Check(((Minion)source).HasTaunt, "base Taunt");
        Play(g, p, "CATA_111"); Core2026Proof.Check(source.Card.Id == id, "board source does not transform");
        source = Add(p, id); Play(g, p, "CATA_111");
        Core2026Proof.Check(source.Card.Id == "CATA_551t", "friendly played Dragon transforms hand source");
        ClearBoard(g, p); PlayEntity(g, p, source); ((Minion)source).Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(((Minion)source).AttackDamage == 6 && !((Minion)source).HasTaunt, "transformed stats are printed; Silence removes Taunt");
    });
}
