using SabberStoneCore.Model.Zones;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ArcaneTripwireProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_881", (g, p, id) => {

        var spellpower = Summon(p, "EX1_012"); int cursor = 0; var tripwire = Play(g, p, id);
        SplitMissileHistory.Flat(g, cursor, tripwire, 5);
        var traps = p.DeckZone.Where(c => c.Card.Id == "JAIL_881t").ToArray();
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && traps.Length == 2 && traps.All(c => c.Cost == 3 && c.Card.Implemented), "five missiles with spell damage; two exact drawn traps");
        p.SetasideZone.Add(p.DeckZone.Remove(traps[1]));
        var clone = g.Clone(); Generic.Draw(clone.Player1, clone.IdEntityDic[traps[0].Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player2.Hero.Damage == 10 && p.Opponent.Hero.Damage == 5, "drawn trap damage isolated in clone");
        int damageAtReplacement = -1;
        void ObserveReplacement(IEntity drawn)
        {
            if (drawn.Controller == p && drawn.Card.Id == "CS2_182") damageAtReplacement = p.Opponent.Hero.Damage;
        }
        g.TriggerManager.DrawTrigger += ObserveReplacement;
        Generic.Draw(p, traps[0]); g.DeathProcessingAndAuraUpdate();
        g.TriggerManager.DrawTrigger -= ObserveReplacement;
        Core2026Proof.Check(damageAtReplacement == 10, "all trap damage resolves before replacement draw triggers");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10 && p.HandZone.Single().Card.Id == "CS2_182" && traps[0].Zone != p.HandZone, "drawn trap deals damage with current spellpower and replaces draw");
        p.DeckZone.Add(p.SetasideZone.Remove(traps[1]));
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); int count = p.DeckZone.Count;
        Generic.Draw(p, traps[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10 && p.DeckZone.Count == count - 1, "burned trap does not cast or replace");
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        spellpower.Silence(); p.Opponent.Hero.Damage = 0;
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        clone = g.CloneExact(history: true); Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && enemy.Damage + p.Opponent.Hero.Damage == 4 && friend.Damage == 0, "deterministic missiles split only among enemies");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); p.Opponent.Hero.Armor = 100;
        var victim = Summon(p, "EX1_008"); for (int i = 0; i < 7; i++) Summon(p.Opponent, "GVG_076");
        Play(g, p, id);
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.Opponent.BoardZone.Count == 0 && p.Opponent.Hero.Armor == 97,
            "missile deathrattles resolve before selecting remaining missile targets");
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity, "two trap shuffle respects deck capacity");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        p.Opponent.Hero.Armor = 100; p.Opponent.Hero.Damage = 0;
        var velen = Summon(p, "EX1_350"); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 92, "Velen doubles four missiles to eight");
        traps = p.DeckZone.Where(c => c.Card.Id == "JAIL_881t").ToArray();
        p.SetasideZone.Add(p.DeckZone.Remove(traps[1]));
        Summon(p, "EX1_012");
        Generic.Draw(p, p.DeckZone.ToList().IndexOf(traps[0])); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Opponent.Hero.Armor == 82 && traps[0].Zone == p.SetasideZone
            && p.DeckZone.Count == 0, "drawn Tripwire doubles base plus spell damage once and draws replacement");
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 72, "played Tripwire doubles base plus spell damage once");
        Kill(g, velen); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 67, "Velen leaving play removes missile multiplier");
    });
}
