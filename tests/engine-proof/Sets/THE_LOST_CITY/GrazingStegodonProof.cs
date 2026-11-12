using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GrazingStegodonProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_827", (g, p, id) => {
        var board = (Minion)Play(g, p, id); var hand = (Minion)Add(p, id);
        var deck = (Minion)Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(deck, 0);
        var enemy = Summon(p.Opponent, id); var dead = Summon(p, id); Kill(g, dead);
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(board.AttackDamage == 0 && ((Minion)clone.IdEntityDic[board.Id]).AttackDamage == 1, "clone across zones isolated");
        End(g); Core2026Proof.Check(board.AttackDamage == 1 && hand.AttackDamage == 1 && deck.AttackDamage == 1 && enemy.AttackDamage == 0 && dead.AttackDamage == 0, "own turn gains Attack in board, hand and deck only");
        End(g); Core2026Proof.Check(board.AttackDamage == 1 && enemy.AttackDamage == 1, "correct side only");
        board.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(board.AttackDamage == 0 && hand.AttackDamage == 2 && deck.AttackDamage == 2, "Silence removes board buff and trigger, other zones keep growing");
        Generic.Draw(p, deck); PlayEntity(g, p, deck); End(g); End(g);
        Core2026Proof.Check(deck.AttackDamage == 3 && hand.AttackDamage == 3 && deck.Zone == p.BoardZone, "zone transitions retain stats and activate exactly one trigger");
    });
}
