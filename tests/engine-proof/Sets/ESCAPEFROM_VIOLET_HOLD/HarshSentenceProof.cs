using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HarshSentenceProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_404", (g, p, id) => {
        var own = Add(p, "CS2_182"); var enemy = Add(p.Opponent, "CS2_182"); var spell = Add(p.Opponent, "CS2_029");
        int deck = p.Opponent.DeckZone.Count; var sentence = Play(g, p, id);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == deck + 2 && p.Opponent.DeckZone.Count(c => c.Card.Id == "CAP_400t2t") == 2, "two exact Imp-formants shuffled into opponent deck");
        Core2026Proof.Check(enemy.Cost == 4 && own.Cost == 4, "no tax before next turn");
        var restored = Game.LoadSnapshot(g.SaveSnapshot()); End(restored);
        Core2026Proof.Check(restored.IdEntityDic[enemy.Id][GameTag.COST] == 6
            && restored.IdEntityDic[own.Id][GameTag.COST] == 4 && enemy.Cost == 4, "pending tax survives snapshot without mutating original");
        End(restored);
        Core2026Proof.Check(restored.IdEntityDic[enemy.Id][GameTag.COST] == 4, "restored pending tax expires after opponent turn");
        End(g); var drawn = Add(p.Opponent, "CS2_231"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.Cost == 6 && enemy[GameTag.COST] == 6 && drawn.Cost == 2 && spell.Cost == 4 && own.Cost == 4, $"next turn taxes existing/new enemy minions only: enemy={enemy.Cost}, drawn={drawn.Cost}, spell={spell.Cost}, own={own.Cost}, pending={sentence.ActivatedTrigger != null}, enchantments={p.AppliedEnchantments?.Count}, auras={g.Auras.Count}");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.IdEntityDic[enemy.Id][GameTag.COST] == 4 && enemy.Cost == 6, "expiry isolated in clone");
        End(g); Core2026Proof.Check(enemy.Cost == 4 && drawn.Cost == 0, "tax expires after opponent turn");
        Play(g, p, id); Play(g, p, id); End(g);
        Core2026Proof.Check(enemy.Cost == 8, "independent casts stack next turn"); End(g);
        Core2026Proof.Check(enemy.Cost == 4, "both casts expire");
    });

    [GroupProof("b14-integration-harsh-repeat")]
    internal static void Repeated()
    {
        var game = HeraldProof.New(CardClass.ROGUE); var p = game.Player1;
        var enemy = Add(p.Opponent, "CS2_182"); int deck = p.Opponent.DeckZone.Count;
        var twice = Add(p, "CAP_404"); twice[(GameTag)32100] = 1;
        Core2026Proof.Check(game.Process(PlayCardTask.Any(p, twice)), "repeated Sentence arms both delayed taxes");
        Core2026Proof.Check(p.Opponent.DeckZone.Count == deck + 4, "both Sentence bodies shuffle two Imps");
        foreach (var branch in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            End(branch);
            Core2026Proof.Check(branch.IdEntityDic[enemy.Id].Cost == 8, "both taxes survive clone/snapshot and stack");
            End(branch);
            Core2026Proof.Check(branch.IdEntityDic[enemy.Id].Cost == 4, "both repeated taxes expire");
        }
    }
}
