using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class IllidariInquisitorProof
{
    internal static void Run() => StandardSetsProof.Case("CS3_020", Unpinned_IllidariInquisitor);
    private static void Unpinned_IllidariInquisitor(Game g, Controller p, string id)
    {
        var body = Summon(p, id); body.BaseHealth = 40; var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 40;
        Play(g, p, "CS2_106");
        PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, (ICharacter)next.IdEntityDic[enemy.Id])), "hero attack accepted"));
        Core2026Proof.Check(enemy.Damage == 11 && body.Damage == 4 && body.NumAttacksThisTurn == 0 && !body.IsExhausted,
            "PinnedRule_InquisitorBudget: follows hero and preserves Rush attack");
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, body, enemy)) && body.NumAttacksThisTurn == 1, "normal Rush attack still available");
        body.IsFrozen = true; p.Hero.NumAttacksThisTurn = 0; p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "second hero attack accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 11 && body.NumAttacksThisTurn == 1 && body.IsFrozen,
             $"Unpinned_InquisitorFollowupValidity: Frozen/spent Inquisitor attacks same hero (damage={p.Opponent.Hero.Damage}, attacks={body.NumAttacksThisTurn}, frozen={body.IsFrozen})");
        ForcedCardProof.Clean(g); body = Summon(p, id); enemy = Summon(p.Opponent, "EX1_556");
        Play(g, p, "CS2_106"); p.Hero.NumAttacksThisTurn = 0; p.Hero.IsExhausted = false;
        int cursor = g.HistoryFacts.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, enemy)), "lethal hero attack");
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksStarted().Count(f => f.Sequence > cursor) == 1
            && p.Opponent.BoardZone.Single().Card.Id == "skele21" && p.Opponent.BoardZone.Single().Damage == 0,
            "Unpinned_InquisitorFollowupValidity: dead original never replaced by Deathrattle summon");
        body.Silence(); p.Hero.NumAttacksThisTurn = 0; p.Hero.IsExhausted = false;
        int damage = p.Opponent.Hero.Damage;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.Opponent.Hero.Damage == damage + 3,
            "Silence removes follow-up");
    }
}
