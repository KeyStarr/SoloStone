using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B15PoolFiltersCATACLYSMProof
{
    [GroupProof("b15-pool-filters-cataclysm-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_567", Unpinned_AscendanceFreshOriginalAndExactTier);
        StandardSetsProof.Case("CATA_621", Unpinned_GelbinInstanceDuration);
        StandardSetsProof.Case("MEND_100", Unpinned_BulbOwnerStartAndCopy);
    }
    private static void Unpinned_AscendanceFreshOriginalAndExactTier(Game g, Controller p, string id)
    {
        var original = Summon(p, "CS2_182"); original.AttackDamage += 3; original.Damage = 1;
        var enemy = Summon(p.Opponent, "CS2_182");
        var random = g.Random.Clone();
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, OfflinePoolFilter.Random(type: CardType.MINION).WithCost(5), Cards.FromId(id).AssetId);
        string expected = pool[random.Next(pool.Count)].Id;
        Play(g, p, id);
        var result = p.BoardZone.Single();
        Core2026Proof.Check(result.Card.Id == expected && enemy.Card.Id == "CS2_182" && (enemy.AppliedEnchantments?.Count ?? 0) == 0, "seeded +1 current Cost, opponent unaffected");
        var ascended = result.AppliedEnchantments.Single(e => e.Card.Id == "CATA_567e");
        Core2026Proof.Check(ascended.CapturedCard.Id == "CS2_182", "attachment captures old identity before transform");
        // Client text "Summon {0}" names an entity: the attachment stores a copy of the original (public on the board) and references it.
        var storedOriginal = PayloadRules.Entries(ascended, 1).Single();
        Core2026Proof.Check(storedOriginal.Card.Id == "CS2_182" && ascended[PayloadRules.ReferenceEntity] == storedOriginal.Id
            && storedOriginal.Id < ascended.Id && ((PayloadPolicy)storedOriginal[PayloadRules.Policy]).HasFlag(PayloadPolicy.ShowAll)
            && storedOriginal.Zone == p.SetasideZone && ((Minion)storedOriginal).AttackDamage == 4 && ((Minion)storedOriginal).Damage == 0,
            "Ascended stores a fresh copy of the original (created before the attachment, so its first snapshot can reference it)");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var clonedAttachment = (Enchantment)next.IdEntityDic[ascended.Id];
            var clonedStored = PayloadRules.Entries(clonedAttachment, 1).Single();
            Core2026Proof.Check(clonedAttachment[PayloadRules.ReferenceEntity] == storedOriginal.Id && clonedStored.Id == storedOriginal.Id
                && clonedStored.Card.Id == "CS2_182", "clone/load keep Ascended's stored original and reference");
        }
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Kill(next, (Minion)next.IdEntityDic[result.Id]);
            var returned = next.Player1.BoardZone.Last();
            Core2026Proof.Check(returned.Card.Id == "CS2_182" && returned.AttackDamage == 4 && returned.Health == 5
                && returned.Damage == 0 && !returned.IsSilenced && (returned.AppliedEnchantments?.Count ?? 0) == 0, "clone/snapshot return fresh full-health original without old buffs");
        }
        result.Silence(); Kill(g, result);
        Core2026Proof.Check(!p.BoardZone.Any(), "Silence removes return-original Deathrattle");
        Core2026Proof.Check(PayloadRules.Entries(ascended, 1).Length == 0, "Silence retires Ascended's stored original with its attachment");
        Summon(p, "CS2_182");
        B15PoolTest.WithMembers(g, Array.Empty<string>(), () => {
            var before = g.Random.CaptureState(); Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Single().Card.Id == "CS2_182" && (p.BoardZone.Single().AppliedEnchantments?.Count ?? 0) == 0
                && g.Random.CaptureState().Equals(before), "D2 empty tier leaves original unchanged without attachment or fallback");
        });
        StandardSetsProof.ParkBoard(p);
        var ten = Summon(p, "CS2_182"); Effects.SetCost(10).ApplyTo(ten);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Single().Card.Cost == 10, "Pinned_EvolveTenStaysTen");
    }
    private static void Unpinned_GelbinInstanceDuration(Game g, Controller p, string id)
    {
        KnownMetaProofFixture.UseBlackEmpire(g);
        var filter = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter;
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count == 6 && candidates.Select(c => c.Id).Distinct().Count() == 6, "six curated Paladin Auras including dual-class Acceleration once");
        var random = g.Random.Clone(); string expected = candidates[random.Next(candidates.Count)].Id;
        Play(g, p, id);
        var aura = (Spell)p.HandZone.Single();
        int duration = aura.Card.Id == "TTN_851" ? 3 : 4;
        Core2026Proof.Check(aura.Card.Id == expected && AuraSpellRules.Remaining(aura) == duration, "seeded generated instance gains one own clock tick");
        End(g); End(g);
        B15PoolTest.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[aura.Id]));
        Core2026Proof.Check(AuraSpellRules.Remaining(aura) == duration, "bonus survives waiting in hand and activation");
        p.UsedMana = 0; var normal = (Spell)Play(g, p, aura.Card.Id);
        Core2026Proof.Check(AuraSpellRules.Remaining(normal) == duration - 1, "unrelated Aura unchanged");
        for (int i = 0; i < 10; i++) End(g);
        Core2026Proof.Check(aura.Zone == p.GraveyardZone && normal.Zone == p.GraveyardZone, "each instance expires on its ordinary clock");
    }
    private static void Unpinned_BulbOwnerStartAndCopy(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g, p, id); var bulb = p.HandZone.Single();
        Core2026Proof.Check(bulb.Card.Id == "MEND_100t" && bulb.Cost == 3 && bulb[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "fresh 3-Cost Bulb tier one");
        body.Silence(); Kill(g, body); End(g);
        Core2026Proof.Check(bulb[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "opponent start does not upgrade held Bulb");
        B15PoolTest.Resume(g, End);
        Core2026Proof.Check(bulb[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && bulb.Cost == 3, "next owner start upgrades spell tier only");
        var copy = Generic.Copy(p, p.Hero, bulb, Zone.HAND);
        var fresh = Add(p, "MEND_100t"); g.AuraUpdate();
        Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && fresh[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "hand copy keeps tier; fresh identity regeneration resets");
        p.SetasideZone.Add(p.HandZone.Remove(copy));
        End(g); End(g);
        Core2026Proof.Check(bulb[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2
            && fresh[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "independent held-only upgrades");
        B15PoolTest.WithMembers(g, Array.Empty<string>(), () => {
            var before = g.Random.CaptureState();
            B15PoolTest.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[bulb.Id]));
            Core2026Proof.Check(g.Random.CaptureState().Equals(before) && p.Choice == null, "three empty exact tiers fizzle without substitute or decision");
        });
        // A singleton ordinary spell isolates exactly three casts from targeting and secondary random effects.
        var one = GiftedDiscoverProof.New(g.Seed, CardClass.ROGUE);
        B15PoolTest.WithMembers(one, new[] { "CORE_BOT_451" }, () => {
            var source = Play(one, one.Player1, "MEND_100t");
            var casts = one.HistoryFacts.Query.Casts().Where(f => f.DbfId == Cards.FromId("CORE_BOT_451").AssetId && f.Outcome == HistoryCastOutcome.Completed).ToArray();
            Core2026Proof.Check(casts.Length == 3 && casts.All(f => f.CasterKind == HistoryCastKind.Effect), "Bulb casts three exact-tier spells using normal automatic path");
        });
    }
}
