using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class AttackOptionsProof
{
    // Work on independent continuations so callers can keep testing their original fixture.
    internal static void Lifecycle(Game game, Minion source, Minion enemy, bool rush, bool keyword = true,
        Action<Game, Minion> grantOnPreviousTurnMinion = null)
    {
        foreach (bool older in new[] { false, true })
        foreach (bool grantOlder in older && grantOnPreviousTurnMinion != null ? new[] { false, true } : new[] { false })
        foreach (bool silence in new[] { false, true })
        foreach (bool snapshot in new[] { false, true })
        {
            var resumed = snapshot ? Game.LoadSnapshot(game.SaveSnapshot()) : game.CloneExact(history: true);
            var minion = (Minion)resumed.IdEntityDic[source.Id];
            var target = (Minion)resumed.IdEntityDic[enemy.Id];
            if (older)
            {
                End(resumed); End(resumed);
                if (grantOlder) grantOnPreviousTurnMinion(resumed, minion);
            }
            string label = source.Card.Id + (older ? " previous turn" : " same turn") + (snapshot ? " snapshot" : " clone") + (grantOlder ? " regrant" : " retained");
            Core2026Proof.Check(minion.IsRush == (keyword && rush) && minion.HasCharge == (keyword && !rush), label + " keyword");
            Core2026Proof.Check(CanAttack(minion, target) == (older || keyword)
                && CanAttack(minion, minion.Controller.Opponent.Hero) == (older || (keyword && !rush)), label + " legal targets before Silence");
            if (silence)
            {
                Play(resumed, minion.Controller, "BE_228", minion);
                Core2026Proof.Check(minion.IsSilenced && !minion.IsRush && !minion.HasCharge && !minion.AttackableByRush,
                    label + " Silence removes attack keywords");
                Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK && minion.Health == minion.Card.Health,
                    label + " Silence restores printed stats");
            }
            var attacks = minion.Controller.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == minion.Id).ToArray();
            bool minions = older || (!silence && keyword);
            bool hero = older || (!silence && keyword && !rush);
            Core2026Proof.Check(CanAttack(minion, target) == minions && CanAttack(minion, minion.Controller.Opponent.Hero) == hero,
                label + " legal targets after continuation");
            Core2026Proof.Check(hero || attacks.All(t => t.Target is Minion), label + " no hero attack option");
            Core2026Proof.Check(minions || attacks.Length == 0, label + " no attack options while summoning sick");
        }
    }
}
