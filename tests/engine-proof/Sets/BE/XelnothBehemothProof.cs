using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;
using static StandardSetsProof;

internal static class XelnothBehemothProof
{
    internal static void Run() => StandardSetsProof.Case("BE_749", (g, p, id) => {
        g = GraftedKnightProof.NewGame(g); p = g.Player1;
        SourceExcluded(g, id);
        Core2026Proof.Check(Cards.FromId("BE_749e").AssetId == 132646, "export buff identity");
        var left = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(p.Options().First(t => t.Source?.Id == source.Id && t is PlayCardTask play && play.ZonePosition == 1)), "play in middle slot");
        var location = LocationProofs.Location(p);
        var right = Summon(p, "CS2_182");
        var hand = Add(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(p.BoardZone.Count == 3 && left.AttackDamage == 4, "Battlecry does not summon or buff");
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        var random = g.Random.Clone();
        var expected = new[] { pool[random.Next(pool.Count)].Id, pool[random.Next(pool.Count)].Id };
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var generated = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        Core2026Proof.Check(generated.Select(m => m.Card.Id).SequenceEqual(expected) && generated.All(m => m.Card.Cost == 1
            && m.AttackDamage >= m.Card.ATK + 3 && m.AppliedEnchantments.Any(e => e.Card.Id == "BE_749e")), "two admitted independent picks, then +3 Attack");
        Core2026Proof.Check(p.BoardZone.GetSlot(0).Id == left.Id && p.BoardZone.GetSlot(1).Id == generated[0].Id
            && p.BoardZone.GetSlot(2).Id == generated[1].Id && p.BoardZone.GetSlot(3).Id == location.Id
            && p.BoardZone.GetSlot(4).Id == right.Id, "summons replace source before neighboring location");
        Core2026Proof.Check(left.AttackDamage == 7 && right.AttackDamage == 7 && ((Minion)hand).AttackDamage == 4
            && enemy.AttackDamage == 4 && g.Random.CaptureState().Equals(random.CaptureState()), "buff only friendly board; exact two draws");
        GraftedKnightProof.ClearMinions(g, p);
        Pool(g, id, 0, () => {
            var survivor = Summon(p, "CS2_182"); source = Summon(p, id);
            var before = g.Random.CaptureState(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 1 && survivor.AttackDamage == 7 && g.Random.CaptureState().Equals(before), "empty pool preserves printed buff without RNG");
        });
        GraftedKnightProof.ClearMinions(g, p);
        Pool(g, id, 1, () => {
            source = Summon(p, id); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1, "singleton pool allows duplicates");
        });
        GraftedKnightProof.ClearMinions(g, p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.IsEmpty, "Silence removes entire Deathrattle");
        source = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count == 6
            && p.BoardZone.Count(m => m[GameTag.DISPLAYED_CREATOR] == source.Id) == 1, "one free physical slot summons only one");
        var attacks = p.BoardZone.Select(m => m.AttackDamage).ToArray();
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Select(m => m.AttackDamage).SequenceEqual(attacks.Select((a, i) => a + (p.BoardZone[i].IsDormant ? 0 : 3))), "full mixed board still buffs: " + string.Join(",",p.BoardZone.Select((m,i) => m.Card.Id + ":" + attacks[i] + "->" + m.AttackDamage)));
        LivingBearer(g, id, copied: false);
        LivingBearer(g, id, copied: true);
        Sequential(g, id);
        RandomInterleaving(g, id);
    });

    private static void LivingBearer(Game seeded, string id, bool copied)
    {
        var g = GraftedKnightProof.NewGame(seeded); var p = g.Player1;
        var left = Summon(p, "CS2_182");
        var original = Summon(p, id);
        var source = copied ? (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY) : original;
        var location = LocationProofs.Location(p);
        var right = Summon(p, "CS2_182");
        int slot = source.ZonePosition;
        Resume(g, next => {
            var owner = next.Player1;
            var swimmer = Add(owner, "JAIL_395");
            Core2026Proof.Check(next.Process(owner.Options().First(t => t.Source?.Id == swimmer.Id
                && t.Target?.Id == source.Id && t is PlayCardTask play && play.ZonePosition == owner.BoardZone.SlotCount)),
                "Sewer Swimmer plays at physical right edge");
            var generated = owner.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
            int expected = copied ? 1 : 2; // Sewer Swimmer also occupies a physical slot.
            Core2026Proof.Check(next.IdEntityDic[source.Id].Zone == owner.BoardZone
                && owner.BoardZone.GetSlot(slot).Id == source.Id && generated.Length == expected
                && Enumerable.Range(0, expected).All(i => owner.BoardZone.GetSlot(slot + 1 + i).Id == generated[i].Id)
                && owner.BoardZone.GetSlot(slot + 1 + expected).Id == location.Id,
                "living/copy bearer summons to its right before location; physical full-board cap");
        });
        int minions = g.IdEntityDic.Values.OfType<Minion>().Count();
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.IsFull && g.IdEntityDic.Values.OfType<Minion>().Count() == minions,
            "living bearer on full mixed board creates no summon entities");
    }

    private static void Sequential(Game seeded, string id)
    {
        var g = GraftedKnightProof.NewGame(seeded); var p = g.Player1;
        Summon(p.Opponent, "EDR_815");
        Pool(g, id, 1, () => {
            var source = Summon(p, id); CorpseRules.Gain(p.Opponent, 4); Kill(g, source);
            var outputs = p.GraveyardZone.OfType<Minion>().Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
            Core2026Proof.Check(outputs.Length == 2 && CorpseRules.Available(p.Opponent) == 0
                && outputs.All(m => m.Damage >= 3) && p.BoardZone.IsEmpty,
                "each summon runs enemy trigger before next summon and final buff");
        });
    }

    private static void RandomInterleaving(Game seeded, string id)
    {
        var g = GraftedKnightProof.NewGame(seeded); var p = g.Player1;
        Summon(p.Opponent, "CS2_200"); Summon(p.Opponent, "CS2_200");
        Summon(p, "NEW1_019");
        var source = Summon(p, id);
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        var random = g.Random.Clone();
        string first = pool[random.Next(pool.Count)].Id;
        random.Next(3); // Knife Juggler picks among two surviving enemy minions and their hero.
        string second = pool[random.Next(pool.Count)].Id;
        random.Next(3);
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id)
            .Select(m => m.Card.Id).SequenceEqual(new[] { first, second })
            && g.Random.CaptureState().Equals(random.CaptureState()), "summon-trigger RNG interleaves the two pool picks");
    }
}
