using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Issue #85: Boon of Speed (TIME_619t5) gives Bwonsamdi (TIME_619t) Rush through a counting (ADD) aura. An added Rush must
// ready him the turn he is played, as a printed or SET Rush does: he may attack a minion, not the enemy hero.
internal static class BoonOfSpeedRushProof
{
    [GroupProof("boon-of-speed-rush", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (bool boonFirst in new[] { true, false })
        {
            var game = DeathKnightC1Proof.NewGame(7); var p = game.Player1;
            // Two enemies: the attack kills one, so "no second attack" is tested with a legal minion target left.
            var enemy = Core2026Proof.Summon(p.Opponent, "CS2_182");
            Core2026Proof.Summon(p.Opponent, "CS2_182");
            var bwonsamdi = (Minion)Core2026Proof.Add(p, "TIME_619t");
            if (boonFirst) Speed(game, p);
            Core2026Proof.PlayEntity(game, p, bwonsamdi);
            if (!boonFirst) Speed(game, p);
            Core2026Proof.Check(bwonsamdi.IsRush && !bwonsamdi.HasCharge, "Boon of Speed gives Bwonsamdi Rush, not Charge");
            string when = boonFirst ? "" : " (boon gained after he entered play)";
            foreach (var branch in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
            {
                var owner = branch.Player1; var body = (Minion)branch.IdEntityDic[bwonsamdi.Id];
                var attacks = Attacks(owner, body);
                Core2026Proof.Check(attacks.Any(o => o.Target?.Id == enemy.Id), "Rush Bwonsamdi may attack a minion the turn he is played" + when);
                Core2026Proof.Check(attacks.All(o => o.Target?.Id != owner.Opponent.Hero.Id), "Rush Bwonsamdi may not attack the hero that turn" + when);
                var target = (Minion)branch.IdEntityDic[enemy.Id];
                Core2026Proof.Check(branch.Process(attacks.First(o => o.Target?.Id == enemy.Id)), "attack accepted");
                Core2026Proof.Check(target.IsDead || target.Zone != owner.Opponent.BoardZone, "the attack kills the damaged minion");
                Core2026Proof.Check(body.IsExhausted && Attacks(owner, body).Length == 0 && owner.Opponent.BoardZone.Count == 1,
                    "one attack only, with an enemy minion still there");
                Core2026Proof.End(branch); Core2026Proof.End(branch);
                Core2026Proof.Check(Attacks(owner, body).Any(o => o.Target?.Id == owner.Opponent.Hero.Id) && body.AttackableByRush == false,
                    "next turn the Rush restriction is gone and he may attack the hero" + when);
            }
        }
        // Taunt and Lifesteal boons give no readiness.
        foreach (string option in new[] { "TIME_619t3", "TIME_619t4" })
        {
            var game = DeathKnightC1Proof.NewGame(7); var p = game.Player1;
            Core2026Proof.Summon(p.Opponent, "CS2_182");
            Boon(game, p, option);
            var bwonsamdi = (Minion)Core2026Proof.Add(p, "TIME_619t");
            Core2026Proof.PlayEntity(game, p, bwonsamdi);
            Core2026Proof.Check(!bwonsamdi.IsRush && !bwonsamdi.CanAttack, option + " leaves Bwonsamdi with summoning sickness");
        }
    }

    private static void Speed(Game game, Controller p) => Boon(game, p, "TIME_619t5");

    private static PlayerTask[] Attacks(Controller p, Minion minion) =>
        p.Options().Where(o => o.PlayerTaskType == PlayerTaskType.MINION_ATTACK && o.Source?.Id == minion.Id).ToArray();

    // What Befell Zandalar (TIME_619t2): deal 2 to all enemies, then choose a Boon.
    private static void Boon(Game game, Controller p, string option)
    {
        Core2026Proof.Play(game, p, "TIME_619t2");
        var decision = game.GetPendingDecisions().Single();
        int entity = p.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == option);
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { entity }), out var error), "boon answer: " + error);
        Core2026Proof.Check(p.AppliedEnchantments.Any(e => e.Card.Id == "TIME_619e" + (option == "TIME_619t3" ? "" : option == "TIME_619t4" ? "2" : "3")),
            "boon applied to the player");
    }
}
