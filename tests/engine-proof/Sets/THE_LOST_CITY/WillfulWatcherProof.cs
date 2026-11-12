using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WillfulWatcherProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_621", (g, p, id) => {
        var top = p.DeckZone.Reverse().Take(3).ToArray(); var next = p.DeckZone.Reverse().Skip(3).First();
        int deck = p.DeckZone.Count; int hand = p.HandZone.Count; int health = p.Hero.Health;
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt, "printed Taunt"); Kill(g, source);
        Core2026Proof.Check(top.All(c => c.Zone == p.GraveyardZone) && p.DeckZone.TopCard == next && p.DeckZone.Count == deck - 3 && p.HandZone.Count == hand && p.Hero.Health == health, "top three destroyed without draws or fatigue");
        while (p.DeckZone.Count > 2) { var c = p.DeckZone.TopCard; p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == 0 && p.Hero.Health == health, "short deck destroys only available two");
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(p.Hero.Health == health, "empty deck does not fatigue");
        Generic.ShuffleIntoDeck(p, null, Entity.FromCard(p, Cards.FromId("CS2_182")));
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == 1, "silence prevents deck destruction");
    });
}
