using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RavenousFelhunterProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_891", (g, p, id) => Resurrection(g, id, false));

    internal static void Resurrection(Game original, string id, bool highCost)
    {
        string eligible = highCost ? "EDR_110" : "EDR_459";
        string excluded = highCost ? "EDR_459" : "EDR_110";
        var definition = Cards.FromId(eligible);
        foreach (bool sword in new[] { false, true })
        {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var dead = Summon(p, eligible);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_009e"), p.Hero, dead);
            dead.Silence(); Kill(g, dead);
            var location = LocationProofs.Location(p);
            var left = Summon(p, "CS2_171"); var source = PlayAt(g, p, id, 2); var right = Summon(p, "CS2_171");
            Core2026Proof.Check(source.IsRace(Race.DEMON) && source.HasDeathrattle, "printed Demon and Deathrattle");
            if (sword) Play(g, p, "EX1_366");
            var clone = g.CloneExact(history: true);
            Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
            Core2026Proof.Check(p.BoardZone.Count == 3 && clone.Player1.BoardZone.Count == 4, "clone death/resurrection leaves original untouched");
            Kill(g, source);
            var raised = p.BoardZone.Where(m => m.Card.Id == eligible).OrderBy(m => m.Id).ToArray();
            Core2026Proof.Check(raised.Length == 2 && raised.All(m => m.Id != dead.Id && m.Damage == 0 && !m.ToBeDestroyed && m.HasDeathrattle),
                "two fresh entities of selected Deathrattle identity");
            Core2026Proof.Check(raised.All(m => m.Poisonous == highCost), "copies retain selected identity's printed Poisonous keyword");
            CheckDeathrattleSlot(p, (location, left, right), raised,
                "dying source's physical slot takes resurrection then copy, past a location");
            Core2026Proof.Check(raised[0].AttackDamage == definition.ATK + (sword ? 1 : 0)
                && raised[0].BaseHealth == definition.Health + (sword ? 1 : 0)
                && raised[1].AttackDamage == definition.ATK + (sword ? 2 : 0)
                && raised[1].BaseHealth == definition.Health + (sword ? 2 : 0),
                "resurrection resets old buffs; copy keeps first Sword buff and gains its own");
            if (sword) Core2026Proof.Check(p.Hero.Weapon.Durability == 3, "two summons consume exactly two Sword charges");
            raised[1].Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(raised[1].AttackDamage == definition.ATK && raised[1].BaseHealth == definition.Health
                && raised[0].HasDeathrattle, "Silence of copy does not change resurrected original");
        }
        {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var enemy = Summon(p.Opponent, eligible); Kill(g, enemy);
            var discarded = Add(p, eligible); Generic.DiscardBlock(p, discarded);
            p.GraveyardZone.Add(Entity.FromCard(p, Cards.FromId(eligible)));
            var wrongCost = Summon(p, excluded); wrongCost.Silence(); Kill(g, wrongCost);
            var ordinary = Summon(p, highCost ? "CS2_200" : "CS2_182"); Kill(g, ordinary);
            var source = (Minion)Play(g, p, id); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 0, "exclude opponent deaths, discard/burn, wrong cost, non-Deathrattle and own identity");
            source = (Minion)Play(g, p, id); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 0, "another death of same identity remains excluded");
        }
        foreach (int spaces in new[] { 1, 2 })
        {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var dead = Summon(p, eligible); dead.Silence(); Kill(g, dead);
            var source = (Minion)Play(g, p, id);
            while (p.BoardZone.Count < 8 - spaces) Summon(p, "CS2_171");
            Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == eligible) == spaces,
                "board capacity limits resurrection and copy");
            int entities = g.IdEntityDic.Count;
            source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
            g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(g.IdEntityDic.Count == entities, "full board creates no entities");
        }
        {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var dead = Summon(p, eligible); dead.Silence(); Kill(g, dead);
            var source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes source Deathrattle");
        }
        {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var dead = Summon(p, eligible); dead.Silence(); Kill(g, dead);
            var left = Summon(p, "CS2_171"); var source = Summon(p, id); var right = Summon(p, "CS2_171");
            left[GameTag.DORMANT] = 1;
            source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
            g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
            var raised = p.BoardZone.Where(m => m.Card.Id == eligible).OrderBy(m => m.Id).ToArray();
            Core2026Proof.Check(raised.Length == 2 && p.BoardZone.Select(m => m.Id)
                .SequenceEqual(new[] { left.Id, source.Id, raised[0].Id, raised[1].Id, right.Id }),
                "triggered Deathrattle while source remains uses actual position, including Dormant neighbour");
        }
        {
            var g = original.CloneExact(history: true); var p = g.Player1;
            foreach (string cardId in new[] { eligible, highCost ? "FIR_778" : "EDR_485" })
            { var dead = Summon(p, cardId); dead.Silence(); Kill(g, dead); }
            var source = (Minion)Play(g, p, id); var clone = g.CloneExact(history: true);
            Kill(g, source); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
            Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1
                && p.BoardZone.Select(m => m.Card.Id).SequenceEqual(clone.Player1.BoardZone.Select(m => m.Card.Id)),
                "one seeded random eligible death selected then copied; cloned RNG agrees");
        }
    }
}
