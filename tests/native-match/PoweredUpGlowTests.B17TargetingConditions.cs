using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b17-targeting-conditions", ProofKind.PoweredUp)]
    private static void RunB17TargetingGlow()
    {
        Scenario("CS3_022", (f, card, met) => {
            if (met) { var weapon = f.Draw("CS2_106"); f.Process(PlayCardTask.Any(f.Player, weapon)); f.Mana(10); }
        }, (f, card) => PlayCardTask.Any(f.Player, card, f.Player.Hero.Weapon != null ? f.Player.Opponent.Hero : null),
        (f, card) => f.Player.Opponent.Hero.Damage == 2);
    }

    [GroupProof("native-b17-targeting-conditions", ProofKind.Native)]
    private static void RunB17TargetingNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var f = new Fixture(); var g = f.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(f.Player)), "opponent turn");
            var owner = opponent ? g.Player2 : g.Player1;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var hand = Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            var source = Generic.DrawCard(owner, Cards.FromId("CATA_161"));
            owner.BaseMana = 10; owner.UsedMana = 0;
            f.Flush(); Check(g.Process(PlayCardTask.Any(owner, source)), "mixed selector play"); f.Flush();
            Check(owner.Choice != null && owner.Choice.Choices.Contains(hand.Id), "hand actor offered");
            AnswerB17(g, hand.Id); f.Flush();
            Check(((Minion)hand).AttackDamage == 7, "hand buff outcome");
            AssertB17Hidden(f, hand.Id, opponent);
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var bribe = Generic.DrawCard(owner, Cards.FromId("JAIL_861"));
            owner.Hero[GameTag.CLASS] = (int)CardClass.DRUID;
            owner.UsedMana = 0; f.Flush();
            Check(g.Process(PlayCardTask.Any(owner, bribe)), "Bribe play"); f.Flush();
            Check(owner.Choice != null, "Bribe offers");
            var offerIds = owner.Choice.Choices.ToArray();
            if (opponent) foreach (int offer in offerIds) AssertB17Hidden(f, offer, true);
            var previous = owner.Opponent.HandZone.Select(e => e.Id).ToArray();
            AnswerB17(g, offerIds[0]); f.Flush();
            var marked = owner.HandZone.Single(); var plain = owner.Opponent.HandZone.Single(e => !previous.Contains(e.Id));
            AssertB17Hidden(f, marked.Id, opponent); AssertB17Hidden(f, plain.Id, !opponent);
            Check(marked[GameTag.CHOOSE_BOTH] == 1 && plain[GameTag.CHOOSE_BOTH] == 0, "local Choose Both only");
        }
        var aura = new Fixture(); var enemy = aura.Summon("CS2_182", aura.Player.Opponent);
        var lancer = aura.Draw("CATA_898"); aura.Process(PlayCardTask.Any(aura.Player, lancer));
        Check(aura.Match.SnapshotVisibleTransportTags(enemy)[(int)GameTag.TAUNT] == 1, "enemy Taunt projected");
        ((Minion)lancer).Silence(); aura.Game.DeathProcessingAndAuraUpdate(); aura.Flush();
        Check(!enemy.HasTaunt, "native aura removal");
    }

    private static void AssertB17Hidden(Fixture f, int entityId, bool hidden)
    {
        bool Allowed(int tag) => tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER
            || tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;
        var snapshots = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
            .Where(e => e != null && e.Entity == entityId).ToArray();
        if (!hidden) { Check(snapshots.Any(e => !string.IsNullOrEmpty(e.Name)), "owner sees identity"); return; }
        Check(snapshots.All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => Allowed(t.Name))), "opponent never sees hidden identity or definition tags");
        Check(f.Entries.Where(e => e.HasTagChange && e.TagChange.Entity == entityId).All(e => Allowed(e.TagChange.Tag)), "hidden incremental tags private");
    }

    private static void AnswerB17(SabberStoneCore.Model.Game game, int entity)
    {
        var decision = game.GetPendingDecisions().Single();
        Check(game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { entity }), out var error), "answer " + error);
    }
}
