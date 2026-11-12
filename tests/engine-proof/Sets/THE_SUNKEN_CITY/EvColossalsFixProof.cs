using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static partial class EvColossalsProof
{
    // Unlike Core2026Proof.Attack, this must never clear summoning exhaustion.
    private static void AttackWhenReady(Game g, int attackerId, int targetId)
    {
        var attacker = (Minion)g.IdEntityDic[attackerId];
        var task = attacker.Controller.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
            && t.Source?.Id == attackerId && t.Target?.Id == targetId);
        Core2026Proof.Check(task != null && g.Process(task), "fresh minion has legal attack: " + attacker.Card.Id);
    }

    [GroupProof("ev-colossals-rush-fix")]
    internal static void HydralodonRushRegression()
    {
        foreach (int seed in new[] { 1, 2, 3 }) foreach (bool second in new[] { false, true })
        {
            var g = Fresh(seed); if (second) End(g); var p = g.CurrentPlayer;
            var body = (Minion)Play(g, p, "TSC_950");
            var heads = ColossalRules.AppendagesOf(body).ToArray();
            var enemy = Summon(p.Opponent, "CS2_200"); enemy.BaseHealth = 100; enemy.AttackDamage = 0;
            foreach (var head in heads)
            {
                Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                    && t.Source?.Id == head.Id && t.Target?.Id == p.Opponent.Hero.Id), "fresh Head cannot Rush face");
                Resume(g, next => AttackWhenReady(next, head.Id, enemy.Id));
            }
            Core2026Proof.Check(enemy.Damage == 6 && heads.All(h => h.NumAttacksThisTurn == 1), "both fresh Heads actually attack same turn");
            Kill(g, heads[0]);
            var replacements = p.BoardZone.Where(Hydralodon.IsHead).Where(h => !heads.Contains(h)).ToArray();
            Core2026Proof.Check(replacements.Length == 2 && replacements.All(h => !h.IsRush)
                && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && replacements.Any(h => h.Id == t.Source?.Id)),
                "replacement Heads have no inherited Rush or immediate attack");
        }
        // Audit every body/limb, both entry paths and seats. No other grant bypasses the Effect path.
        var printedRush = new[] { "TSC_030", "TSC_030t2", "TSC_029t", "TSC_029t2", "TSC_937t", "TSC_937t3", "TID_712", "NX2_037t", "NX2_037t2" };
        foreach (string id in Ids) foreach (bool play in new[] { false, true }) foreach (bool second in new[] { false, true })
        {
            var g = Fresh(); if (second) End(g); var p = g.CurrentPlayer;
            var body = play ? (Minion)Play(g, p, id) : Summon(p, id);
            while (p.Choice != null) Answer(g);
            var minions = new[] { body }.Concat(ColossalRules.AppendagesOf(body)).ToArray();
            foreach (var minion in minions)
            {
                bool ready = printedRush.Contains(minion.Card.Id) || play && Hydralodon.IsHead(minion);
                if (!ready)
                {
                    Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == minion.Id),
                        "newborn without Rush/Charge cannot attack: " + minion.Card.Id);
                    continue;
                }
                var target = Summon(p.Opponent, "CS2_200"); target.BaseHealth = 100; target.AttackDamage = 0;
                int damage = minion.Card.Id == "TID_712" ? 8 : minion.AttackDamage + (id == "TSC_029" ? 1 : 0);
                Resume(g, next => AttackWhenReady(next, minion.Id, target.Id));
                Core2026Proof.Check(target.Damage == damage && minion.NumAttacksThisTurn == 1,
                    "actual same-turn attack result: " + minion.Card.Id);
                Kill(g, target);
                while (p.Choice != null) Answer(g);
            }
        }
        Console.WriteLine("PASS ev-colossals Rush readiness: actual attacks, play/summon, both seats, clone/snapshot");
    }

    [GroupProof("ev-colossals-dredge-fix")]
    internal static void Unpinned_DredgeModifiedDuplicates()
    {
        foreach (bool second in new[] { false, true })
        {
            var g = Fresh(); if (second) End(g); var p = g.CurrentPlayer; ClassDeck(p);
            for (int i = 0; i < 3; i++)
            {
                var original = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"));
                original.AttackDamage = 10 + i; original.BaseHealth = 12 + i; original.Cost = i;
                original.HasTaunt = i == 1;
                p.DeckZone.Add(original);
            }
            var originals = p.DeckZone.Select(c => c.Id).ToArray();
            var body = (Minion)Play(g, p, "TSC_030"); var target = Summon(p.Opponent, "CS2_200");
            Resume(g, next => AttackWhenReady(next, body.Id, target.Id));
            Core2026Proof.Check(p.Choice.Choices.Count == 3, "Dredge offers three duplicate entities");
            Privacy(g, p, p.Choice.Choices.Concat(originals));
            for (int i = 0; i < 3; i++)
            {
                var offer = (Minion)g.IdEntityDic[p.Choice.Choices[i]];
                Core2026Proof.Check(offer[DiscoverRealRules.BoundEntity] == originals[i] && offer.AttackDamage == 10 + i
                    && offer.BaseHealth == 12 + i && offer.Cost == i && offer.HasTaunt == (i == 1),
                    "Dredge display retains bound duplicate modifications: index=" + i);
            }
            Resume(g, next => Answer(next, 1));
            Core2026Proof.Check(p.DeckZone.Last().Id == originals[1], "Dredge moves selected modified original, not proxy");
            var claw = ColossalRules.AppendagesOf(body).Single(); target = Summon(p.Opponent, "CS2_200");
            Resume(g, next => AttackWhenReady(next, claw.Id, target.Id));
            var drawn = (Minion)p.HandZone.Last();
            Core2026Proof.Check(drawn.Id == originals[1] && drawn.AttackDamage == 11 && drawn.BaseHealth == 13 && drawn.Cost == 1 && drawn.HasTaunt,
                "draw preserves selected duplicate modifications");
        }
        Console.WriteLine("PASS ev-colossals modified duplicate Dredge display/binding/draw both seats, clone/snapshot");
    }
}
