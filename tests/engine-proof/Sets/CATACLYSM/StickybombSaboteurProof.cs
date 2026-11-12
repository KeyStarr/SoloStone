using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class StickybombSaboteurProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_186", (g, p, id) => {
        Definition("CATA_186t"); Definition("CATA_186te");
        Core2026Proof.Check(Cards.FromId("CATA_186t").Cost == 2 && Cards.FromId("CATA_186t")[(GameTag)401] == 1, "2-Cost EVIL_GLOW token");
        var silent = HeroDivineShieldProof.NewGame(false); var silentOwner = silent.Player1;
        EmptyHand(silentOwner);
        var silentLeft = Add(silentOwner, "CS2_182"); var silentToken = Add(silentOwner, "CATA_186t");
        Visible(silentLeft, 5); Actors(silentLeft, silentToken.Id);
        var silentCopy = Game.LoadSnapshot(silent.SaveSnapshot());
        silentCopy.Player1.SetasideZone.Add(silentCopy.Player1.HandZone.Remove(silentCopy.IdEntityDic[silentToken.Id]));
        Visible(silentCopy.IdEntityDic[silentLeft.Id], 4); Actors(silentCopy.IdEntityDic[silentLeft.Id]);
        Visible(silentLeft, 5); Actors(silentLeft, silentToken.Id);
        foreach (bool adjacent in new[] { false, true }) {
            var copyGame = HeroDivineShieldProof.NewGame(true); var owner = copyGame.Player1;
            EmptyHand(owner);
            var original = Add(owner, "CS2_182"); var sabotage = Add(owner, "CATA_186t");
            Visible(original, 5); Actors(original, sabotage.Id);
            if (!adjacent) Add(owner, "CS2_120");
            var copied = Generic.Copy(owner, owner.Hero, original, Zone.HAND); copyGame.AuraUpdate();
            Visible(copied, adjacent ? 5 : 4);
            Actors(copied, adjacent ? new[] { sabotage.Id } : Array.Empty<int>());
            var copyActors = copied.AppliedEnchantments?.Where(e => e.Card.Id == "CATA_186te").ToArray() ?? Array.Empty<Enchantment>();
            owner.SetasideZone.Add(owner.HandZone.Remove(sabotage));
            Visible(original, 4); Actors(original); Visible(copied, 4); Actors(copied);
            foreach (var presentation in copyActors) Retired(presentation);
        }
        var q = p.Opponent;
        var left = Add(q, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 4, "4/4 Saboteur");
        var token = q.HandZone.Single(c => c.Card.Id == "CATA_186t");
        Core2026Proof.Check(token.Controller == q && token[GameTag.DISPLAYED_CREATOR] == source.Id, "opponent token creator");
        Visible(token, 2); Visible(left, 5);
        var right = Add(q, "CS2_182"); Visible(right, 5);
        var initial = Actors(left, token.Id).Single(); Actors(right, token.Id);
        Core2026Proof.Check(g.PowerHistory.Full.OfType<PowerHistoryShowEntity>().Any(e => e.Entity.Id == initial.Id
            && e.Entity.Name == "CATA_186te" && e.Entity.Tags[GameTag.ATTACHED] == left.Id), "presentation SHOW_ENTITY attaches to neighbor");
        g.AuraUpdate(); g.AuraUpdate();
        Core2026Proof.Check(Actors(left, token.Id).Single().Id == initial.Id, "refresh retains one actor and one tax");
        q.HandZone.Swap(token, left); Visible(left, 5); Visible(right, 4); Actors(right);
        q.HandZone.Swap(token, right); Visible(left, 5); Visible(right, 4); Actors(left, token.Id);
        q.HandZone.Swap(token, left); Visible(left, 5); Visible(right, 5);
        var old = Actors(left, token.Id).Single();
        left = Generic.ChangeEntityBlock(q, left, Cards.FromId("CS2_120"), true); g.AuraUpdate();
        Visible(left, 3); Actors(left, token.Id); Retired(old);
        var actor = Actors(left, token.Id).Single();
        var quiet = g.CloneExact(false);
        foreach (var restored in new[] { quiet, g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var owner = restored.Player2; var restoredLeft = restored.IdEntityDic[left.Id];
            var restoredActor = Actors(restoredLeft, token.Id).Single();
            Core2026Proof.Check(restoredActor.Id == actor.Id && !ReferenceEquals(actor, restoredActor), "independent clone/snapshot actor");
            owner.SetasideZone.Add(owner.HandZone.Remove(restored.IdEntityDic[token.Id]));
            Visible(restoredLeft, 2); Actors(restoredLeft); Retired(restoredActor, !ReferenceEquals(restored, quiet));
            Visible(left, 3); Actors(left, token.Id);
        }
        End(g); q.BaseMana = 10; q.UsedMana = 0;
        Core2026Proof.Check(q.Options().Any(t => t.Source?.Id == token.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "EVIL_GLOW token is playable");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(q, token)) && q.UsedMana == 2, "playing Sabotage pays two Mana");
        Visible(left, 2); Visible(right, 4); Actors(left); Actors(right); Retired(actor);
        EmptyHand(q);
        var first = Add(q, "CATA_186t"); var middle = Add(q, "CS2_182"); var second = Add(q, "CATA_186t");
        Visible(middle, 6); Actors(middle, first.Id, second.Id);
        q.SetasideZone.Add(q.HandZone.Remove(first)); Visible(middle, 5); Actors(middle, second.Id);
        var remaining = Actors(middle, second.Id).Single();
        second = Generic.ChangeEntityBlock(q, second, Cards.FromId("CS2_008"), true); g.AuraUpdate();
        Visible(middle, 4); Actors(middle); Retired(remaining);
        var transformed = Generic.ChangeEntityBlock(q, second, Cards.FromId("CATA_186t"), true); g.AuraUpdate();
        Visible(middle, 5); Actors(middle, transformed.Id);
        EmptyHand(q); FillHand(q); var grave = q.GraveyardZone.Select(c => c.Id).ToHashSet();
        Resolve(g, source);
        var burned = q.GraveyardZone.Single(c => !grave.Contains(c.Id));
        Core2026Proof.Check(q.HandZone.Count == 10 && burned.Card.Id == "CATA_186t" && burned.OngoingEffect == null, "full opponent hand burns token without aura");
        foreach (var held in q.HandZone) { Visible(held, 4); Actors(held); }
    });

    private static Enchantment[] Actors(IPlayable target, params int[] sources)
    {
        var actors = (target.AppliedEnchantments ?? new List<Enchantment>()).Where(e => e.Card.Id == "CATA_186te").ToArray();
        Core2026Proof.Check(actors.Select(e => e.Creator.Id).Order().SequenceEqual(sources.Order())
            && actors.All(e => ReferenceEquals(e.Target, target) && e.Power.Enchant == null), "one presentation-only actor per neighboring Sabotage");
        return actors;
    }
    private static void Retired(Enchantment actor, bool captureHistory = true)
    {
        Core2026Proof.Check(!actor.Target.AppliedEnchantments.Contains(actor), "retired neighbor actor detached");
        if (actor.Game.History && captureHistory)
            Core2026Proof.Check(actor[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME
                && actor.Game.PowerHistory.Full.OfType<PowerHistoryHideEntity>().Any(e => e.EntityID == actor.Id), "retired neighbor actor hidden");
    }
}
