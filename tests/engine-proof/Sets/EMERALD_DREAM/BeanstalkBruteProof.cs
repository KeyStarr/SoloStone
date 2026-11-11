using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BeanstalkBruteProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_230", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var bottom = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(bottom);
        var matches = new List<IPlayable>();
        foreach (string next in new[] { "CS2_182", "CS2_029", "CS2_182", "CS2_029", "CS2_182" }) {
            var entity = Entity.FromCard(p, Cards.FromId(next)); p.DeckZone.Add(entity);
            if (entity is Minion) matches.Add(entity);
        }
        var order = p.DeckZone.Select(c => c.Id).ToArray(); Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Select(c => c.Id).SequenceEqual(order), "buff preserves deck order");
        Core2026Proof.Check(matches.Cast<Minion>().All(m => m.AttackDamage == 8 && m.Health == 9 && m.AppliedEnchantments.Any(e => e.Card.Id == "EDR_230e")), "top three minions receive exact +4/+4 enchantment across spells");
        Core2026Proof.Check(((Minion)bottom).AttackDamage == 4 && p.DeckZone.Where(c => c is Spell).All(c => c.AppliedEnchantments?.Count is null or 0), "fourth minion and spells unchanged");
        var drawn = (Minion)Generic.Draw(p); Core2026Proof.Check(drawn.AttackDamage == 8 && drawn.Health == 9, "deck buff survives draw");
        PlayEntity(g, p, drawn); drawn.Silence(); Core2026Proof.Check(drawn.AttackDamage == 4 && drawn.Health == 5, "deck buff survives play and silence removes it");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var only = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(only); Play(g, p, id);
        Core2026Proof.Check(only.AttackDamage == 8 && only.Health == 9, "fewer than three minions buff available ones");
        p.SetasideZone.Add(p.DeckZone.Remove(only)); Play(g, p, id); Core2026Proof.Check(p.Hero.Damage == 0, "empty deck does nothing");
    });
}
