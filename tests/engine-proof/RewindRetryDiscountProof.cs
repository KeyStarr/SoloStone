using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static partial class RewindProof
{
    internal static void ClocksworthCopiedRecipe()
    {
        var g = Fixture(); var source = Summon(g.Player1, "CS2_182");
        Cards.FromId("TIME_038").Power.PowerTask.Process(g, g.Player1, source, null);
        g.DeathProcessingAndAuraUpdate();
        Check(g.Player1.BoardZone.Count == 3
            && g.Player1.BoardZone.Where(card => card.Id != source.Id).All(card => card.Card.Rarity == Rarity.LEGENDARY),
            "copied Clocksworth Battlecry uses its declared recipe with another runtime source");
        Console.WriteLine("PASS Rewind ClocksworthCopiedRecipe");
    }

    internal static void KarovClocksworthAfterExpiredTax()
    {
        var g = Fixture(); var p = g.Player1;
        var karov = Summon(p, "JAIL_448");
        // Constrain only Karov's real Deathrattle; retries use the installed pool.
        var installed = g.OfflinePools;
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(row => (string)row.Attribute("cardId") == "TIME_038").ToArray());
        xml.Root.Element("Dependencies").RemoveNodes();
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, OfflinePoolFixtures.Manifest(xml)); Kill(g, karov); }
        finally { property.SetValue(g, installed); }
        var source = (Minion)p.HandZone.First(card => card.Card.Id == "TIME_038");
        int enchantment = source.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_448e").Id;
        Check(source.Cost == 1 && source.AttackDamage == 1 && source.Health == 1
            && source[GameTag.DISPLAYED_CREATOR] == karov.Id, "Karov generates discounted 1/1 Clocksworth");

        End(g); Play(g, g.Player2, "TLC_439");
        Check(source.Cost == 3, "later Wave of Tar taxes Karov's discounted card");
        End(g); End(g); End(g);
        Check(source.Cost == 1 && g.CurrentPlayer == p, "expired tax restores Karov cost before play");
        p.BaseMana = 1; p.UsedMana = 0; p.TemporaryMana = 0;
        PlayCardTask Task(Game game) => new PlayCardTask(game.Player1, game.IdEntityDic[source.Id]);
        var preplay = new[] { g, g.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot()) };
        foreach (var game in preplay)
        {
            Check(game.IdEntityDic[source.Id].Cost == 1 && game.Process(Task(game)), "one-mana Karov Clocksworth play accepted");
            var offered = new[] { game, game.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot()) };
            foreach (var next in offered)
            {
                for (int used = 1; used <= 3; used++)
                {
                    Answer(next, true);
                    var replayed = (Minion)next.IdEntityDic[source.Id];
                    Check(replayed.Card.Id == "TIME_038t" + used && replayed[GameTag.USED_REWIND] == used
                        && replayed.AttackDamage == 1 && replayed.Health == 1
                        && replayed.AppliedEnchantments.Any(e => e.Id == enchantment && e.Card.Id == "JAIL_448e"),
                        "retry retains Karov stats/enchantment and advances Clocksworth identity");
                    Accounting(next, source.Id, 1);
                    Check(replayed[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 1
                        && (next.Rewind == null || PinnedPoolFixture.LoadSnapshot(next.Rewind.Baseline).IdEntityDic[source.Id].Cost == 1),
                        "retry baseline retains discounted cost before payment");
                }
                Check(next.Rewind == null, "Karov Clocksworth exhausts three retries");
            }
            Check(offered.Select(Canonical.State).Distinct().Count() == 1
                && offered.Select(next => next.Random.CaptureState()).Distinct().Count() == 1,
                "offered source/clone/load retry future parity");
        }
        Check(preplay.Select(Canonical.State).Distinct().Count() == 1, "pre-play source/clone/load retry parity");
        Console.WriteLine("PASS Rewind KarovClocksworthAfterExpiredTax: real Karov generation, expired Wave of Tar, three one-mana retries, source/clone/load parity");
    }
}
