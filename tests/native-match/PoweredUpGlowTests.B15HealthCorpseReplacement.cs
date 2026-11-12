using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-health-corpse-replacement", ProofKind.PoweredUp)]
    private static void RunB15ReplacementGlow()
    {
        Scenario("EDR_813", (f, card, met) => {
            f.Summon("CS2_182", f.Player.Opponent); CorpseRules.Gain(f.Player, met ? 2 : 1);
        }, (f, card) => PlayCardTask.Any(f.Player, card,
            CorpseRules.Available(f.Player) >= 2 ? f.Player.Opponent.BoardZone.Single() : null,
            chooseOne: CorpseRules.Available(f.Player) >= 2 ? 2 : 1),
        (f, card) => f.Player.Opponent.BoardZone.Single().Damage == 4);
    }

    [GroupProof("native-b15-health-corpse-replacement", ProofKind.Native)]
    private static void RunB15ReplacementNative()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "CATA_155", "CORE_EDR_003" })
        {
            var f = new Fixture(); var g = f.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(f.Player)), "opponent turn for privacy");
            var owner = opponent ? g.Player2 : g.Player1;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            if (id == "CORE_EDR_003") SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CORE_RLK_118"), zone: owner.DeckZone);
            var card = Generic.DrawCard(owner, Cards.FromId(id)); f.Flush();
            var handBefore = owner.HandZone.Select(e => e.Id).ToArray();
            owner.BaseMana = 10; owner.UsedMana = 0;
            Check(g.Process(PlayCardTask.Any(owner, card)), "native replacement card play " + id);
            f.Flush();
            var hidden = owner.HandZone.Where(e => !handBefore.Contains(e.Id)).Select(e => e.Id).ToArray();
            Check(hidden.Length == (id == "CATA_155" ? 2 : 1), "expected hidden outputs");
            bool Allowed(int tag) => tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER
                || tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;
            var snapshots = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity }).Where(e => e != null && hidden.Contains(e.Entity)).ToArray();
            if (opponent)
            {
                Check(snapshots.All(e => string.IsNullOrEmpty(e.Name) && e.Tags.All(t => Allowed(t.Name))), "opponent output identities stay private");
                Check(f.Entries.Where(e => e.HasTagChange && hidden.Contains(e.TagChange.Entity)).All(e => Allowed(e.TagChange.Tag)), "opponent output definition tags stay private");
            }
            else foreach (int entity in hidden) Check(snapshots.Any(e => e.Entity == entity && !string.IsNullOrEmpty(e.Name)), "owner sees generated/drawn card");
        }
        var native = new Fixture(); var p = native.Player;
        var agam = native.Draw("EDR_489"); native.Process(PlayCardTask.Any(p, agam));
        var next = native.Draw("CS2_182"); native.Flush();
        Check(native.Match.SnapshotVisibleTransportTags(next).TryGetValue(2837, out int kind) && kind == 4, "retail opponent-Health alternate cost");
        native.Mana(0); native.Process(PlayCardTask.Any(p, next));
        Check(p.Opponent.Hero.Health == 26 && p.UsedMana == 0, "native opponent payment outcome");
        var onyxia = new Fixture(); p = onyxia.Player;
        var body = onyxia.Draw("CATA_155"); onyxia.Process(PlayCardTask.Any(p, body));
        p.Hero.TakeDamage(p.Hero, 3); onyxia.Flush();
        Check(onyxia.Match.SnapshotVisibleTransportTags(p.Hero)[(int)GameTag.HEALTH] == 33 && p.Hero.Health == 33,
            "native max/current Health replacement");
        Check(onyxia.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity }).Any(e => e != null && e.Name == "CATA_155e"), "exported Black Scales actor projected");
        Console.WriteLine("PASS native b15 Health/Corpse replacements and negative privacy");
    }
}
