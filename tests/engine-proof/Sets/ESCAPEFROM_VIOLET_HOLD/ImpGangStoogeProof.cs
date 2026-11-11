using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ImpGangStoogeProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_399", (g, p, id) => {
        var order = p.DeckZone.Select(c => c.Id).ToArray(); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && p.DeckZone.Count == order.Length, "Taunt and no tokens before death"); Kill(g, source);
        var tokens = p.DeckZone.Take(2).Cast<Minion>().ToArray();
        Core2026Proof.Check(tokens.All(m => m.Card.Id == "JAIL_399t1" && m.Card.Implemented && m.AttackDamage == 8 && m.Health == 8 && m.Cost == 8 && m.HasTaunt && m.HasLifeSteal && m.Card.GetRawRace() == Race.DEMON), "two exact Grandmother Imps with Taunt and Lifesteal");
        Core2026Proof.Check(tokens.All(m => g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryFullEntity>()
            .Single(e => e.Entity.Id == m.Id).Entity.Tags[GameTag.ZONE] == (int)Zone.DECK), "tokens created directly in hidden deck zone");
        Core2026Proof.Check(p.DeckZone.Skip(2).Select(c => c.Id).SequenceEqual(order), "bottom insertion preserves existing deck order");
        Core2026Proof.Check(tokens[1].Id < tokens[0].Id && p.HandZone.Count == 0, "created tokens go to deck bottom, not hand or board");
        source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == order.Length + 2, "silence removes Deathrattle");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        source = Summon(p, id); Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == 2 && Generic.Draw(p).Card.Id == "JAIL_399t1", "empty deck gets drawable tokens");
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        source = Summon(p, id); Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity, "deck capacity respected");
    });
}
