using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using L = LocationProofs;

internal static class LocationBoardProof
{
    internal static BoardObject Object(Controller c, int position = -1)
    {
        var location = L.Location(c, position: position);
        return location;
    }
    internal static void Run()
    {
        foreach (int locations in new[] { 1, 2 })
        {
            var game = L.Game(); var board = game.Player1.BoardZone;
            for (int i = 0; i < 7 - locations; i++) Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: board);
            L.Check(!board.HasLocations && board.Slots.SequenceEqual(board.Cast<IPlayable>()), "legacy slots equal minion views");
            for (int i = 0; i < locations; i++) Object(game.Player1, i);
            L.Check(board.IsFull && board.FreeSpace == 0 && board.SlotCount == 7 && board.Count == 7 - locations, "mixed capacity");
            L.Check(board.GetAll().Length == 7 - locations && board.Count() == 7 - locations &&
                ((IZone)board).Count == 7 - locations && ((IZone)board).GetAll.Count == 7, "typed/interface minion count and physical entity view");
            for (int i = 0; i < 7; i++) L.Check(board.GetSlot(i).ZonePosition == i && board.GetSlot(i)[GameTag.ZONE_POSITION] == i + 1, "physical positions");
            var clone = game.CloneExact(true);
            L.Check(board.Hash() == clone.Player1.BoardZone.Hash(), "mixed clone hash");
            clone.Player1.BoardZone.GetLocations()[0][GameTag.DAMAGE]++;
            L.Check(board.Hash() != clone.Player1.BoardZone.Hash(), "location hash and clone independent");
        }
        PlacementAndViews();
        SummonCopyAndReborn();
        PinnedRule_LocationBlocksAdjacency();
        Console.WriteLine("PASS location board capacity/views/positions/clone; PinnedRule_LocationBlocksAdjacency");
    }
    private static void PlacementAndViews()
    {
        foreach (int position in new[] { 0, 1, 2 })
        {
            var game = L.Game(); var p = game.Player1; var board = p.BoardZone;
            var first = L.Minion(p); var second = L.Minion(p);
            var source = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.HandZone);
            var choices = p.Options().OfType<PlayCardTask>().Where(t => t.Source == source).ToArray();
            L.Check(choices.Select(t => t.ZonePosition).SequenceEqual(new[] { 0, 1, 2 }), "location hand placement choices");
            int plays = p.NumCardsPlayedThisTurn, mana = p.RemainingMana, minions = p.NumMinionsPlayedThisTurn;
            L.Check(game.Process(choices.Single(t => t.ZonePosition == position)), "play selected physical gap");
            L.Check(board.GetSlot(position) == source && p.NumCardsPlayedThisTurn == plays + 1 && p.RemainingMana == mana - 1 && p.NumMinionsPlayedThisTurn == minions, "location payment/card count without minion play");
            var handMinion = Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.HandZone);
            L.Check(p.Options().OfType<PlayCardTask>().Where(t => t.Source == handMinion).Select(t => t.ZonePosition).SequenceEqual(new[] { 0, 1, 2, 3 }), "minion placements include every physical gap");
            L.Check(board.GetSpan().Length == 2 && board.MinionCount == 2 && board.Any(m => m == first) && board.CountOf(_ => true) == 2, "minion span/predicates");
            var visited = new List<Minion>(); board.ForEach(visited.Add);
            L.Check(visited.SequenceEqual(new[] { first, second }) && Enumerable.Range(0, 20).All(_ => board.Random is Minion), "minion traversal/random");
            var stack = new TaskStack();
            new CountTask(Zone.PLAY, false, false).Process(game, p, source, null, stack);
            L.Check(stack.Number == 2 && SelfCondition.IsZoneCount(Zone.PLAY, 2).Eval(source), "generic PLAY counts minions");
            L.Check(new EffectContext(game, p.Id, source.Id).FriendlyLocationIds.SequenceEqual(new[] { source.Id }), "friendly location ids");
            var spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.HandZone);
            L.Check(!spell.ValidPlayTargets.Cast<IPlayable>().Contains(source), "character targeting excludes locations");
            new DestroyTask(EntityType.ALLMINIONS).Process(game, p, p.Hero, null);
            game.DeathProcessingAndAuraUpdate();
            L.Check(board.SlotCount == 1 && board.GetSlot(0) == source, "all-minions destroy leaves location");
        }
        {
            var game = L.Game(); var p = game.Player1; var location = L.Location(p);
            for (int i = 0; i < 6; i++) L.Minion(p);
            p.BoardZone[0][GameTag.DORMANT] = 1;
            L.Check(p.BoardZone.IsFull && p.BoardZone.Count == 6 && p.BoardZone.GetAll().Length == 5 && p.BoardZone.GetSpan().Length == 6, "dormant consumes physical slot; legacy filtering preserved");
            L.Check(!LocationRules.CanActivate(p, location, p.BoardZone[0]), "dormant not activation target");
            var hand = Entity.FromCard(p, Cards.FromId("JAIL_877"), zone: p.HandZone);
            L.Check(!p.Options().Any(t => t.Source == hand), "full board excludes location hand play");
        }
    }
    private static void SummonCopyAndReborn()
    {
        var game = L.Game(); var p = game.Player1;
        L.Location(p); var source = L.Minion(p); L.Location(p, "JAIL_877");
        int alternate = 0;
        L.Check(SummonTask.GetPosition(source, SummonSide.NUMBER, 99, ref alternate) == 3, "summon clamp uses physical count");
        var stack = new TaskStack { Number = 99, Playables = new List<IPlayable>() };
        new SummonTask(SummonSide.NUMBER, Cards.FromId("CS2_231"), addToStack: true).Process(game, p, source, null, stack);
        L.Check(stack.Playables.Single().ZonePosition == 3, "summon destination clamp includes location");
        source.LastBoardPosition = 99;
        var copy = Generic.Copy(p, source, source, Zone.PLAY, deathrattle: true);
        L.Check(copy.ZonePosition == 4, "deathrattle copy clamp uses physical count");
        var reborn = L.Minion(p, position: 5); reborn[GameTag.REBORN] = 1;
        int original = reborn.Id;
        reborn.Destroy(); game.DeathProcessingAndAuraUpdate();
        var restored = p.BoardZone.GetSlot(5) as Minion;
        L.Check(restored != null && restored.Id != original && restored.Health == 1 && restored[GameTag.HAS_BEEN_REBORN] == 1, "Reborn preserves physical position after locations");
        new SummonCopyTask(EntityType.TARGET).Process(game, p, source, source);
        L.Check(p.BoardZone.SlotCount == 7, "SummonCopy respects remaining physical space");
    }
    private static void PinnedRule_LocationBlocksAdjacency()
    {
        var game = L.Game(); var board = game.Player1.BoardZone;
        var left = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: board);
        var wolf = (Minion)Entity.FromCard(game.Player1, Cards.FromId("EX1_162"), zone: board);
        var right = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: board);
        game.AuraUpdate();
        L.Check(left.AttackDamage == 2 && right.AttackDamage == 2, "adjacency fixture");
        var location = Object(game.Player1, 1); game.AuraUpdate();
        L.Check(wolf.GetAdjacentMinions().SequenceEqual(new[] { right }) && left.AttackDamage == 1 && right.AttackDamage == 2, "location blocks left adjacency");
        board.Remove(location); game.AuraUpdate();
        L.Check(!board.HasLocations && wolf.GetAdjacentMinions().Length == 2 && left.AttackDamage == 2, "last location restores legacy path");
    }
}
