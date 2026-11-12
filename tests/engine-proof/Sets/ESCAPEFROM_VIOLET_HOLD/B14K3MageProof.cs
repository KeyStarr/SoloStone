using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static B14K3BEProof;

internal static class B14K3ESCAPEFROMVIOLETHOLDProof
{
    internal static void CardsProof() => StandardSetsProof.Case("JAIL_735", Unpinned_CodeVioletCastHistory);
    // repeat-effects/shadow-rounds-cast-history.yaml (C Defile analogy, approved player-cast counter policy).
    private static void Unpinned_CodeVioletCastHistory(Game g, Controller p, string id)
    {
        CheckPoolBuckets(g, id);
        Core2026Proof.Check(Cards.FromId(id)[(GameTag)4354] == 1, "printed Prepare retained");
        var filter = Filter(id); var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3, "eight-cost all-class pool minimum");
        var countered = g.CloneExact(history: true); var caster = countered.Player1;
        var waiting = Add(caster, id);
        End(countered); Play(countered, caster.Opponent, "CORE_EX1_287"); End(countered);
        for (int i = 0; i < 3; i++) Play(countered, caster, "CS2_008", caster.Hero);
        Core2026Proof.Check(!PoweredUp.IsMet(waiting) && waiting[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "one countered plus two completed player spells is below threshold");
        foreach (int count in new[] { 0, 2, 3 })
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            var held = Add(owner, id);
            for (int i = 0; i < count; i++) Play(branch, owner, "CS2_008", owner.Hero);
            branch.RefreshHistoryDisplays();
            Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == Math.Min(3, count) && PoweredUp.IsMet(held) == (count >= 3), "visible progress (client prints 3-SDN1 left) and glow match completed own casts");
            LocationProofs.Location(owner); Summon(owner, "CS2_182");
            var existing = owner.BoardZone.Slots.Select(c => c.Id).ToHashSet();
            branch.SetRandomSeed(seed); var reference = branch.CloneExact(history: true);
            var expected = Enumerable.Range(0, count >= 3 ? 2 : 1).Select(_ => OfflineCardPools.Pick(reference, filter, owner.HeroClass, Cards.FromId(id).AssetId).Id).ToArray();
            Future(branch, next => {
                PlayEntity(next, next.Player1, next.IdEntityDic[held.Id]);
                var generated = next.Player1.BoardZone.Where(m => !existing.Contains(m.Id)).ToArray();
                Core2026Proof.Check(generated.Select(m => m.Card.Id).SequenceEqual(expected) && generated.All(m => m.Card.Type == CardType.MINION && m.Card.Cost == 8), "seeded eight-cost summons in creation order; threshold excludes current spell");
            });
        }
        var automatic = (Spell)Entity.FromCard(p, Cards.FromId("CS2_008"));
        for (int i = 0; i < 3; i++) Generic.CastSpell(p, g, automatic, p.Opponent.Hero, 0);
        var card = Add(p, id);
        Core2026Proof.Check(!PoweredUp.IsMet(card), "effect casts excluded from player count");
        for (int i = 0; i < 3; i++) Play(g, p, "CS2_008", p.Hero);
        Core2026Proof.Check(PoweredUp.IsMet(card), "third other spell qualifies");
        End(g); Core2026Proof.Check(!PoweredUp.IsMet(card), "global turn boundary clears condition");
        for (int i = 0; i < 3; i++) Play(g, p.Opponent, "CS2_008", p.Hero);
        Core2026Proof.Check(!PoweredUp.IsMet(card), "opponent casts do not qualify"); End(g);
        var preparation = g.CloneExact(history: true); var preparer = preparation.Player1;
        ParkBoard(preparer); PoolPilotProof.EmptyHand(preparer);
        var prepared = Add(preparer, id); preparer.BaseMana = 10; preparer.UsedMana = 0;
        Core2026Proof.Check(preparation.Process(PrepareCardTask.Any(preparer, prepared)) && prepared.Cost == 0
            && prepared[GameTag.PREPARING] == 1, "Prepare pays six and discounts to zero while locking this turn");
        End(preparation); End(preparation);
        PlayEntity(preparation, preparer, prepared);
        Core2026Proof.Check(preparer.BoardZone.Count == 1 && preparer.BoardZone.Single().Card.Cost == 8,
            "prepared spell plays next turn through same ordinary summon path");
        Pool(g, new[] { "CORE_EX1_414" }, () => {
            ParkBoard(p); LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
            for (int i = 0; i < 3; i++) Play(g, p, "CS2_008", p.Hero);
            PlayEntity(g, p, card);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CORE_EX1_414") == 1, "one physical slot truncates second summon");
        });
    }

}
