using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ForestLordCenariusDecisionProof
{
    internal static void Run()
    {
        // Tier D: pinned 37.0.0.254477 cards.json.gz EDR_209 has Choose Thrice, no Battlecry tag.
        // Lend its recipe to a real Battlecry for replay coverage; native Cenarius stays three choices.
        var carrier = Cards.FromId("EX1_319");
        var original = carrier.Power;
        try
        {
            carrier.Power = Cards.FromId("EDR_209").Power;
            Replay();
            Completion("EX1_319", 6);
            Completion("EDR_209", 3);
        }
        finally { carrier.Power = original; }
        Console.WriteLine("PASS Cenarius decision completion: borrowed Shudderwock/Brann recipe; native three prompts; deferred summon/after-play; replay clone/snapshot");
    }

    private static void Replay()
    {
        var replay = PlayerDecisionProof.Fixture(17);
        var p = replay.Player1;
        var wisp = Summon(p, "CS2_231");
        p.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId("EX1_319")));
        PoolPilotProof.Resume(replay, next => {
            Play(next, next.Player1, "GIL_820");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && !Generic.HasDecisionPlay(next.Player1),
                "Cenarius Shudderwock replay completes all choices synchronously");
            int buffs = next.Player1.BoardZone.Single(m => m.Id == wisp.Id).AppliedEnchantments?.Count(e => e.Card.Id == "EDR_209e2") ?? 0;
            int summons = next.Player1.BoardZone.Count(m => m.Card.Id == "EDR_209t5");
            Core2026Proof.Check(buffs + summons == 3 && next.Player1.SetasideZone.Count(c => c.Card.Id is "EDR_209a" or "EDR_209b") == 6,
                "Shudderwock resolves exactly three Cenarius branches");
            End(next);
        });
    }

    private static void Completion(string id, int prompts)
    {
        var game = PlayerDecisionProof.Fixture(19, brann: true);
        var p = game.Player1;
        var juggler = Summon(p, "NEW1_019");
        End(game); Play(game, p.Opponent, "EX1_294"); End(game);
        int health = p.Opponent.Hero.Health;
        var source = Play(game, p, id);
        for (int step = 0; step < prompts; step++)
        {
            Core2026Proof.Check(p.Choice != null && Generic.HasDecisionPlay(p) && p.Opponent.Hero.Health == health
                && p.Opponent.BoardZone.Count == 0,
                "Cenarius waits for every choice before summon and after-play triggers");
            int chosen = p.Choice.Choices.Single(i => game.IdEntityDic[i].Card.Id == "EDR_209a");
            CoreCostProof.Answer(game, chosen);
        }
        Core2026Proof.Check(p.Choice == null && !game.Resolution.HasSuspendedEffect && !Generic.HasDecisionPlay(p)
            && juggler.AttackDamage == 2 + prompts && juggler.Health == 2 + 3 * prompts
            && p.Opponent.Hero.Health + p.Opponent.BoardZone.Sum(m => m.Health) == health + source.Card.Health - 1
            && p.Opponent.BoardZone.Single().Card.Id == id
            && ((Minion)source).AttackDamage == source.Card.ATK && ((Minion)source).Health == source.Card.Health,
            "Cenarius resolves every buff, excludes source, then fires Juggler and Mirror Entity once");
        End(game);
    }
}
