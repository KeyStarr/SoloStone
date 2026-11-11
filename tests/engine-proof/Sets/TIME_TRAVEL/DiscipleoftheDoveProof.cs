using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DiscipleoftheDoveProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_037", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_029"); var board = Summon(p, "CS2_182");
        int deck = p.DeckZone.Count;
        var source = (Minion)Play(g, p, id);
        var drawn = p.HandZone.OfType<Minion>().Single(m => m.Id != held.Id);
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && drawn.Card.Id == "CS2_182" && drawn.AttackDamage == 4 && drawn.Health == 7
            && held.Health == 7 && board.Health == 5 && source.Health == 2 && spell.Cost == 4, "draw first, then +2 Health only to hand minions including drawn card");
        Core2026Proof.Check(drawn.AppliedEnchantments.Any(e => e.Card.Id == "TIME_037e"), "pinned hand enchantment");
        foreach (var c in p.DeckZone.ToArray()) { p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        var spellDeck = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(spellDeck);
        var before = held.Health; var damage = p.Hero.Damage;
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == 1 && held.Health == before + 2 && p.Hero.Damage == damage, "no minion match still buffs; no fatigue or spell draw");
        var next = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(next);
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && next.Zone.Type == Zone.GRAVEYARD && p.HandZone.OfType<Minion>().All(m => m.Health >= 7), "full hand burns tutored minion; existing hand still buffed");
    });
}
