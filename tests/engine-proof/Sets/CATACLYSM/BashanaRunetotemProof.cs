using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class BashanaRunetotemProof
{
    internal static void Run() => Unpinned_ExactTwelveBySlots();
    private static void Unpinned_ExactTwelveBySlots() => StandardSetsProof.Case("MEND_046", (g, p, id) => {
        for (int slots = 3; slots >= 1; slots--)
        {
            PoolPilotProof.EmptyHand(p); StandardSetsProof.ClearBoard(g, p);
            for (int i = 0; i < 10 - slots; i++) Add(p, "CS2_120");
            var source = Play(g, p, id);
            var treants = p.HandZone.Where(c => c.Card.Id == "MEND_046t").ToArray();
            var nature = OfflineCardPools.Candidates(g.OfflinePools, p.BaseClass,
                OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 4), Cards.FromId(id).AssetId);
            bool exists = slots == 1 ? nature.Any(c => c.Cost == 12)
                : slots == 2 ? nature.Any(a => nature.Any(b => a.Cost + b.Cost == 12))
                : nature.Any(a => nature.Any(b => nature.Any(c => a.Cost + b.Cost + c.Cost == 12)));
            Core2026Proof.Check(treants.Length == (exists ? slots : 0), "capacity-bound exact tuple or no-valid-tuple fizzle");
            if (!exists) continue;
            Core2026Proof.Check(treants.SelectMany(t => PayloadRules.Entries(t, 1)).Sum(c => c.Card.Cost) == 12
                && treants.All(t => t.Card.Cost == 1 && ((Minion)t).AttackDamage == 2 && ((Minion)t).Health == 2), "one Nature spell per 2/2 Treant, exact printed twelve");
            var original = treants[0]; p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.First(c => c.Card.Id != "MEND_046t")));
            var copy = Generic.Copy(p, p.Hero, original, Zone.HAND); g.AuraUpdate();
            Core2026Proof.Check(PayloadRules.Entries(copy, 1).Single().Id != PayloadRules.Entries(original, 1).Single().Id
                && copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == PayloadRules.Entries(copy, 1).Single().Id
                && original[GameTag.TAG_SCRIPT_DATA_NUM_1] == PayloadRules.Entries(original, 1).Single().Id,
                "carved spell copy independent; each Treant references its own carved entity (tag 2 is an entity id)");
        }
        PoolPilotProof.EmptyHand(p);
        for (int i = 0; i < 10; i++) Add(p, "CS2_120");
        int entities = g.IdEntityDic.Count; var rng = g.Random.CaptureState();
        g.TaskQueue.Execute(Cards.FromId(id).Power.PowerTask, p, p.BoardZone.Single(m => m.Card.Id == id), null); g.ProcessTasks();
        Core2026Proof.Check(g.IdEntityDic.Count == entities && rng.Equals(g.Random.CaptureState()), "zero slots creates nothing and consumes no RNG");
        PoolPilotProof.EmptyHand(p);
        var token = Add(p, "MEND_046t"); PayloadRules.StoreCard(token, 1, Cards.FromId("CS2_005"));
        int armor = p.Hero.Armor; PlayEntity(g, p, token);
        Core2026Proof.Check(p.Hero.Armor == armor + 2, "carved token actually casts its stored Claw");
    });
}
