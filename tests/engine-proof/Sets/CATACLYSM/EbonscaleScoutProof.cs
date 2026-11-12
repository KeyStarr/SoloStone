using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EbonscaleScoutProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_552", (g, p, id) => {
        var buffed = (Minion)Add(p, id);
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_133e"), buffed, buffed);
        buffed.HasDivineShield = true;
        var buffClone = g.CloneExact(history: true); Play(buffClone, buffClone.Player1, "CATA_111");
        var buffCopy = (Minion)buffClone.IdEntityDic[buffed.Id];
        Core2026Proof.Check(buffCopy.AttackDamage == 9 && buffCopy.Health == 9 && buffCopy.HasDivineShield
            && buffCopy.AppliedEnchantments.Any(e => e.Card.Id == "CATA_133e")
            && buffed.AttackDamage == 5 && buffed.Card.Id == id, "hand replacement preserves +1/+1 enchantment and keyword; clone isolated");
        PlayEntity(buffClone, buffClone.Player1, buffCopy, buffClone.Player2.Hero);
        Core2026Proof.Check(buffCopy.AttackDamage == 9 && buffCopy.Health == 9 && buffClone.Player2.Hero.Damage == 9, "buffed replacement plays with retained stats and effect");
        buffCopy.Silence(); buffClone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(buffCopy.AttackDamage == 8 && buffCopy.Health == 8 && !buffCopy.HasDivineShield, "Silence removes carried buffs and retains replacement base stats");
        p.HandZone.Remove(buffed); p.SetasideZone.Add(buffed);
        var source = Add(p, id); var enemy = Summon(p.Opponent, "CS2_182");
        p.UsedMana = 0; Core2026Proof.Check(p.Options().Any(t => t.Source?.Id == source.Id && t.Target?.Id == p.Opponent.Hero.Id) && p.Options().Any(t => t.Source?.Id == source.Id && t.Target?.Id == enemy.Id), "battlecry targets heroes and minions");
        PlayEntity(g, p, source, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "base Attack deals four");
        source = Add(p, id); Play(g, p, "CS2_182"); Core2026Proof.Check(source.Card.Id == id, "non-Dragon excluded");
        End(g); Play(g, p.Opponent, "CATA_111"); End(g); Core2026Proof.Check(source.Card.Id == id, "opponent Dragon excluded");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CATA_111");
        var transformed = (Minion)clone.IdEntityDic[source.Id];
        Core2026Proof.Check(transformed.Card.Id == "CATA_552t" && transformed.AttackDamage == 8 && transformed.Health == 8 && transformed.Cost == 6 && transformed.Card.IsRace(Race.DRAGON) && source.Card.Id == id, "exact transformed token isolated in clone");
        transformed.AttackDamage = 10; PlayEntity(clone, clone.Player1, transformed, clone.Player2.Hero);
        Core2026Proof.Check(clone.Player2.Hero.Damage == 14, "transformed battlecry uses current Attack");
        Play(g, p, "CATA_111"); PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && source.Card.Id == "CATA_552t", "transformed battlecry kills minion");
        ((Minion)source).Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(((Minion)source).AttackDamage == 8, "Silence retains transformed printed stats");
    });
}
