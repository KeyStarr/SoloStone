using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class SilenceValueProof
{
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            PoolPilotProof.EmptyHand(game.Player1);
            var champion = Summon(game.Player1, "CS2_222");
            var looter = Summon(game.Player1, "EX1_096");
            var fordragon = Summon(game.Player1, "CORE_SW_047");
            var recipient = (Minion)Add(game.Player1, "CS2_182");
            Core2026Proof.Check(!champion.IsSilenced, "missing SILENCED is false");
            champion.IsSilenced = false;
            looter[GameTag.SILENCED] = 0;
            fordragon[GameTag.SILENCED] = 0;
            foreach (var branch in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var aura = (Minion)branch.IdEntityDic[champion.Id];
                var deathrattle = (Minion)branch.IdEntityDic[looter.Id];
                var trigger = (Minion)branch.IdEntityDic[fordragon.Id];
                foreach (var minion in new[] { aura, deathrattle, trigger })
                {
                    Core2026Proof.Check(minion.NativeTags.TryGetValue(GameTag.SILENCED, out int value) && value == 0
                        && !minion.IsSilenced, "explicit SILENCED=0 stays unsilenced through clone/snapshot");
                    Core2026Proof.Check(PowerHistoryBuilder.FullEntity(minion).Entity.Tags[GameTag.SILENCED] == 0
                        && PowerHistoryBuilder.ShowEntity(minion).Entity.Tags[GameTag.SILENCED] == 0,
                        "full/show snapshots project zero Silence");
                }
                trigger.TakeDamage(branch.Player2.Hero, 1);
                branch.ProcessTasks();
                var handMinion = (Minion)branch.IdEntityDic[recipient.Id];
                Core2026Proof.Check(handMinion.AttackDamage == 9 && handMinion.Health == 10,
                    "zero Silence preserves Fordragon's shield-loss trigger");
                Core2026Proof.Check(deathrattle.HasDeathrattle && deathrattle.AttackDamage == 3 && deathrattle.BaseHealth == 2,
                    "zero Silence preserves Deathrattle and Champion aura");
                int hand = branch.Player1.HandZone.Count;
                Kill(branch, deathrattle);
                Core2026Proof.Check(branch.Player1.HandZone.Count == hand + 1, "zero Silence preserves Loot Hoarder draw");
                var copy = (Minion)Generic.Copy(branch.Player1, aura, aura, Zone.PLAY, preserveBoardMinionState: true);
                branch.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(!copy.IsSilenced && trigger.AttackDamage == 7, "zero Silence board copy keeps outgoing aura");
                aura.Silence(); copy.Silence();
                branch.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(aura.IsSilenced && aura[GameTag.SILENCED] == 1 && trigger.AttackDamage == 5,
                    "real Silence still removes outgoing auras and writes one");
                aura[GameTag.SILENCED] = 2;
                Core2026Proof.Check(aura.IsSilenced, "nonzero SILENCED is true");
                aura.Reset();
                Core2026Proof.Check(!aura.IsSilenced && !aura.NativeTags.ContainsKey(GameTag.SILENCED), "Reset removes Silence");
            }
            DormantCopy(history);
            MalignantCopy(history);
        }
        Console.WriteLine("PASS Silence values: explicit zero, trigger/deathrattle/aura, real Silence/reset, dormant/board copies, Malignant Horror generations; clone/snapshot; history on/off");
    }

    private static void DormantCopy(bool history)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var champion = Summon(game.Player1, "CS2_222");
        var recruit = Summon(game.Player1, "CS2_101t");
        Core2026Proof.Check(DormantRules.Make(champion, 2, "TIME_046e", game.Player1.Hero), "Champion sleeps");
        var stack = new TaskStack();
        new SummonCopyTask(EntityType.SOURCE, addToStack: true).Process(game, game.Player1, champion, null, stack);
        game.ProcessTasks();
        game.DeathProcessingAndAuraUpdate();
        var copy = (Minion)stack.Playables.Single();
        Core2026Proof.Check(copy.NativeTags.TryGetValue(GameTag.SILENCED, out int value) && value == 0,
            "unsilenced dormant SummonCopyTask legitimately writes SILENCED=0");
        foreach (var branch in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var sleeper = (Minion)branch.IdEntityDic[copy.Id];
            Core2026Proof.Check(DormantRules.Awaken(sleeper), "dormant copy awakens");
            branch.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!sleeper.IsSilenced && ((Minion)branch.IdEntityDic[recruit.Id]).AttackDamage == 2,
                "dormant copy's zero Silence allows aura restoration on wake");
        }
    }

    private static void MalignantCopy(bool history)
    {
        var game = DeathKnightC1Proof.NewGame(71).CloneExact(history);
        var source = Summon(game.Player1, "CORE_RLK_745");
        Core2026Proof.Check(DormantRules.Make(source, 1, "TIME_046e", game.Player1.Hero)
            && DormantRules.Awaken(source) && DormantRules.HasState(source)
            && !source.NativeTags.ContainsKey(GameTag.SILENCED), "awakened Horror has dormant state without a Silence tag");
        CorpseRules.Gain(game.Player1, 4);
        foreach (var branch in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            End(branch);
            var copy = branch.Player1.BoardZone.Single(m => m.Id != source.Id);
            Core2026Proof.Check(copy.NativeTags.TryGetValue(GameTag.SILENCED, out int value) && value == 0 && !copy.IsSilenced
                && copy[GameTag.REBORN] == 1 && CorpseRules.Available(branch.Player1) == 0,
                "Malignant Horror end-turn copy retains zero Silence and Reborn");
            Kill(branch, (Minion)branch.IdEntityDic[source.Id]);
            // Leave only the copy to prove its own end-turn trigger, independently of the original/reborn body.
            foreach (var other in branch.Player1.BoardZone.Where(m => m.Id != copy.Id).ToArray())
                branch.Player1.SetasideZone.Add(branch.Player1.BoardZone.Remove(other));
            CorpseRules.TrySpend(branch.Player1, CorpseRules.Available(branch.Player1));
            CorpseRules.Gain(branch.Player1, 4);
            End(branch); End(branch);
            Core2026Proof.Check(branch.Player1.BoardZone.Count == 2 && CorpseRules.Available(branch.Player1) == 0
                && branch.Player1.BoardZone.All(m => !m.IsSilenced && m[GameTag.REBORN] == 1),
                "zero-Silence Horror copy keeps trigger and creates next generation");
        }
    }
}
