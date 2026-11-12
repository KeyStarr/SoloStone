using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SeismopodProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_421", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var enemyHeld = (Minion)Add(p.Opponent, "CS2_182"); var board = Summon(p, "CS2_182");
        var deckSpell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(deckSpell, 0);
        var deck = p.DeckZone.OfType<Minion>().ToArray(); var enemyDeck = p.Opponent.DeckZone.Cast<Minion>().ToArray();
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.CantBeTargetedBySpells && source.CantBeTargetedByHeroPowers, "Taunt and Elusive");
        Kill(g, source);
        Core2026Proof.Check(held.AttackDamage == 7 && held.Health == 8 && deck.All(m => m.AttackDamage == 7 && m.Health == 8), "hand and deck minions gain +3/+3");
        Core2026Proof.Check(held.AppliedEnchantments.Any(e => e.Card.Id == "DINO_421e") && spell.Cost == 4 && deckSpell.Cost == 4 && deckSpell.Zone == p.DeckZone && board.Health == 5 && enemyHeld.Health == 5 && enemyDeck.All(m => m.Health == 5), "pinned buff excludes spells, board and opponent");
        var drawn = Generic.Draw(p) as Minion; Core2026Proof.Check(drawn.AttackDamage == 7 && drawn.Health == 8, "deck buff survives draw");
        PlayEntity(g, p, held); Core2026Proof.Check(held.Health == 8, "hand buff survives play");
        Play(g, p, "BE_228", held); Core2026Proof.Check(held.AttackDamage == 4 && held.Health == 5, "silence removes buff");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(deck.Where(m => m.Zone == p.DeckZone).All(m => m.Health == 8), "silenced Seismopod grants no second buff");
    });
}
