using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k4-warrior-dk", ProofKind.PoweredUp)]
    private static void RunB14K4Glow()
    {
        Scenario("FIR_951", (f, card, met) => CorpseRules.Gain(f.Player, met ? 10 : 9),
            (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
                bool offered = f.Player.Choice != null;
                if (offered)
                {
                    Check(f.Player.Choice.Choices.Count == 1 && f.Game.IdEntityDic[f.Player.Choice.Choices[0]].Card.Id == "FIR_951t2",
                        "only affordable Volcoross tier reaches native choice");
                    f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices[0]));
                }
                var body = (Minion)card;
                Check(body.AttackDamage == (offered ? 15 : 5) && body.Health == (offered ? 15 : 5)
                    && CorpseRules.Available(f.Player) == (offered ? 0 : 9), "Volcoross choice reconciles stats and resources");
                return offered;
            });
    }

    [GroupProof("native-b14-k4-warrior-dk", ProofKind.Native)]
    private static void RunB14K4Privacy()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "JAIL_444", "CORE_CATA_007", "EDR_455" })
        {
            var f = new Fixture(); var g = f.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(f.Player)), "privacy fixture opponent turn");
            var owner = opponent ? g.Player2 : g.Player1;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var source = Generic.DrawCard(owner, Cards.FromId(id));
            if (id == "JAIL_444") f.Summon("CS2_182", owner);
            if (id == "CORE_CATA_007") { var m = f.Summon("CS2_182", owner.Opponent); m.Damage = 2; }
            if (id == "EDR_455")
            {
                var dragon = f.Summon("EX1_043", owner); dragon.Destroy(); g.DeathProcessingAndAuraUpdate();
            }
            var deckIds = owner.DeckZone.Select(e => e.Id).ToArray();
            f.Flush(); owner.BaseMana = 10; owner.UsedMana = 0;
            Check(g.Process(PlayCardTask.Any(owner, source)), "native card play " + id);
            var offers = owner.Choice?.Choices.ToArray() ?? Array.Empty<int>();
            f.Flush();
            if (opponent)
                Check(f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity })
                    .Where(e => e != null && offers.Contains(e.Entity)).All(e => string.IsNullOrEmpty(e.Name)),
                    "opponent offers remain hidden before answer");
            if (offers.Length != 0)
            {
                var d = g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { offers[0] }), out _), "native saved choice answer");
                f.Flush();
            }
            var snapshots = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity }).Where(e => e != null).ToArray();
            var drawn = owner.HandZone.Where(e => deckIds.Contains(e.Id)).ToArray();
            Check(id == "EDR_455" ? offers.Length == 1 : drawn.Length == 1, "effect produced its expected draw/choice");
            bool HiddenTag(int tag) => tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER
                || tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;
            var privateIds = opponent ? drawn.Select(e => e.Id).Concat(offers.Where(x => g.IdEntityDic[x].Zone != owner.BoardZone)).ToArray() : Array.Empty<int>();
            Check(snapshots.Where(e => privateIds.Contains(e.Entity)).All(e => string.IsNullOrEmpty(e.Name)
                && e.Tags.All(t => HiddenTag(t.Name))), "opponent draw/Discover identities and definition tags remain private: " + id);
            Check(f.Entries.Where(e => e.HasTagChange && privateIds.Contains(e.TagChange.Entity)).All(e => HiddenTag(e.TagChange.Tag)),
                "opponent hidden card tag changes remain private: " + id);
            if (!opponent)
                foreach (var drawnCard in drawn)
                    Check(snapshots.Any(e => e.Entity == drawnCard.Id && e.Name == drawnCard.Card.Id), "owner receives drawn identity");
        }
        foreach (bool opponent in new[] { false, true })
        {
            var f = new Fixture(); var g = f.Game; var owner = opponent ? g.Player2 : g.Player1;
            var source = f.Summon("TIME_850", owner);
            var selected = Generic.DrawCard(owner, Cards.FromId("TIME_850t"));
            var unselected = Generic.DrawCard(owner, Cards.FromId("CS2_200"));
            f.Flush(); source.Destroy(); g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(selected.Zone == owner.BoardZone && unselected.Zone == owner.HandZone, "Blood Fighter moves only actual selected member");
            var snapshots = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity }).Where(e => e != null).ToArray();
            Check(snapshots.Any(e => e.Entity == selected.Id && e.Name == "TIME_850t"), "summoned hand member becomes public");
            if (opponent)
                Check(snapshots.Where(e => e.Entity == unselected.Id).All(e => string.IsNullOrEmpty(e.Name)),
                    "Blood Fighter selection never reveals unrelated opponent hand identity");
        }
        Console.WriteLine("PASS native b14-k4-warrior-dk: draw and Discover privacy, both perspectives");
    }
}
