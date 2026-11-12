using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BlackMarketAuctioneerProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_718", (g, p, id) => {

        var source = (Minion)Play(g, p, id); int deck = p.DeckZone.Count;
        Play(g, p, "CS2_182"); Core2026Proof.Check(p.DeckZone.Count == deck, "minion play does not draw");
        var clone = g.CloneExact(); Play(clone, clone.Player1, "GAME_005");
        Core2026Proof.Check(clone.Player1.DeckZone.Count == deck - 1 && p.DeckZone.Count == deck, "clone spell draws independently");
        Play(g, p, "CS2_029", p.Opponent.Hero); Core2026Proof.Check(p.DeckZone.Count == deck - 1, "friendly spell draws one");
        End(g); deck = p.DeckZone.Count; Play(g, p.Opponent, "CS2_029", p.Hero);
        Core2026Proof.Check(p.DeckZone.Count == deck, "enemy cast excluded"); End(g);
        source.Silence(); deck = p.DeckZone.Count; Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.DeckZone.Count == deck, "Silence removes trigger"); Kill(g, source);
        source = Summon(p, id); Kill(g, source); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.DeckZone.Count == deck, "dead source does not draw");
    });
}
