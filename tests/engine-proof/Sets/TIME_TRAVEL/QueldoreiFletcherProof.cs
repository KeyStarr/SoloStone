using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class QueldoreiFletcherProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_606", (g, p, id) => {
        Definition("TIME_606e");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 3, "1/3 Fletcher");
        var initial = Presentation(p.Hero.HeroPower, source.Id).Single();
        Core2026Proof.Check(g.PowerHistory.Full.OfType<PowerHistoryShowEntity>().Any(e => e.Entity.Id == initial.Id
            && e.Entity.Name == "TIME_606e" && e.Entity.Tags[GameTag.ATTACHED] == p.Hero.HeroPower.Id), "presentation SHOW_ENTITY attaches to Hero Power");
        FillHand(p, 3); Visible(p.Hero.HeroPower, 0);
        g.AuraUpdate(); g.AuraUpdate();
        Core2026Proof.Check(Presentation(p.Hero.HeroPower, source.Id).Single().Id == initial.Id, "refresh keeps one presentation actor");
        var fourth = Add(p, "CS2_182"); Visible(p.Hero.HeroPower, 2); Presentation(p.Hero.HeroPower); Retired(initial);
        p.SetasideZone.Add(p.HandZone.Remove(fourth)); Visible(p.Hero.HeroPower, 0);
        var old = p.Hero.HeroPower; var rebound = Presentation(old, source.Id).Single();
        p.Hero.HeroPower = (HeroPower)Entity.FromCard(p, Cards.FromId("CS1h_001"));
        Visible(p.Hero.HeroPower, 0); Visible(old, old.Card.Cost); Presentation(old); Retired(rebound);
        var active = Presentation(p.Hero.HeroPower, source.Id).Single();
        var quiet = g.CloneExact(history: false);
        foreach (var resumed in new[] { quiet, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var restoredActor = Presentation(resumed.Player1.Hero.HeroPower, source.Id).Single();
            Core2026Proof.Check(restoredActor.Id == active.Id && !ReferenceEquals(restoredActor, active), "independent restored presentation actor");
            ((Minion)resumed.IdEntityDic[source.Id]).Silence(); resumed.AuraUpdate();
            Presentation(resumed.Player1.Hero.HeroPower); Retired(restoredActor, captureHistory: !ReferenceEquals(resumed, quiet)); Presentation(p.Hero.HeroPower, source.Id);
            Visible(resumed.Player1.Hero.HeroPower, 2); Visible(p.Hero.HeroPower, 0);
        }
        p.Hero.HeroPower.IsExhausted = false; p.BaseMana = 0; p.UsedMana = 0; p.TemporaryMana = 0; int activations = p.HeroPowerActivationsThisTurn;
        var activation = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
        Core2026Proof.Check(g.Process(activation) && p.RemainingMana == 0 && p.HeroPowerActivationsThisTurn == activations + 1, "zero-cost Hero Power activation");
        source.Silence(); g.AuraUpdate(); Visible(p.Hero.HeroPower, 2); Presentation(p.Hero.HeroPower); Retired(active);
        var first = Summon(p, id); var second = Summon(p, id); Visible(p.Hero.HeroPower, 0);
        var actors = Presentation(p.Hero.HeroPower, first.Id, second.Id);
        Kill(g, first); Visible(p.Hero.HeroPower, 0); Presentation(p.Hero.HeroPower, second.Id); Retired(actors.Single(e => e.Creator.Id == first.Id));
        Kill(g, second); Visible(p.Hero.HeroPower, 2); Presentation(p.Hero.HeroPower); Retired(actors.Single(e => e.Creator.Id == second.Id));
        EmptyHand(p);
        var bounced = Summon(p, id); var bouncedActor = Presentation(p.Hero.HeroPower, bounced.Id).Single();
        p.HandZone.Add(p.BoardZone.Remove(bounced)); g.AuraUpdate();
        Visible(p.Hero.HeroPower, 2); Presentation(p.Hero.HeroPower); Retired(bouncedActor);
        var held = Add(p, id); Visible(p.Hero.HeroPower, 2);
        var copy = Generic.Copy(p.Opponent, p.Hero, held, Zone.HAND); Visible(p.Opponent.Hero.HeroPower, 2);
    });

    private static Enchantment[] Presentation(HeroPower power, params int[] sources)
    {
        var actors = (power.AppliedEnchantments ?? new List<Enchantment>()).Where(e => e.Card.Id == "TIME_606e").ToArray();
        Core2026Proof.Check(actors.Select(e => e.Creator.Id).OrderBy(id => id).SequenceEqual(sources.OrderBy(id => id))
            && actors.All(e => ReferenceEquals(e.Target, power) && e.Power.Enchant == null), "presentation only, one actor per active Fletcher");
        return actors;
    }

    private static void Retired(Enchantment actor, bool captureHistory = true)
    {
        Core2026Proof.Check(!actor.Target.AppliedEnchantments.Contains(actor), "retired presentation detached");
        if (actor.Game.History)
            Core2026Proof.Check(actor[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME, "retired presentation removed from game");
        if (captureHistory)
            Core2026Proof.Check(actor.Game.PowerHistory.Full.OfType<PowerHistoryHideEntity>().Any(e => e.EntityID == actor.Id), "retired presentation hidden");
    }
}
