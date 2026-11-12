using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

// Regression test for fuzz determinism. Card task recipes (StateTaskList) are shared by every game in a process, and
// Process() used to write its running/complete status onto that shared object. A game that ran later could then see
// different state and history than an identical earlier game, so replays diverged. The same game is played twice
// here and must produce identical state.
internal static class StateTaskListDeterminismProof
{
    internal static void Run()
    {
        // A fresh shared recipe catches the leak even when earlier proofs have warmed catalogue tasks.
        var definition = Cards.FromId("EX1_245").Clone();
        definition.Power = new Power { PowerTask = StateTaskList.Chain(
            new SilenceTask(EntityType.TARGET), new DamageTask(1, EntityType.TARGET, true)) };

        string Replay()
        {
            var game = HistoryFactsProof.Fixture();
            var player = game.Player1;
            Add(player, "MEND_805"); // Charity enrolls the same history consumer as the failing fuzz deck.
            var target = Summon(player, "CS2_179");
            var spell = Entity.FromCard(player, definition.Clone());
            player.HandZone.Add(spell);
            PlayEntity(game, player, spell, target);
            Core2026Proof.Check(target.IsSilenced && target.Damage == 1 && !target.HasTaunt,
                "shared chain still executes silence then damage");
            Core2026Proof.Check(game.HistoryFacts.HasConsumers && game.HistoryFacts.Query.Casts().Any(f => f.EntityId == spell.Id),
                "replay state includes the frozen spell definition");
            return Canonical.State(game);
        }

        string first = Replay();
        string repeat = Replay();
        Core2026Proof.Check(first == repeat, "shared StateTaskList recipe changes state between identical games");
        Console.WriteLine("PASS shared task recipe: identical games retain identical frozen history and state");
    }
}
