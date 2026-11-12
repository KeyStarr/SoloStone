using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PressurePlateProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_152", (g, p, id) => {

        var secret = (Spell)Play(g, p, id); Core2026Proof.Check(secret.IsSecret && secret.Zone == p.SecretZone, "printed Secret installed");
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, "CS2_008", enemy);
        Core2026Proof.Check(secret.Zone == p.SecretZone && enemy.Zone == p.Opponent.BoardZone, "friendly spell cannot trigger");
        End(g); Play(g, p.Opponent, "CS2_182"); Core2026Proof.Check(secret.Zone == p.SecretZone, "enemy minion play cannot trigger");
        ClearBoard(g, p.Opponent); Play(g, p.Opponent, "CS2_008", p.Hero);
        Core2026Proof.Check(secret.Zone == p.SecretZone, "no enemy minion after resolution leaves secret armed");
        enemy = Summon(p.Opponent, "CS2_171"); Play(g, p.Opponent, "CS2_008", enemy);
        Core2026Proof.Check(secret.Zone == p.SecretZone && enemy.Zone == p.Opponent.GraveyardZone, "spell killing last enemy leaves secret armed");
        var friendly = Summon(p, "CS2_182");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player2, "EX1_345");
        Core2026Proof.Check(secret.Zone == p.SecretZone && p.Opponent.BoardZone.Count == 0, "clone secret isolated");
        Play(g, p.Opponent, "EX1_345");
        Core2026Proof.Check(secret.Zone == p.GraveyardZone && p.Opponent.BoardZone.Count == 0 && friendly.Zone == p.BoardZone, "post-spell summon is destroyed; friendly board untouched");
        Core2026Proof.Check(((Spell)clone.IdEntityDic[secret.Id]).Zone == clone.Player1.GraveyardZone, "clone also triggers after spell-created minion");
        End(g); secret = (Spell)Play(g, p, id); End(g);
        var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_200");
        clone = g.CloneExact(history: true); Play(clone, clone.Player2, "CS2_008", clone.Player1.Hero);
        Play(g, p.Opponent, "CS2_008", p.Hero);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && secret.Zone == p.GraveyardZone && first.Zone.Type == clone.IdEntityDic[first.Id].Zone.Type && second.Zone.Type == clone.IdEntityDic[second.Id].Zone.Type, "one random enemy destroyed with cloned RNG parity");
        Core2026Proof.Check(secret.ActivatedTrigger == null, "spent secret unregisters trigger");
        foreach (bool secretFirst in new[] { true, false })
        foreach (bool lethalPyromancer in new[] { false, true })
        {
            if (g.CurrentPlayer != p) End(g);
            ClearBoard(g, p); ClearBoard(g, p.Opponent);
            Minion pyromancer;
            if (!secretFirst)
            {
                End(g); pyromancer = (Minion)Play(g, p.Opponent, "NEW1_020"); End(g);
                secret = (Spell)Play(g, p, id); End(g);
            }
            else
            {
                secret = (Spell)Play(g, p, id); End(g);
                pyromancer = (Minion)Play(g, p.Opponent, "NEW1_020");
            }
            Core2026Proof.Check((secret.OrderOfPlay < pyromancer.OrderOfPlay) == secretFirst, "competing AFTER_CAST fixtures use both play orders");
            friendly = Summon(p, "CS2_182");
            if (lethalPyromancer) pyromancer.Damage = 1;
            Play(g, p.Opponent, "CS2_008", p.Hero);
            bool shouldSpend = secretFirst || !lethalPyromancer;
            Core2026Proof.Check(secret.Zone.Type == (shouldSpend ? Zone.GRAVEYARD : Zone.SECRET)
                && pyromancer.Zone == p.Opponent.GraveyardZone && friendly.Damage == 1,
                $"AFTER_CAST play order: secretFirst={secretFirst}, lethalPyromancer={lethalPyromancer}; pending destruction still allows queued Pyromancer damage");
            if (!shouldSpend)
            {
                enemy = Summon(p.Opponent, "CS2_182");
                Play(g, p.Opponent, "CS2_008", p.Hero);
                Core2026Proof.Check(secret.Zone == p.GraveyardZone && enemy.Zone == p.Opponent.GraveyardZone,
                    "Pyromancer killing last eligible target leaves secret armed for next spell");
            }
        }
    });
}
