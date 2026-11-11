using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
// Summon placement follows owner-mandated living-source physical-slot rule (batch 11).
internal static class FinjaTheFlyingStarProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CFM_344", (g, p, id) => {
        var first = (Minion)Entity.FromCard(p, Cards.FromId("EX1_506a"), zone: p.DeckZone);
        var second = (Minion)Entity.FromCard(p, Cards.FromId("EX1_506"), zone: p.DeckZone);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var source = Summon(p, id); var right = Summon(p, "CS2_182");
        var victim = Summon(p.Opponent, "EX1_506a"); Core2026Proof.Check(source.HasStealth, "printed Stealth");
        PoolPilotProof.Resume(g, next => {
            Attack(next, (Minion)next.IdEntityDic[source.Id], (Minion)next.IdEntityDic[victim.Id]); var owner = next.Player1;
            Core2026Proof.Check(owner.BoardZone.Count(m => m.Id == first.Id || m.Id == second.Id) == 2
                && next.IdEntityDic[first.Id].Zone == owner.BoardZone && next.IdEntityDic[second.Id].Zone == owner.BoardZone, "recruits exact two existing deck entities, no Battlecry");
            Core2026Proof.Check(((Minion)next.IdEntityDic[first.Id]).AttackDamage == 1 && ((Minion)next.IdEntityDic[first.Id]).Health == 1
                && ((Minion)next.IdEntityDic[second.Id]).AttackDamage == 2 && ((Minion)next.IdEntityDic[second.Id]).Health == 1, "recruited Murlocs retain exact stats");
            Core2026Proof.Check(owner.BoardZone.GetSlot(0).Id == location.Id && owner.BoardZone.GetSlot(1).Id == left.Id
                && owner.BoardZone.GetSlot(2).Id == source.Id && owner.BoardZone.GetSlot(5).Id == right.Id
                && new[] { owner.BoardZone.GetSlot(3).Id, owner.BoardZone.GetSlot(4).Id }.Order().SequenceEqual(new[] { first.Id, second.Id }.Order()), "both recruits beside living source with locations counted");
        });
        foreach (string scenario in new[] { "survives", "hero", "enemy-attack", "silenced", "departed", "empty", "one", "three", "full", "one-slot" })
        {
            ParkBoard(p); ParkBoard(p.Opponent); PoolPilotProof.ClassDeck(p);
            source = Summon(p, id); var recruited = Entity.FromCard(p, Cards.FromId("EX1_506a"), zone: p.DeckZone);
            if (scenario != "one") Entity.FromCard(p, Cards.FromId("EX1_506"), zone: p.DeckZone);
            if (scenario == "three") Entity.FromCard(p, Cards.FromId("EX1_103"), zone: p.DeckZone);
            var plain = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
            if (scenario == "empty") { p.SetasideZone.Add(p.DeckZone.Remove(recruited)); foreach (var c in p.DeckZone.Where(c => c.Card.IsRace(Race.MURLOC)).ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c)); }
            int count = p.DeckZone.Count; long randomDraws = g.Random.DrawCount;
            if (scenario == "full" || scenario == "one-slot") { LocationProofs.Location(p); while (p.BoardZone.SlotCount < (scenario == "full" ? 7 : 6)) Summon(p, "CS2_182"); }
            victim = Summon(p.Opponent, scenario == "survives" ? "CS2_182" : "EX1_506a");
            if (scenario == "one-slot")
            {
                var selected = new HashSet<int>();
                for (int seed = 1; seed <= 16; seed++)
                {
                    var trial = g.CloneExact(history: true); trial.SetRandomSeed(seed);
                    Attack(trial, (Minion)trial.IdEntityDic[source.Id], (Minion)trial.IdEntityDic[victim.Id]);
                    var recruit = trial.Player1.BoardZone.Where(m => m.Id != source.Id && m.IsRace(Race.MURLOC)).Single();
                    selected.Add(recruit.Id);
                    Core2026Proof.Check(trial.Player1.BoardZone.SlotCount == 7 && trial.Player1.DeckZone.Count == count - 1,
                        "one free physical slot recruits exactly one of two deck Murlocs");
                }
                Core2026Proof.Check(selected.Count == 2, "both deck Murlocs can win the one-slot recruitment across seeds");
            }
            if (scenario == "silenced") source.Silence();
            if (scenario == "departed") { Kill(g, source); source = Summon(p, "CS2_182"); }
            if (scenario == "enemy-attack") { source.HasStealth = false; End(g); Attack(g, victim, source); Core2026Proof.Check(p.DeckZone.Count == count, "defending does not recruit"); End(g); continue; }
            else Attack(g, source, scenario == "hero" ? p.Opponent.Hero : victim);
            int expected = scenario == "three" ? 2 : scenario == "one" || scenario == "one-slot" ? 1 : 0;
            Core2026Proof.Check(p.DeckZone.Count == count - expected && plain.Zone == p.DeckZone, "recruit boundary: " + scenario);
            if (scenario == "three") Core2026Proof.Check(g.Random.DrawCount > randomDraws && p.BoardZone.Count(m => m.IsRace(Race.MURLOC)) == 3, "game RNG samples exactly two of three deck Murlocs");
            if (expected == 1) Core2026Proof.Check(recruited.Zone == p.BoardZone || p.BoardZone.Any(m => m.Card.Id == "EX1_506"), "one available Murloc/slot recruited");
        }
        ParkBoard(p); ParkBoard(p.Opponent); source = Summon(p, id); var lethal = g.CloneExact(history: true);
        lethal.Player2.Hero.Damage = 29; int remaining = lethal.Player1.DeckZone.Count;
        Attack(lethal, (Minion)lethal.IdEntityDic[source.Id], lethal.Player2.Hero);
        Core2026Proof.Check(lethal.Player1.DeckZone.Count == remaining, "killing a hero cannot recruit Murlocs");
    }, legacyPoolMember: true);
}
