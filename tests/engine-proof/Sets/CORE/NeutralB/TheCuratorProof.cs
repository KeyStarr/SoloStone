using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class TheCuratorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_KAR_061", (g, p, id) => {
        PoolPilotProof.ClassDeck(p);
        foreach (string card in new[] { "CS2_172", "EX1_043", "EX1_506", "CS2_182" }) Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.IsRace(Race.MECHANICAL) && source.AttackDamage == 4 && source.Health == 6, "current 5-Mana 4/6 Taunt Mech");
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).Order().SequenceEqual(new[] { "CS2_172", "EX1_043", "EX1_506" })
            && p.DeckZone.Single().Card.Id == "CS2_182", "draw one of each exact tribe, leave nonmatching minion");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p); int fatigue = p.Hero.Fatigue;
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0 && p.Hero.Fatigue == fatigue, "no matching cards means no draw or fatigue");
        foreach (string card in new[] { "CS2_172", "EX1_043", "EX1_506" }) Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
        PoolPilotProof.FillHand(p, 9); source = (Minion)Add(p, id); PlayEntity(g, p, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Any(c => c.Card.Id == "CS2_172") && p.DeckZone.Count == 0
            && p.GraveyardZone.Any(c => c.Card.Id == "EX1_043") && p.GraveyardZone.Any(c => c.Card.Id == "EX1_506"), "one hand slot keeps Beast, Dragon and Murloc burn in order");
        source.Silence(); Core2026Proof.Check(!source.HasTaunt, "Silence removes Taunt");
    }, legacyPoolMember: true);
}
