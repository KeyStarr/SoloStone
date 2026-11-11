using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class Batch14K7CataclysmProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_131", Treant);

    private static void Treant(Game g, Controller p, string id)
    {
        Play(g, p, "EX1_400");
        var held = Add(p, id);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "spending before hand entry excluded");
        Play(g, p, "EX1_400");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && !PoweredUp.IsMet(held), "one spent, three remain");
        End(g); Play(g, p.Opponent, "EX1_400"); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "opponent spending excluded, prior own turn retained");
        var baseline = g.CloneExact(history: true);
        foreach (int spent in new[] { 0, 2, 3, 4 })
        {
            var branch = baseline.CloneExact(history: true); var owner = branch.Player1;
            var treant = branch.IdEntityDic[held.Id];
            if (spent > 0)
            {
                var payment = Add(owner, "EX1_400"); payment.Cost = spent;
                PlayEntity(branch, owner, payment);
            }
            Core2026Proof.Check(treant[GameTag.TAG_SCRIPT_DATA_NUM_1] == Math.Max(0, 3 - spent)
                && PoweredUp.IsMet(treant) == (spent >= 3), "exact four-Mana threshold and visible remaining count");
            var copy = Generic.Copy(owner, owner.Hero, treant, Zone.HAND);
            branch.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "hand copy starts new residence");
            foreach (var next in new[] { branch, branch.CloneExact(history: true), Game.LoadSnapshot(branch.SaveSnapshot()) })
            {
                var player = next.Player1; var source = next.IdEntityDic[treant.Id];
                player.BaseMana = 5; player.UsedMana = 3; player.TemporaryMana = 0;
                Core2026Proof.Check(next.Process(PlayCardTask.Any(player, source)), "Treant play accepted at two remaining Mana");
                Core2026Proof.Check(player.RemainingMana == 1 && player.BaseMana == (spent >= 3 ? 6 : 5)
                    && player.TemporaryMana == (spent >= 3 ? 0 : 1),
                    "own payment excluded: unready gains temporary crystal, ready gains full permanent crystal");
                ((Minion)source).Silence(); Kill(next, (Minion)source); End(next);
                Core2026Proof.Check(player.BaseMana == (spent >= 3 ? 6 : 5) && next.Player2.TemporaryMana == 0,
                    "crystal never transfers to opponent; source removal does not remove permanent Mana");
                End(next);
                Core2026Proof.Check(player.TemporaryMana == 0 && player.BaseMana == (spent >= 3 ? 7 : 6),
                    "next own turn clears temporary Mana; permanent crystal plus normal turn growth remains");
            }
        }
        var health = Add(p, "CS2_029");
        CostRules.AddPaymentOverride((Playable)health, CostPaymentKind.Health);
        p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, health, p.Opponent.Hero)), "Health-paid spell accepted");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "Health payment does not count as Mana");
        p.UsedMana = 0;
        Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "holding hero power accepted");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "hero power Mana counts");
        var prepare = Add(p, "CS2_029"); PrepareRules.Grant(prepare); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p, prepare)) && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "Prepare Mana counts");
        HistoryCardProofChecks.HiddenDisplay(g, id);
    }
}
