using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static partial class B16DiscoverExtCardsProof
{
    private static void Unpinned_TokiTrio(Game g, Controller p, string id)
    {
        var pool = OfflineCardPools.Candidates(g, CardClass.MAGE, OfflinePoolFilter.Random(type: CardType.SPELL, scope: PoolScope.Past), 119741);
        Console.WriteLine("Toki any-class Past spell bucket=" + pool.Count);
        var rng = g.Random.Clone();
        var expected = Enumerable.Range(0,3).Select(_ => pool[rng.Next(pool.Count)].Id).ToArray();
        var toki = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(expected), "Toki three independent seeded Past spells in order");
        var spells = p.HandZone.ToArray();
        Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "TIME_861e1") == 1, "activation-local tracker");
        var lost = g.CloneExact(history: true);
        Generic.DiscardBlock(lost.Player1, lost.IdEntityDic[spells[0].Id]);
        Core2026Proof.Check(lost.Player1.AppliedEnchantments.Any(e => e.Card.Id == "TIME_861e1"), "lost spell leaves trio incomplete");
        var transformed = g.CloneExact(history: true);
        foreach (var spell in spells) {
            var minion = Generic.ChangeEntityBlock(transformed.Player1, transformed.IdEntityDic[spell.Id], Cards.FromId("CS2_231"), true);
            PlayEntity(transformed, transformed.Player1, minion);
        }
        Core2026Proof.Check(transformed.Player1.HandZone.All(c => c.Card.Id != id)
            && transformed.Player1.AppliedEnchantments.Any(e => e.Card.Id == "TIME_861e1"), "transformed minion plays never finish Toki");
        var overflow = DiscoverRealProof.New();
        PoolPilotProof.FillHand(overflow.Player1, 9);
        Play(overflow, overflow.Player1, id);
        var overflowTracker = overflow.Player1.AppliedEnchantments.Single(e => e.Card.Id == "TIME_861e1");
        Core2026Proof.Check(Enumerable.Range(0,3).Count(i => overflow.IdEntityDic[overflowTracker[(GameTag)(32410+i)]].Zone == overflow.Player1.GraveyardZone) == 2,
            "full hand burns two tracked spells; activation remains incomplete");
        Kill(g, toki);
        DiscoverRealProof.Saved(g, next => {
            var owner = next.Player1;
            foreach (var spell in spells) {
                // Earlier generated removal/summons must not make the next qualified spell unplayable.
                StandardSetsProof.ParkBoard(owner); StandardSetsProof.ParkBoard(owner.Opponent);
                Summon(owner, "CS2_200"); Summon(owner.Opponent, "CS2_200");
                owner.BaseMana = 10; owner.UsedMana = 0;
                var task = owner.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id);
                Core2026Proof.Check(task != null && next.Process(task), "generated Past spell plays normally: " + spell.Card.Id);
                while (owner.Choice != null) DiscoverRealProof.Answer(next, owner.Choice.Choices.First());
            }
            Core2026Proof.Check(owner.HandZone.Count(c => c.Card.Id == id) == 1 && !owner.AppliedEnchantments.Any(e => e.Card.Id == "TIME_861e1"),
                "three exact spell plays grant one Toki after original dies; clone/snapshot");
        });
    }

}
