using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using L = LocationProofs;

internal static class LocationSnapshotProof
{
    internal static void Run()
    {
        foreach (string stage in new[] { "ready", "closed", "ajar", "one-charge-left" })
        {
            var game = L.Game(); var location = L.Location(game.Player1); var target = L.Minion(game.Player1); target.BaseHealth = 20;
            game.DeathProcessingAndAuraUpdate();
            if (stage != "ready") L.Use(location, target);
            if (stage == "ajar" || stage == "one-charge-left") { L.End(game); L.End(game); }
            if (stage == "one-charge-left") { L.End(game); L.End(game); L.Use(location, target); L.End(game); L.End(game); L.End(game); L.End(game); }
            if (stage == "ajar") L.Check(location.IsExhausted && location[GameTag.EXHAUSTED] == 1 && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "snapshot fixture ajar on skipped owner turn");
            location.Location.Stage = 2;
            location.Location.ConditionLocked = true;
            var restored = SnapshotProof.RoundTrip(game); var clone = game.CloneExact(true);
            foreach (var copy in new[] { restored, clone })
            {
                var other = (BoardObject)copy.IdEntityDic[location.Id];
                L.Check(other[GameTag.HEALTH] == 3 && other[GameTag.DAMAGE] == location[GameTag.DAMAGE] &&
                    other.Durability == other[GameTag.HEALTH] - other[GameTag.DAMAGE], stage + " HEALTH/DAMAGE preserved");
                L.Check(other != location && other.Location != location.Location && other.Durability == location.Durability && other.Location.UsesCompleted == location.Location.UsesCompleted && other.Location.Stage == 2 && other.Location.ConditionLocked && copy.Player1.BoardZone.Hash() == game.Player1.BoardZone.Hash(), stage + " clone/snapshot equal and independent");
                other.Location.Stage = 7; L.Check(location.Location.Stage == 2, "mutable location state independent"); other.Location.Stage = 2;
                other[GameTag.DAMAGE]++;
                L.Check(other[GameTag.DAMAGE] == location[GameTag.DAMAGE] + 1, "spent durability independent");
                other[GameTag.DAMAGE]--;
            }
            foreach (var instance in new[] { game, restored, clone })
            {
                var source = (BoardObject)instance.IdEntityDic[location.Id];
                source.Location.ConditionLocked = false;
                while (instance.CurrentPlayer != instance.Player1 || source.IsExhausted) L.End(instance);
                L.Use(source, instance.IdEntityDic[target.Id]);
                L.End(instance); L.End(instance);
            }
            foreach (var copy in new[] { restored, clone })
                L.Check(copy.Player1.BoardZone.Hash() == game.Player1.BoardZone.Hash() && copy.Player1.GraveyardZone.Hash() == game.Player1.GraveyardZone.Hash() && Canonical.Value(copy.PowerHistory.Last) == Canonical.Value(game.PowerHistory.Last), stage + " resumed activation/cooldown identical");
        }
        Console.WriteLine("PASS location Clone/SaveSnapshot/LoadSnapshot ready, closed, ajar, one-charge-left and identical futures");
    }
}
