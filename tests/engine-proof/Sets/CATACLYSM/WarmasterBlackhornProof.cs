using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WarmasterBlackhornProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_720", (g, p, id) => {
        foreach (var owner in new[] { p, p.Opponent })
        {
            foreach (var card in owner.DeckZone.ToArray()) { owner.DeckZone.Remove(card); owner.SetasideZone.Add(card); }
            foreach (string cardId in new[] { "CS2_120", "CS2_029", "CS2_182", "CS2_008" }) owner.DeckZone.Add(Entity.FromCard(owner, Cards.FromId(cardId)));
        }
        var hand = Add(p, "CS2_120"); var friend = Summon(p, "CS2_120"); Play(g, p, id);
        foreach (var owner in new[] { p, p.Opponent }) Core2026Proof.Check(owner.DeckZone.Select(c => c.Card.Id).SequenceEqual(new[] { "CS2_029", "CS2_182" }) && owner.GraveyardZone.Count(c => c.Card.Id == "CS2_120" || c.Card.Id == "CS2_008") == 2, "cost two inclusive, spell and minion destroyed in both decks; survivors ordered");
        Core2026Proof.Check(hand.Zone.Type == Zone.HAND && friend.Zone.Type == Zone.PLAY, "hand and board untouched");
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == 2 && p.Opponent.DeckZone.Count == 2, "no eligible cards safe");
    });
}
