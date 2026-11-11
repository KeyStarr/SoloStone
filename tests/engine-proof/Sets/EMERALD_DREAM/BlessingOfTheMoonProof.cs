using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BlessingOfTheMoonProof
{
    private static void Check(bool value, string label) => Core2026Proof.Check(value, "Moon: " + label);
    private static OfflinePoolFilter Filter(CardType type) => new(classMode: PoolClassMode.Exact,
        exactClass: CardClass.PRIEST, type: type);
    private static Game Fixture(int seed, int n = 1)
    {
        var g = ImbueProof.Make(CardClass.PRIEST, seed);
        ImbueRules.Imbue(g.Player1, g.Player1.Hero, n);
        Check(g.Player1.Hero.HeroPower.Card.Id == "EDR_449p", "Priest installs Moon");
        return g;
    }
    private static Card[] Candidates(Game g, CardType type, int bound = int.MaxValue) =>
        OfflineCardPools.Candidates(g, CardClass.PRIEST, Filter(type).WithMaxCost(bound), 114069).ToArray();
    private static IPlayable[] Offers(Game g) => g.Player1.Choice.Choices.Select(id => g.IdEntityDic[id]).ToArray();
    private static void Answer(Game g, int id)
    {
        var d = g.GetPendingDecisions().Single();
        Check(g.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { id }), out var error), error);
    }
    // Real admitted metadata only; keep Past rows so an empty Standard fixture remains valid.
    private static void Pool(Game g, params string[] ids)
    {
        var xml = OfflinePoolFixtures.Catalog();
        xml.Root.Element("Members").Elements("Card")
            .Where(c => (string)c.Attribute("scope") == "Standard" && !ids.Contains((string)c.Attribute("cardId"))).Remove();
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, OfflinePoolFixtures.Manifest(xml));
    }
    internal static void Run()
    {
        Identity();
        foreach (int seed in new[] { 1, 2, 3 })
        {
            foreach (int n in new[] { 1, 2, 5 })
            {
                Lean_MoonPlayableAffordable(seed, n);
                Lean_MoonTemporaryDiscount(seed, n);
            }
            Lean_MoonPrintedCostAffordability(seed);
            Lean_MoonOfferShape(seed);
            Lean_MoonEmptyPools(seed);
            Lean_MoonFullHandDiscover(seed);
            Lean_MoonChoiceSnapshot(seed);
        }
        Console.WriteLine("PASS Moon: Lean_MoonPlayableAffordable; Lean_MoonPrintedCostAffordability; Lean_MoonOfferShape; Lean_MoonEmptyPools; Lean_MoonFullHandDiscover; Lean_MoonTemporaryDiscount; Lean_MoonChoiceSnapshot; seeds=1..3");
    }
    private static void Identity()
    {
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        foreach (string id in new[] { "EDR_449p", "GBL_999e" })
        {
            var data = doc.RootElement.GetProperty("cards").EnumerateArray().Single(c => c.GetProperty("cardId").GetString() == id);
            var card = Cards.FromId(id);
            Check(card.Implemented && EffectModules.ByDbfId[card.AssetId] == id && card.AssetId == data.GetProperty("dbfId").GetInt32()
                && card.Cost == data.GetProperty("cost").GetInt32() && card.ATK == data.GetProperty("atk").GetInt32()
                && card.Health == data.GetProperty("health").GetInt32() && card.Text == data.GetProperty("text").GetString(), "pinned identity/stats/text " + id);
            foreach (var tag in data.GetProperty("tags").EnumerateObject())
                Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) == tag.Value.GetInt32(), "pinned keyword/tag " + id + "/" + tag.Name);
        }
        Check(Cards.FromId("EDR_449p").ExcludeFromLegacyRandomPools, "power excluded from legacy pools");
        var declaration = OfflinePoolDeclarations.Cards.Single(d => d.CardId == "EDR_449p");
        Check(declaration.DbfId == 114069 && declaration.Pools.Select(p => p.Name).SequenceEqual(new[] { "moon-minion", "moon-spell" })
            && declaration.Pools.All(p => p.ClassDomain.SequenceEqual(new[] { "PRIEST" })
                && p.CostBinding == SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumberMaxCost && p.OrdinaryCost == 1), "pool declarations");
        Check(declaration.Pools[0].Filter == Filter(CardType.MINION) && declaration.Pools[1].Filter == Filter(CardType.SPELL),
            "exact Priest MINION/SPELL templates");
        var report = OfflinePoolAdmission.Evaluate(OfflinePoolPolicy.Load().BuildManifest()).Cards.Single(d => d.CardId == "EDR_449p");
        Check(report.PoolRequirements.Count == 4 && report.PoolRequirements.All(r => r.Count >= 3), "both ordinary/static pools meet admission");
        Console.WriteLine("PASS Moon pools: " + string.Join("; ", report.PoolRequirements.Select(r => r.Name + "/" + r.Binding + "=" + r.Count)));
    }
    private static void Lean_MoonPlayableAffordable(int seed, int n)
    {
        // imbue-20 lean: equality after HP payment, with N=1/2/5; no target or board-space filter.
        foreach (int payment in new[] { 0, 2 })
        {
            var g = Fixture(seed, n); var p = g.Player1;
            const int remaining = 2;
            p.BaseMana = remaining + payment;
            if (payment == 0) CostRules.AddNextHeroPowerRule(p, setCost: 0);
            var minion = Candidates(g, CardType.MINION).First(c => c.Cost == n + remaining);
            var expensive = Candidates(g, CardType.MINION).First(c => c.Cost > n + remaining);
            var spell = Candidates(g, CardType.SPELL).First(c => c.Cost <= n + remaining);
            Pool(g, minion.Id, expensive.Id, spell.Id);
            LostCityPoolProof.FullBoard(p);
            Check(g.Process(HeroPowerTask.Any(p)), "paid/discounted power accepted on full physical board");
            Check(p.UsedMana == payment && p.RemainingMana == remaining && p.NumTimesHeroPowerUsedThisGame == 1
                && p.Hero.HeroPower.IsExhausted && p.BoardZone.SlotCount == 7, "actual power payment/use/exhaustion");
            Check(Offers(g).Select(c => c.Card.Id).SequenceEqual(new[] { minion.Id, spell.Id }), "boundary minion admitted; above bound excluded");
            Answer(g, Offers(g)[0].Id);
            Check(p.HandZone.Single().Cost == remaining, "boundary chosen cost exactly remaining mana");
        }
        // A targeted spell stays offerable without any legal target.
        var targetless = Fixture(seed, n); var q = targetless.Player1;
        var targeted = Candidates(targetless, CardType.SPELL).First(c => c.Id == "CORE_CS2_004");
        Pool(targetless, targeted.Id); q.BaseMana = targeted.Cost + 2;
        Check(!Entity.FromCard(q, targeted).IsPlayableByCardReq, "Power Word Shield requires a minion target on empty boards");
        Check(targetless.Process(HeroPowerTask.Any(q)) && Offers(targetless).Single().Card.Id == targeted.Id,
            "no target-legality filter");
    }
    private static void Lean_MoonPrintedCostAffordability(int seed)
    {
        // Named deviation, imbue-20: candidate affordability uses printed cost, ignoring hand cost auras.
        var g = Fixture(seed); var p = g.Player1; p.BaseMana = 2;
        var cheap = Candidates(g, CardType.MINION).First(c => c.Cost == 1);
        var expensive = Candidates(g, CardType.MINION).First(c => c.Cost == 2);
        Summon(p, "EX1_076"); // Pint-Sized Summoner discounts first minion in hand.
        var probe = Add(p, expensive.Id); g.DeathProcessingAndAuraUpdate();
        Check(probe.Cost == 1, "real hand aura would make above-bound minion affordable");
        p.SetasideZone.Add(p.HandZone.Remove(probe)); Pool(g, cheap.Id, expensive.Id);
        Check(g.Process(HeroPowerTask.Any(p)) && Offers(g).Single().Card.Id == cheap.Id, "printed bound ignores hand aura");
    }
    private static void Lean_MoonOfferShape(int seed)
    {
        var g = Fixture(seed, 2); var p = g.Player1;
        var reference = g.CloneExact();
        var minion = OfflineCardPools.Pick(reference, Filter(CardType.MINION).WithMaxCost(10), CardClass.PRIEST, 114069);
        var spell = OfflineCardPools.Pick(reference, Filter(CardType.SPELL).WithMaxCost(10), CardClass.PRIEST, 114069);
        Check(g.Process(HeroPowerTask.Any(p)), "two-offer activation");
        var offers = Offers(g);
        Check(offers.Select(o => o.Card.Id).SequenceEqual(new[] { minion.Id, spell.Id })
            && g.Random.CaptureState().Equals(reference.Random.CaptureState()), "minion then spell, exactly two deterministic picks");
        Check(p.Choice.SourceId == p.Hero.HeroPower.Id && p.Choice.ChoiceAction == ChoiceAction.STACK
            && offers.Select(o => o.Id).Distinct().Count() == 2 && offers.Select(o => o.Card.Id).Distinct().Count() == 2, "HP source, distinct entities/definitions");
        foreach (var offer in offers)
            Check(offer.Zone == p.SetasideZone && CardClassRules.IsAllowedInDeck(offer.Card, CardClass.PRIEST) && offer.Card.Collectible
                && offer[GameTag.CREATOR] == p.Hero.HeroPower.Id && offer[GameTag.DISPLAYED_CREATOR] == p.Hero.HeroPower.Id
                && offer[Generic.PrivateDecisionOfferTag] == p.PlayerId && offer.Cost == offer.Card.Cost
                && !TemporaryRules.IsTemporary(offer), "private generated offers preserve printed cost until selected");
        var offeredMinion = (Minion)offers[0];
        Check(offeredMinion.AttackDamage == minion.ATK && offeredMinion.Health == minion.Health
            && offeredMinion.HasTaunt == minion.Taunt, "generated minion has definition stats/keyword");
        var d = g.GetPendingDecisions().Single(); var bytes = g.SaveSnapshot();
        foreach (var answer in new[] { new DecisionAnswer(d.Id, p.Opponent.Id, new[] { offers[0].Id }),
            new DecisionAnswer(d.Id, d.ControllerId, new[] { int.MaxValue }), new DecisionAnswer(d.Id, d.ControllerId, Array.Empty<int>()) })
            Check(!g.TryAnswer(answer, out _) && bytes.SequenceEqual(g.SaveSnapshot()), "invalid answer leaves state/RNG unchanged");
        foreach (var type in new[] { CardType.MINION, CardType.SPELL })
        {
            var single = Fixture(seed); var card = Candidates(single, type, 1).First(); Pool(single, card.Id);
            Check(single.Process(HeroPowerTask.Any(single.Player1)) && Offers(single).Single().Card.Id == card.Id,
                "single nonempty type still prompts");
        }
        // Copied power still uses exact Priest domain; borrowed activation spends no mana/use and remains exhausted.
        var copied = ImbueProof.Make(CardClass.MAGE, seed); var c = copied.Player1;
        new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("EDR_449p")).Process(copied, c, c.Hero, null);
        c.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] = 2; c.Hero.HeroPower.IsExhausted = true;
        Check(ImbueRules.TriggerCurrentHeroPower(c) == TaskState.COMPLETE && c.Choice != null
            && c.Choice.Choices.All(i => CardClassRules.IsAllowedInDeck(copied.IdEntityDic[i].Card, CardClass.PRIEST))
            && c.UsedMana == 0 && c.NumTimesHeroPowerUsedThisGame == 0 && c.Hero.HeroPower.IsExhausted,
            "copied/borrowed Moon uses Priest pool without payment/use/refresh");
    }
    private static void Lean_MoonEmptyPools(int seed)
    {
        // imbue-21 lean: no offers still pays/exhausts; empty picks consume no RNG/entities.
        foreach (bool negative in new[] { false, true })
        {
            var g = Fixture(seed); var p = g.Player1;
            if (negative) p.UsedMana = p.BaseMana + 1; else Pool(g);
            var rng = g.Random.CaptureState(); int entities = g.IdEntityDic.Count;
            if (negative) Check(ImbueRules.TriggerCurrentHeroPower(p) == TaskState.COMPLETE, "negative mana borrowed effect completes");
            else Check(g.Process(HeroPowerTask.Any(p)) && p.UsedMana == 2 && p.Hero.HeroPower.IsExhausted, "empty paid activation");
            Check(p.Choice == null && p.HandZone.IsEmpty && g.IdEntityDic.Count == entities
                && rng.Equals(g.Random.CaptureState()), "empty/negative leaves no choice, RNG, or generated entities");
        }
        var overflow = Fixture(seed); var q = overflow.Player1;
        q.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] = int.MaxValue;
        Check(overflow.Process(HeroPowerTask.Any(q)) && Offers(overflow).Length == 2, "long bound clamps without wrapping");
    }
    private static void Lean_MoonFullHandDiscover(int seed)
    {
        foreach (int capacity in new[] { 10, 11 })
        {
            var g = Fixture(seed); var p = g.Player1;
            p[GameTag.MAXHANDSIZE] = capacity;
            for (int i = 0; i < capacity; i++) Add(p, "CS2_182");
            LostCityPoolProof.FullBoard(p); var held = p.HandZone.Select(c => c.Id).ToArray();
            int entities = g.IdEntityDic.Count; var rng = g.Random.CaptureState();
            Check(!p.Hero.HeroPower.IsPlayable
                && p.Options().All(t => t.PlayerTaskType != PlayerTaskType.HERO_POWER)
                && !g.Process(HeroPowerTask.Any(p)), "full hand rejects Moon activation/options");
            Check(p.HandZone.Select(c => c.Id).SequenceEqual(held) && p.UsedMana == 0
                && p.NumTimesHeroPowerUsedThisGame == 0 && p.HeroPowerActivationsThisTurn == 0
                && !p.Hero.HeroPower.IsExhausted && p.Choice == null && g.Rewind == null
                && g.IdEntityDic.Count == entities && rng.Equals(g.Random.CaptureState()),
                "rejected Moon preserves hand, payment, uses, exhaustion, choices, entities and RNG");
            // Effects that trigger the power directly still choose and burn into a full hand.
            Check(ImbueRules.TriggerCurrentHeroPower(p) == TaskState.COMPLETE && Offers(g).Length == 2,
                "full-hand direct Moon effect still offers");
            var chosen = Offers(g)[0]; var discards = p.DiscardedEntities.ToArray();
            Answer(g, chosen.Id);
            Check(chosen.Zone == p.GraveyardZone && p.HandZone.Select(c => c.Id).SequenceEqual(held)
                && p.DiscardedEntities.SequenceEqual(discards) && p.Choice == null && p.UsedMana == 0,
                "direct Moon selection burns without discard, replacement, pending choice or payment");
            p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.Last()));
            Check(p.Hero.HeroPower.IsPlayable && g.Process(HeroPowerTask.Any(p)) && Offers(g).Length == 2,
                "one free slot permits Moon activation and choice");
            chosen = Offers(g).First(c => c.Card.Shatter == null);
            Answer(g, chosen.Id);
            Check(chosen.Zone == p.HandZone && p.HandZone.Count == capacity && p.UsedMana == 2
                && p.NumTimesHeroPowerUsedThisGame == 1 && p.HeroPowerActivationsThisTurn == 1
                && p.Hero.HeroPower.IsExhausted && p.Choice == null && p.BoardZone.SlotCount == 7,
                "Moon fills last slot and settles one payment/use with choice cleared");
        }
    }
    private static void Lean_MoonTemporaryDiscount(int seed, int n)
    {
        // imbue-22 lean: permanent -N then C Temporary, selected entity rather than a replacement.
        foreach (string cardId in new[] { "CORE_RLK_814", "CORE_BAR_310" })
        {
            var g = Fixture(seed, n); var p = g.Player1;
            var card = Candidates(g, CardType.MINION).Single(c => c.Id == cardId);
            int cost = card.Cost;
            Pool(g, card.Id); Check(g.Process(HeroPowerTask.Any(p)), "Temporary activation");
            var chosen = Offers(g).Single(); Answer(g, chosen.Id);
            Check(p.HandZone.Single().Id == chosen.Id && chosen.Cost == Math.Max(0, cost - n) && chosen[GameTag.COST] == chosen.Cost
                && TemporaryRules.IsTemporary(chosen) && chosen.AppliedEnchantments.Single().Card.Id == "GBL_999e", "selected discount/floor/Temporary helper");
            var copy = Generic.Copy(p, p.Hero, chosen, Zone.HAND);
            Check(copy.Cost == chosen.Cost && TemporaryRules.IsTemporary(copy)
                && copy.AppliedEnchantments.Single().Id != chosen.AppliedEnchantments.Single().Id, "copy independent Temporary and discount");
            var play = g.CloneExact(); var played = play.IdEntityDic[chosen.Id];
            Check(play.Process(PlayCardTask.Any(play.Player1, played)) && played.Zone == play.Player1.BoardZone
                && !TemporaryRules.IsTemporary(played) && played.AppliedEnchantments.All(e => e.Card.Id != "GBL_999e"), "normal play removes Temporary helper");
            var discards = new List<int>(); SabberStoneCore.Model.TriggerManager.TriggerHandler handler = e => discards.Add(e.Id);
            g.TriggerManager.DiscardTrigger += handler; TemporaryRulesProof.End(g); g.TriggerManager.DiscardTrigger -= handler;
            Check(chosen.Zone == p.GraveyardZone && copy.Zone == p.GraveyardZone && discards.SequenceEqual(new[] { chosen.Id, copy.Id })
                && p.DiscardedEntities.Contains(chosen.Id) && !TemporaryRules.IsTemporary(chosen), "owner end performs real ordered discard");
            TemporaryRulesProof.End(play);
            Check(played.Zone == play.Player1.BoardZone, "played card survives Temporary end boundary");
        }
    }
    private static void Lean_MoonChoiceSnapshot(int seed)
    {
        var g = Fixture(seed, 2); var p = g.Player1;
        Check(g.Process(HeroPowerTask.Any(p)), "pending choice activation");
        int selected = Offers(g)[0].Id; string[] offered = Offers(g).Select(o => o.Card.Id).ToArray();
        var bytes = g.SaveSnapshot(); var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "pending choice byte round trip");
        foreach (var child in new[] { clone, loaded })
        {
            Check(Offers(child).Select(o => o.Card.Id).SequenceEqual(offered)
                && child.Random.CaptureState().Equals(g.Random.CaptureState()), "pending IDs and RNG preserved");
            ImbueRules.Imbue(child.Player1, child.Player1.Hero, 3);
            new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("CS2_034"))
                .Process(child, child.Player1, child.Player1.Hero, null);
            Answer(child, selected);
            var card = child.Player1.HandZone.Single();
            Check(card.Id == selected && card.Cost == Math.Max(0, card.Card.Cost - 2) && TemporaryRules.IsTemporary(card),
                "captured N survives later Imbue and source leaving play");
        }
        Check(bytes.SequenceEqual(g.SaveSnapshot()), "answering clones never mutates parent callback/state");
        Answer(g, selected);
        Check(g.Player1.HandZone.Single().Cost == Math.Max(0, g.IdEntityDic[selected].Card.Cost - 2), "shared callback remains immutable");
        Check(Canonical.State(clone) == Canonical.State(loaded), "clone/load future state matches");
    }
}
