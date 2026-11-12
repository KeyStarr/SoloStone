using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ChooseOneProof
{
    internal static IPlayable PlayOption(Game game, Controller player, string id, int branch, ICharacter target = null)
    {
        var source = Add(player, id);
        PlayOption(game, player, source, branch, target);
        return source;
    }
    internal static void PlayOption(Game game, Controller player, IPlayable source, int branch, ICharacter target = null)
    {
        player.BaseMana = 10; player.UsedMana = 0;
        var option = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && t.Source?.Id == source.Id && t.ChooseOne == branch && t.Target?.Id == target?.Id);
        Core2026Proof.Check(option != null && game.Process(option), "atomic branch/target accepted: " + source.Card.Id);
    }
    internal static void OptionsSurvive(Game game, Controller player, string id, string a, string b)
    {
        var source = Add(player, id);
        void Verify(Game copy)
        {
            var card = copy.IdEntityDic[source.Id];
            Core2026Proof.Check(card.ChooseOnePlayables.Select(p => p.Card.Id).SequenceEqual(new[] { a, b }), "exact option identities: " + id);
            Core2026Proof.Check(card.ChooseOnePlayables.All(p => ReferenceEquals(copy.IdEntityDic[p.Id], p)
                && p[GameTag.CREATOR] == source.Id && p[GameTag.PARENT_CARD] == source.Id), "restored branch ownership: " + id);
            var originalOptions = player.Options().Where(t => t.Source?.Id == source.Id)
                .Select(t => (t.ChooseOne, Target: t.Target?.Id, Position: (t as PlayCardTask)?.ZonePosition)).ToArray();
            var copiedOptions = copy.Player1.Options().Where(t => t.Source?.Id == source.Id)
                .Select(t => (t.ChooseOne, Target: t.Target?.Id, Position: (t as PlayCardTask)?.ZonePosition)).ToArray();
            Core2026Proof.Check(originalOptions.SequenceEqual(copiedOptions), "playable branches survive clone/snapshot: " + id);
        }
        Verify(game);
        var clone = game.CloneExact(history: true); var snapshot = Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools);
        Verify(clone); Verify(snapshot);
        foreach (var copy in new[] { clone, snapshot })
        {
            var option = copy.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
            Core2026Proof.Check(copy.Process(option), "restored option executes: " + id);
        }
        Core2026Proof.Check(Canonical.State(clone) == Canonical.State(snapshot) && source.Zone == player.HandZone,
            "clone/snapshot execute identically without changing source: " + id);
        var restored = game.CloneExact(history: true);
        var previous = restored.IdEntityDic[source.Id]; restored.Player1.HandZone.Remove(previous);
        Entity.FromCard(restored.Player1, Cards.FromId(id), previous.ToDictionary(pair => pair.Key, pair => pair.Value),
            zone: restored.Player1.HandZone, id: source.Id);
        Verify(restored);
        if (a.EndsWith("A", StringComparison.Ordinal))
        {
            bool missing = false;
            try { Cards.FromId(id + "a"); } catch (KeyNotFoundException) { missing = true; }
            Core2026Proof.Check(missing, "card dictionary remains case-sensitive: " + id);
        }
        var changed = Generic.ChangeEntityBlock(player, Add(player, "GAME_005"), Cards.FromId(id), false);
        Core2026Proof.Check(changed.ChooseOnePlayables.Select(p => p.Card.Id).SequenceEqual(new[] { a, b }), "transformation option identities: " + id);
        player.HandZone.Remove(changed); player.SetasideZone.Add(changed);
        player.HandZone.Remove(source); player.SetasideZone.Add(source);
    }
    internal static Minion Both(Controller player)
    {
        var fandral = Summon(player, "OG_044");
        Core2026Proof.Check(player.ChooseBoth, "registered Fandral enables Choose Both");
        return fandral;
    }
}
