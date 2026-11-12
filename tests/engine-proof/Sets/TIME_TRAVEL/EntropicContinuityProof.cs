using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EntropicContinuityProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_026", (g, p, id) => {
        var source = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var held = (Minion)Add(p, "CS2_182"); int count = p.DeckZone.Count;
        Play(g, p, id); var shreds = p.DeckZone.Where(c => c.Card.Id == "TIME_025t").ToArray();
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 6 && enemy.AttackDamage == 4 && held.AttackDamage == 4, "only friendly board gets +1/+1");
        Core2026Proof.Check(source.AppliedEnchantments.Single().Card.Id == "TIME_026e" && shreds.Length == 2 && p.DeckZone.Count == count + 2 && shreds.All(c => c.Card.Cost == 0 && c.Card.Implemented), "exact buff and two scripted Shreds");
        Generic.Draw(p, shreds[0]); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.Hero.Damage == 3 && shreds[0].Zone == p.GraveyardZone, "shuffled Shred casts on draw");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 5, "Silence removes buff");
        ClearBoard(g, p); count = p.DeckZone.Count; Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == count + 2, "empty board still shuffles");
    });
}
