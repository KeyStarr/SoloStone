using PegasusGame;
using EH = SabberStoneCore.Kettle.PowerHistoryBuilder;
using EM = SabberStoneCore.Kettle.PowerHistoryMetaData;
using EB = SabberStoneCore.Kettle.PowerHistoryBlockStart;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;
using Tag = PegasusGame.Tag;

internal static partial class HistoryShapeProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "history-shape " + message);
    private static PowerHistoryData Start(int type = 3, int source = 10, int target = 11, int keyword = 0) => new() {
        PowerStart = new() { Type = (HistoryBlock.Type)type, Source = source, Target = target, EffectIndex = 0, TriggerKeyword = keyword }
    };
    private static PowerHistoryData End() => new() { PowerEnd = new() };
    private static PowerHistoryData TC(int entity, GameTag tag, int value) => new() {
        TagChange = new() { Entity = entity, Tag = (int)tag, Value = value }
    };
    private static PowerHistoryData Meta(HistoryMeta.Type type, int amount, params int[] ids) => new() {
        MetaData = new() { Type = type, Data = amount, Info = ids.ToList() }
    };
    private static PowerHistoryData Full(int id, string card, CardType type = CardType.MINION, Zone zone = Zone.PLAY, int owner = 1, int damage = 0) => new() {
        FullEntity = new() { Entity = id, Name = card, Tags = new() {
            new Tag { Name = (int)GameTag.ENTITY_ID, Value = id },
            new Tag { Name = (int)GameTag.CARDTYPE, Value = (int)type }, new Tag { Name = (int)GameTag.ZONE, Value = (int)zone },
            new Tag { Name = (int)GameTag.CONTROLLER, Value = owner }, new Tag { Name = (int)GameTag.DAMAGE, Value = damage }
        } }
    };
    private static PowerHistoryData Show(int id, string card, Zone zone, int owner = 2)
    {
        var full = Full(id, card, zone: zone, owner: owner);
        return new() { ShowEntity = full.FullEntity };
    }
    private static HistoryShape Seed()
    {
        var shape = new HistoryShape();
        Feed(shape, Full(10, "CS2_029", CardType.SPELL, Zone.GRAVEYARD), Full(11, "CS2_182", damage: 5),
            Full(12, "HERO_01", CardType.HERO), Full(13, "CS2_200"), Full(14, "HERO_09bp", CardType.HERO_POWER),
            Full(15, "CS2_120"), Full(16, "", zone: Zone.DECK, owner: 2));
        return shape;
    }
    private static void Feed(HistoryShape shape, params PowerHistoryData[] entries) => shape.Observe(new() { List = entries.ToList() });
    private static HistoryShape.Hit Hit(int source = 10, int amount = 3, bool heal = false, bool fatigue = false, bool lifesteal = false, bool selected = false) => new() {
        Source = source, SourceCard = source == 15 ? "CS2_120" : "CS2_029", Victim = 11, Amount = amount,
        Heal = heal, Fatigue = fatigue, Lifesteal = lifesteal, Selected = selected
    };
    private static string Variant(HistoryShapeFinding f) => f.Detail.Split(' ')[0];
    private static HistoryShapeFinding[] Splits(HistoryShape shape) => shape.Findings.Where(f => f.Rule == "SPLIT_EFFECT_BLOCK").ToArray();
    private static void SplitEffectBlocks()
    {
        var bone = Seed();
        Feed(bone, Full(20, "JAIL_445", CardType.SPELL, Zone.GRAVEYARD), Start(7, 20, 0), Start(3, 20, 0));
        for (int i = 0; i < 6; i++)
        {
            Feed(bone, Start(3, 20, 0));
            Feed(bone, Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 6 + i), End());
        }
        Feed(bone, End(), End()); bone.Finish();
        var split = bone.Findings.Where(f => f.Rule == "SPLIT_EFFECT_BLOCK").ToArray();
        Check(split.Count(f => f.Card == "JAIL_445" && Variant(f) == "nested-in-POWER" && f.Severity == "error") == 6,
            "Bone Flurry counts each nested missile block once across packets");
        Check(split.Count(f => Variant(f) == "sibling-POWER") == 5, "Bone Flurry also counts later sibling blocks");
        Check(split.All(f => f.Detail.EndsWith(" under=POWER header=same meta=TARGET+DAMAGE", StringComparison.Ordinal)),
            "Bone Flurry details name parent, header identity and metadata shape");

        var flat = Seed();
        Feed(flat, Full(20, "MEND_302"), Start(7, 20, 0), Start(3, 20, 0));
        for (int i = 0; i < 6; i++)
            Feed(flat, Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 6 + i));
        Feed(flat, End(), End()); flat.Finish();
        Check(!flat.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK"), "flat Wasteland effect has no split");

        var shuffle = Seed();
        Feed(shuffle, Start(7, 10, 0), Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11),
            Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), Start(3, 10, 0),
            Full(20, "", zone: Zone.DECK), Show(20, "CS2_182", Zone.DECK, owner: 1),
            new PowerHistoryData { HideEntity = new() { Entity = 20, Zone = (int)Zone.DECK } },
            Meta(HistoryMeta.Type.TARGET, 0, 20), End(), End());
        shuffle.Finish();
        Check(Splits(shuffle).Length == 0, "retail post-hide DECK TARGET is a shuffle phase, not another missile");

        var trigger = Seed();
        Feed(trigger, Start(7, 10, 0), Start(5, 10, 0), Start(3, 10, 0));
        Feed(trigger, Meta(HistoryMeta.Type.HEALING, 1, 11), End(), End(), Start(3, 10, 0));
        Feed(trigger, Meta(HistoryMeta.Type.TARGET, 10, 11), End(), End()); trigger.Finish();
        Check(trigger.Findings.Count(f => f.Rule == "SPLIT_EFFECT_BLOCK" && Variant(f) == "nested-in-TRIGGER") == 1
            && trigger.Findings.Count(f => f.Rule == "SPLIT_EFFECT_BLOCK" && Variant(f) == "sibling-TRIGGER") == 1,
            "TRIGGER parent and earlier sibling survive packet boundaries, including mixed block types");

        foreach (int earlierType in new[] { 3, 5 })
        {
            var root = Seed();
            Feed(root, Start(earlierType, 10, 0), End());
            Feed(root, Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11), End());
            root.Finish();
            Check(root.Findings.Count(f => f.Rule == "SPLIT_EFFECT_BLOCK"
                && Variant(f) == (earlierType == 3 ? "root-sibling-POWER" : "root-sibling-TRIGGER")) == 1,
                "root continuation catches empty activation followed by queued effect across packets");
        }
        var actions = Seed();
        Feed(actions, Start(5, 10, 0), End());
        actions.Step++;
        Feed(actions, Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11), End());
        Feed(actions, Start(5, 10, 0), End(), Start(3, 15, 0), Meta(HistoryMeta.Type.TARGET, 15, 11), End());
        Feed(actions, Start(5, 10, 0), End(), Start(7, 10, 0), End(), Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11), End());
        Feed(actions, Start(5, 10, 0), End(), TC(10, GameTag.EXHAUSTED, 1), Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11), End());
        actions.Finish();
        Check(!actions.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK"),
            "root negatives: separate player actions, different sources, intervening root block or entry");

        var separate = Seed();
        Feed(separate, Start(7, 10, 0), Start(3, 10, 0), Start(5, 15, 0), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(),
            End(), Start(3, 10, 0), End(), End(), Start(7, 10, 0), Start(3, 10, 0),
            Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End()); separate.Finish();
        Check(!separate.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK"),
            "different source, metadata-free block, indirect metadata and separate parents do not split");
        Console.WriteLine("PASS history-shape split effect blocks: nested Bone Flurry, flat Wasteland, TRIGGER and scope boundaries");
    }
    private static void SplitKeywordBlocksAreIndependent()
    {
        foreach (var (keyword, effectIndex) in new[] { (685, 0), (363, 0), (363, -1), (0, -1) })
        {
            PowerHistoryData Keyword()
            {
                var entry = Start(5, 10, 0, keyword);
                entry.PowerStart.EffectIndex = effectIndex;
                return entry;
            }
            var shape = Seed();
            Feed(shape, Full(10, "CORE_BAR_311", CardType.SPELL, Zone.GRAVEYARD), Start(7, 10, 0), Start(3, 10, 0),
                Meta(HistoryMeta.Type.DAMAGE, 1, 11), Keyword(), Meta(HistoryMeta.Type.HEALING, 1, 12), End(),
                Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), Keyword(), Meta(HistoryMeta.Type.HEALING, 1, 12), End(), End());
            shape.Step++;
            // An excluded enclosing/earlier block must not make the flat POWER a continuation either.
            Feed(shape, Start(7, 10, 0), Keyword(), Start(3, 10, 0), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End(),
                Start(3, 10, 0), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End());
            shape.Step++;
            Feed(shape, Keyword(), End(), Start(3, 10, 0), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
            shape.Finish();
            Check(!shape.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK"),
                $"keyword/attribution {keyword}/{effectIndex} is neither split block nor enclosing/earlier activation");
        }
        Console.WriteLine("PASS history-shape SplitKeywordBlocksAreIndependent");
    }
    private static void SplitAcrossKeywordWrappers()
    {
        foreach (var (keyword, effectIndex) in new[] { (685, 0), (363, 0), (363, -1), (0, -1) })
        {
            PowerHistoryData Keyword(int source = 20)
            {
                var entry = Start(5, source, 0, keyword);
                entry.PowerStart.EffectIndex = effectIndex;
                return entry;
            }
            string under = "TRIGGER" + (keyword != 0 ? "/kw" + keyword : "") + (effectIndex < 0 ? "/ei" + effectIndex : "");
            // Wrapping each missile in a keyword/attribution block must not hide it from the enclosing activation.
            var wrapped = Seed();
            Feed(wrapped, Full(20, "JAIL_445", CardType.SPELL, Zone.GRAVEYARD), Start(7, 20, 0), Start(3, 20, 0));
            for (int i = 0; i < 3; i++)
                Feed(wrapped, Keyword(), Start(3, 20, 0), Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End());
            Feed(wrapped, End(), End()); wrapped.Finish();
            Check(Splits(wrapped).Length == 3 && Splits(wrapped).All(f => f.Card == "JAIL_445"
                && f.Detail == $"nested-in-POWER under={under} header=same meta=TARGET+DAMAGE"),
                $"{keyword}/{effectIndex} wrapper does not hide nested missiles; found={string.Join(';', Splits(wrapped).Select(f => f.Detail))}");
            // A missile hit emitted directly inside a same-source wrapper is the activation's damage.
            var direct = Seed();
            Feed(direct, Full(20, "JAIL_445", CardType.SPELL, Zone.GRAVEYARD), Start(7, 20, 0), Start(3, 20, 0),
                Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11));
            for (int i = 0; i < 2; i++) Feed(direct, Keyword(), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
            Feed(direct, End(), End()); direct.Finish();
            Check(Splits(direct).Length == 2 && Splits(direct).All(f => f.Detail == $"keyword-hit-in-POWER block={under} under=POWER meta=DAMAGE"),
                $"{keyword}/{effectIndex} wrapper DAMAGE is a split hit; found={string.Join(';', Splits(direct).Select(f => f.Detail))}");
            // The wrapper's own effect (Lifesteal heal, Poisonous target) and other sources' wrappers stay clean.
            var own = Seed();
            Feed(own, Full(20, "JAIL_445", CardType.SPELL, Zone.GRAVEYARD), Start(7, 20, 0), Start(3, 20, 0),
                Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11),
                Keyword(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.HEALING, 1, 12), End(),
                Keyword(15), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End(), End()); own.Finish();
            Check(Splits(own).Length == 0, $"{keyword}/{effectIndex} wrapper's own effect and foreign wrappers are not split hits");
            // A hit wrapper emitted after its activation closed: as a later sibling, or as the next root block.
            var after = Seed();
            Feed(after, Full(20, "JAIL_445", CardType.SPELL, Zone.GRAVEYARD), Start(7, 20, 0), Start(3, 20, 0),
                Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(),
                Keyword(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End());
            after.Step++;
            Feed(after, Start(3, 20, 0), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), Keyword(), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
            after.Step++;
            Feed(after, Start(3, 20, 0), End()); after.Step++; Feed(after, Keyword(), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
            after.Finish();
            Check(Splits(after).Select(f => f.Detail).SequenceEqual(new[] {
                $"keyword-hit-after-POWER block={under} under=PLAY meta=TARGET+DAMAGE", $"keyword-hit-after-root-POWER block={under} under=root meta=DAMAGE" }),
                $"{keyword}/{effectIndex} wrapper hit after its activation; separate actions do not; found={string.Join(';', Splits(after).Select(f => f.Detail))}");
        }
        // Auchenai-style conversion: Lifesteal's own-hero heal arrives as DAMAGE and is the wrapper's effect.
        var converted = Seed();
        Feed(converted, Full(20, "CORE_BAR_311", CardType.SPELL, Zone.GRAVEYARD), Full(30, "HERO_02", CardType.HERO, owner: 2), Start(7, 20, 0),
            Start(3, 20, 0), Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11),
            Start(5, 20, 0, 685), Meta(HistoryMeta.Type.DAMAGE, 1, 12), End(), Start(5, 20, 0, 685), Meta(HistoryMeta.Type.DAMAGE, 1, 30), End(),
            Start(5, 20, 0, 363), Meta(HistoryMeta.Type.DAMAGE, 1, 12), End(), End(), End()); converted.Finish();
        Check(Splits(converted).Select(f => f.Detail).SequenceEqual(new[] {
            "keyword-hit-in-POWER block=TRIGGER/kw685 under=POWER meta=DAMAGE", "keyword-hit-in-POWER block=TRIGGER/kw363 under=POWER meta=DAMAGE" }),
            "converted Lifesteal self-damage is exempt; enemy-hero or non-Lifesteal wrapper DAMAGE is not; found=" + string.Join(';', Splits(converted).Select(f => f.Detail)));
        // Hit-bearing TRIGGER continuations: nested below the activation, as later siblings, and at the root.
        var nested = Seed();
        Feed(nested, Full(20, "JAIL_445", CardType.SPELL, Zone.GRAVEYARD), Start(7, 20, 0), Start(3, 20, 0));
        for (int i = 0; i < 3; i++)
            Feed(nested, Start(5, 20, 0), Meta(HistoryMeta.Type.TARGET, 20, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
        Feed(nested, End(), End()); nested.Finish();
        Check(Splits(nested).Count(f => f.Detail == "nested-in-POWER under=POWER header=differs meta=TARGET+DAMAGE") == 3
            && Splits(nested).Count(f => f.Detail == "sibling-TRIGGER under=POWER header=same meta=TARGET+DAMAGE") == 2
            && Splits(nested).Length == 5, "TRIGGER missile continuations are nested and sibling splits");
        var root = Seed();
        Feed(root, Start(5, 10, 0, 217), End());
        Feed(root, Start(5, 10, 0, 217), Meta(HistoryMeta.Type.TARGET, 10, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
        Feed(root, Start(5, 10, 0, 217), End(), Start(5, 10, 15, 217), Meta(HistoryMeta.Type.TARGET, 10, 11), End());
        root.Finish();
        Check(Splits(root).Length == 1 && Splits(root)[0].Detail == "root-sibling-TRIGGER under=root header=same meta=TARGET+DAMAGE",
            "root TRIGGER continuation splits; a different TRIGGER event target does not");
        Console.WriteLine("PASS history-shape SplitAcrossKeywordWrappers: wrapped/direct keyword hits, TRIGGER continuations");
    }
    private static void SplitBaselineShapes()
    {
        // Synthetic classification cases must survive legitimate full-corpus baseline turnover.
        var baseline = new[] {
            new HistoryShapeBaseline { Rule = "SPLIT_EFFECT_BLOCK", Card = "CORE_EX1_319", Kind = "accepted",
                Reason = "Synthetic doubled Battlecry", Shapes = new[] { "sibling-POWER under=PLAY header=same meta=TARGET+DAMAGE" } },
            new HistoryShapeBaseline { Rule = "SPLIT_EFFECT_BLOCK", Card = "DAL_010", Kind = "accepted",
                Reason = "Synthetic separate copies", Shapes = new[] { "sibling-POWER under=PLAY header=same meta=TARGET" } }
        };
        // Uses the same classification as HistoryShapeReport.Write: accepted means the bucket is not new.
        bool Accepted(string card, Action<HistoryShape> feed)
        {
            var shape = Seed(); Feed(shape, Full(20, card), Start(7, 20, 0)); feed(shape); Feed(shape, End()); shape.Finish();
            var bucket = HistoryShapeReport.Bucket(shape.Findings, "SPLIT_EFFECT_BLOCK", card);
            Check(bucket.Count > 0, card + " synthetic stream splits");
            return HistoryShapeReport.Classify(new[] { bucket }, baseline, fullCorpus: false).Fresh.Length == 0;
        }
        void Hit(HistoryShape s, int target, params HistoryMeta.Type[] meta)
        {
            Feed(s, Start(3, 20, target));
            foreach (var type in meta) Feed(s, Meta(type, type == HistoryMeta.Type.TARGET ? 20 : 3, 12));
            Feed(s, End());
        }
        var hit = new[] { HistoryMeta.Type.TARGET, HistoryMeta.Type.DAMAGE };
        var target = new[] { HistoryMeta.Type.TARGET };
        var heal = new[] { HistoryMeta.Type.TARGET, HistoryMeta.Type.HEALING };
        // Flame Imp's reviewed shape is two identical sibling Battlecry POWERs below PLAY; nothing else.
        Check(Accepted("CORE_EX1_319", s => { Hit(s, 12, hit); Hit(s, 12, hit); }), "Flame Imp reviewed sibling shape accepted");
        Check(!Accepted("CORE_EX1_319", s => { Feed(s, Start(3, 20, 12)); Hit(s, 12, hit); Feed(s, End()); }),
            "Flame Imp entry rejects a nested-POWER split regression");
        Check(!Accepted("CORE_EX1_319", s => { Hit(s, 12, hit); Hit(s, 11, hit); }), "Flame Imp entry rejects differing headers");
        Check(!Accepted("CORE_EX1_319", s => { Feed(s, Start(3, 20, 12)); for (int i = 0; i < 2; i++) { Feed(s, Start(5, 20, 0, 685)); Hit(s, 12, hit); Feed(s, End()); } Feed(s, End()); }),
            "Flame Imp entry rejects Lifesteal-wrapped split hits");
        // Scheme's reviewed shape is TARGET-only in every participant, in either order.
        Check(Accepted("DAL_010", s => { Hit(s, 12, target); Hit(s, 12, target); }), "Scheme reviewed TARGET-only shape accepted");
        foreach (var other in new[] { hit, heal })
        {
            Check(!Accepted("DAL_010", s => { Hit(s, 12, target); Hit(s, 12, other); }), "Scheme entry rejects later split " + other[1]);
            Check(!Accepted("DAL_010", s => { Hit(s, 12, other); Hit(s, 12, target); }), "Scheme entry rejects earlier split " + other[1]);
        }
        Check(!Accepted("CS2_029", s => { Hit(s, 12, hit); Hit(s, 12, hit); }), "unlisted card is new");
        // Stale: a listed shape the complete corpus never produced; focused/replay runs never report stale.
        var imp = baseline.Single(e => e.Rule == "SPLIT_EFFECT_BLOCK" && e.Card == "CORE_EX1_319");
        HistoryShapeBucket Observed(params string[] shapes)
        {
            var bucket = new HistoryShapeBucket { Rule = imp.Rule, Severity = "error", Card = imp.Card, Count = shapes.Length };
            foreach (string shape in shapes) bucket.Shapes.Add(shape);
            return bucket;
        }
        var reviewed = HistoryShapeReport.Classify(new[] { Observed(imp.Shapes) }, baseline, fullCorpus: true);
        var drifted = HistoryShapeReport.Classify(new[] { Observed("nested-in-POWER under=POWER header=same meta=TARGET+DAMAGE") }, baseline, fullCorpus: true);
        Check(reviewed.Fresh.Length == 0 && !reviewed.Stale.Contains(imp), "reviewed corpus shape is neither new nor stale");
        Check(drifted.Fresh.Length == 1 && drifted.Stale.Contains(imp), "drifted shape is new and its unseen listed shape stale");
        Check(HistoryShapeReport.Classify(new[] { Observed("x") }, baseline, fullCorpus: false).Stale.Length == 0, "partial runs report no stale entries");
        // Split entries must list shapes; other rules must not.
        foreach (var (rule, shapes) in new[] { ("SPLIT_EFFECT_BLOCK", (string[])null), ("SPLIT_EFFECT_BLOCK", new string[0]),
            ("SPLIT_EFFECT_BLOCK", new[] { "a", "a" }), ("SPLIT_EFFECT_BLOCK", new[] { " " }), ("TARGET_META", new[] { "a" }) })
        {
            bool threw = false;
            try { HistoryShapeReport.Baseline(new[] { new HistoryShapeBaseline { Rule = rule, Card = "CS2_029", Reason = "r", Kind = "accepted", Shapes = shapes } }); }
            catch (InvalidOperationException) { threw = true; }
            Check(threw, $"baseline validation rejects {rule} shapes=[{(shapes == null ? "null" : string.Join(',', shapes))}]");
        }
        Console.WriteLine("PASS history-shape SplitBaselineShapes: entries accept only reviewed split shapes; new/stale classification");
    }
    private static void SplitDevouringPlagueRegression()
    {
        var shape = Seed();
        Feed(shape, Full(10, "CORE_BAR_311", CardType.SPELL, Zone.GRAVEYARD), Start(7, 10, 0), Start(3, 10, 0));
        for (int i = 0; i < 4; i++)
            Feed(shape, Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11),
                Start(5, 10, 0, 685), Meta(HistoryMeta.Type.HEALING, 1, 12), End(), End());
        Feed(shape, End(), End()); shape.Finish();
        var split = shape.Findings.Where(f => f.Rule == "SPLIT_EFFECT_BLOCK").ToArray();
        Check(split.Count(f => f.Card == "CORE_BAR_311" && Variant(f) == "nested-in-POWER" && f.Severity == "error") == 4
            && split.Count(f => Variant(f) == "sibling-POWER") == 3 && split.Length == 7,
            "Devouring Plague per-missile regression still fires despite nested Lifesteal");
        Console.WriteLine("PASS history-shape SplitDevouringPlagueRegression");
    }
    private static void SplitDistinctActivations()
    {
        foreach (string marker in new[] { "trigger-target", "keyword", "effect-index", "effect-card", "cast-zone" })
        {
            var shape = Seed();
            var first = Start(5, 10, 0, 217);
            var second = Start(5, 10, marker == "trigger-target" ? 15 : 0, marker == "keyword" ? 218 : 217);
            if (marker == "effect-index") second.PowerStart.EffectIndex = 1;
            if (marker == "effect-card") second.PowerStart.EffectCardId = "CS2_029";
            Feed(shape, Start(7, 10, 0), first, Meta(HistoryMeta.Type.DAMAGE, 1, 11), End());
            if (marker == "cast-zone") Feed(shape, TC(10, GameTag.ZONE, (int)Zone.HAND), TC(10, GameTag.ZONE, (int)Zone.SETASIDE));
            Feed(shape, second, Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End()); shape.Finish();
            Check(!shape.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK"), $"distinct activation wire marker {marker}");
        }
        foreach (bool hidden in new[] { false, true })
        {
            var drawn = Seed();
            Feed(drawn, Full(10, hidden ? "" : "TIME_025t", hidden ? CardType.INVALID : CardType.SPELL, Zone.HAND),
                Start(5, 10, 0), TC(10, GameTag.ZONE, (int)Zone.SETASIDE));
            Feed(drawn, Start(3, 10, 0), Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), End()); drawn.Finish();
            Check(!drawn.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK"), $"draw trigger casts spell in a new activation; hidden={hidden}");
        }

        var deathrattle = Seed();
        Feed(deathrattle, Full(10, "TLC_249", CardType.MINION, Zone.GRAVEYARD), Start(7, 10, 0), Start(5, 10, 0, 217), Start(3, 10, 11),
            Meta(HistoryMeta.Type.DAMAGE, 1, 11), End(), Start(3, 10, 13), Meta(HistoryMeta.Type.DAMAGE, 1, 13), End(), End(), End());
        deathrattle.Finish();
        Check(deathrattle.Findings.Count(f => f.Rule == "SPLIT_EFFECT_BLOCK" && Variant(f) == "nested-in-TRIGGER") == 2,
            "Deathrattle keyword does not exempt per-missile POWER continuations or different hit targets");
        Console.WriteLine("PASS history-shape SplitDistinctActivations");
    }
    private static void Synthetic()
    {
        var good = Seed();
        // Literal retail contracts: targeted spell, attack, actual heal, AoE, repeated hits, DECK SHOW before burn.
        Feed(good, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), End(),
            Start(1, 15, 11), TC(1, GameTag.PROPOSED_ATTACKER, 15), TC(1, GameTag.PROPOSED_DEFENDER, 11),
            TC(15, GameTag.ATTACKING, 1), TC(11, GameTag.DEFENDING, 1), TC(15, GameTag.NUM_ATTACKS_THIS_TURN, 1), TC(15, GameTag.EXHAUSTED, 1),
            Meta(HistoryMeta.Type.DAMAGE, 2, 11), TC(11, GameTag.DAMAGE, 10), Meta(HistoryMeta.Type.DAMAGE, 1, 15), TC(15, GameTag.DAMAGE, 1),
            TC(1, GameTag.PROPOSED_ATTACKER, 0), TC(1, GameTag.PROPOSED_DEFENDER, 0), TC(15, GameTag.ATTACKING, 0), TC(11, GameTag.DEFENDING, 0), End(),
            Start(3, 14, 11), Meta(HistoryMeta.Type.TARGET, 14, 11), Meta(HistoryMeta.Type.HEALING, 4, 11), TC(11, GameTag.DAMAGE, 6), End(),
            Start(target: 0), Meta(HistoryMeta.Type.TARGET, 0, 11, 13), Meta(HistoryMeta.Type.DAMAGE, 2, 11), TC(11, GameTag.DAMAGE, 8),
            Meta(HistoryMeta.Type.DAMAGE, 2, 13), TC(13, GameTag.DAMAGE, 2), End());
        good.Step++; // The repeated-hit spell is a separate player action.
        Feed(good, Start(target: 0), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 9),
            Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 10), End(),
            Show(16, "CS2_182", Zone.GRAVEYARD));
        good.Finish();
        Check(good.Findings.Count == 0, "retail targeted/attack/heal/AoE/repeated-hit/mill stream has zero findings");

        var hand = Seed();
        Feed(hand, Full(20, "CS2_182", zone: Zone.HAND), Start(target: 0),
            new() { SubSpellStart = new() { SourceEntityId = 12, TargetEntityIds = new() { 20 },
                SpellPrefabGuid = "ReuseFX_Generic_HandBuff_ScaleUp_OpponentAE_Gold_Super:2fb243a4d1ef3324fa09d018ce3f4418" } },
            Full(21, "", CardType.INVALID, Zone.SETASIDE));
        var handEnchant = Full(21, "RLK_712e", CardType.ENCHANTMENT, Zone.SETASIDE);
        handEnchant.FullEntity.Tags.Add(new Tag { Name = (int)GameTag.ATTACHED, Value = 20 });
        Feed(hand, new() { ShowEntity = handEnchant.FullEntity }, TC(21, GameTag.ZONE, (int)Zone.PLAY));
        Feed(hand, TC(20, GameTag.ATK, 5), TC(20, GameTag.HEALTH, 6), new() { SubSpellEnd = new() }, End());
        hand.Finish();
        Check(hand.Findings.Count == 0, "retail hand buff FULL/SHOW + targeted sub-spell survives packet splits");

        var trade = Seed();
        var reduction = Full(21, "RLK_712e", CardType.ENCHANTMENT, Zone.PLAY);
        reduction.FullEntity.Tags.Add(new Tag { Name = (int)GameTag.ATTACHED, Value = 20 });
        Feed(trade, Full(20, "CORE_SW_072", zone: Zone.HAND), TC(20, GameTag.ATK, 1), reduction,
            Start(type: 13, source: 20, target: 0), TC(20, GameTag.IS_USING_TRADE_OPTION, 1),
            TC(20, GameTag.ATK, 3), TC(21, GameTag.ATTACHED, 0), TC(21, GameTag.ZONE, (int)Zone.GRAVEYARD),
            TC(20, GameTag.ZONE, (int)Zone.DECK), End());
        trade.Finish();
        Check(trade.Findings.Count == 0, "Trade restores base stats while removing hand-only enchantments; no buff FX");

        var summonScale = Seed();
        var heldEnchant = Full(21, "RLK_712e", CardType.ENCHANTMENT, Zone.PLAY);
        heldEnchant.FullEntity.Tags.Add(new Tag { Name = (int)GameTag.ATTACHED, Value = 20 });
        Feed(summonScale, Full(20, "CATA_725t", zone: Zone.HAND), heldEnchant, Start(type: 7, source: 20, target: 0),
            TC(20, GameTag.ATK, 8), TC(20, GameTag.HEALTH, 8), TC(20, GameTag.ZONE, (int)Zone.PLAY), End());
        summonScale.Finish();
        Check(!summonScale.Findings.Any(f => f.Rule == "HAND_BUFF_FX"), "summon-time source scaling uses summon presentation");

        var covered = new HashSet<string>();
        void Broken(string rule, Action<HistoryShape> action)
        {
            var shape = Seed(); action(shape); shape.Finish();
            Check(shape.Findings.Any(v => v.Rule == rule), "sensitivity " + rule + "; found=" + string.Join(',', shape.Findings.Select(v => v.Rule)));
            covered.Add(rule);
        }
        // Retail O:3282-3307: explicit hand sub-spell encloses SHOW/attachment and both stat changes.
        foreach (string variant in new[] { "absent", "history-only", "wrong-target", "empty-prefab", "late", "expired" })
            Broken("HAND_BUFF_FX", s => {
                Feed(s, Full(20, "CS2_182", zone: Zone.HAND), Start(target: 0));
                var enchant = Full(21, "RLK_712e", CardType.ENCHANTMENT, Zone.SETASIDE);
                enchant.FullEntity.Tags.Add(new Tag { Name = (int)GameTag.ATTACHED, Value = 20 });
                var sub = new PowerHistoryData { SubSpellStart = new() {
                    SpellPrefabGuid = variant == "empty-prefab" ? "" : "ReuseFX_Generic_HandBuff_ScaleUp_OpponentAE_Gold_Super:2fb243a4d1ef3324fa09d018ce3f4418",
                    SourceEntityId = 12, TargetEntityIds = new() { variant == "wrong-target" ? 13 : 20 } } };
                if (variant == "history-only") Feed(s, Meta(HistoryMeta.Type.HISTORY_TARGET, 0, 20));
                if (variant is not ("absent" or "history-only" or "late")) Feed(s, sub);
                Feed(s, new PowerHistoryData { ShowEntity = enchant.FullEntity }, TC(21, GameTag.ZONE, (int)Zone.PLAY));
                if (variant == "expired") Feed(s, new PowerHistoryData { SubSpellEnd = new() });
                Feed(s, TC(20, GameTag.ATK, 5), TC(20, GameTag.HEALTH, 6));
                if (variant == "late") Feed(s, sub);
                if (variant is not ("absent" or "history-only" or "expired")) Feed(s, new PowerHistoryData { SubSpellEnd = new() });
                Feed(s, End());
            });
        Broken("BALANCE", s => Feed(s, End()));
        Broken("BALANCE", s => Feed(s, Start()));
        Broken("BALANCE", s => Feed(s, Start(10), End()));
        Broken("DAMAGE_META", s => Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), TC(11, GameTag.DAMAGE, 8), End()));
        Broken("DAMAGE_META", s => Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11, 13),
            Meta(HistoryMeta.Type.DAMAGE, 3, 11, 13), TC(11, GameTag.DAMAGE, 8), End()));
        Broken("DAMAGE_META_AMOUNT", s => Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 9, 11), TC(11, GameTag.DAMAGE, 8), End()));
        Broken("SHIELD_META", s => Feed(s, TC(11, GameTag.DIVINE_SHIELD, 1), Start(), TC(11, GameTag.DIVINE_SHIELD, 0), End()));
        Broken("HEALING_META", s => Feed(s, Start(3, 14), Meta(HistoryMeta.Type.TARGET, 0, 11), TC(11, GameTag.DAMAGE, 3), End()));
        Broken("HEALING_META", s => Feed(s, Start(3, 14), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.HEALING, 5, 11), TC(11, GameTag.DAMAGE, 3), End()));
        Broken("TARGET_META", s => Feed(s, Start(), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), Meta(HistoryMeta.Type.TARGET, 0, 11), End()));
        Broken("TARGET_DATA", s => Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 3, 11), End()));
        Broken("POWER_HIT_TARGET", s => {
            s.RecordMutation(11, GameTag.DAMAGE, 8, Hit(selected: true));
            Feed(s, Start(target: 0), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), End());
        });
        Broken("HIT_TARGET", s => Feed(s, Start(target: 0), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), End()));
        Broken("FOREIGN_SOURCE", s => {
            s.RecordMutation(11, GameTag.DAMAGE, 8, Hit(source: 15));
            Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), End());
        });
        Broken("ATTACK_SHAPE", s => Feed(s, Start(1, 15), End()));
        Broken("ATTACK_PROPOSED", s => Feed(s, Start(1, 15), End()));
        Broken("ATTACK_RESET", s => Feed(s, Start(1, 15), TC(1, GameTag.PROPOSED_ATTACKER, 15), TC(1, GameTag.PROPOSED_DEFENDER, 11),
            TC(15, GameTag.ATTACKING, 1), TC(11, GameTag.DEFENDING, 1), End()));
        Broken("TRIGGER_KEYWORD", s => {
            s.RecordBlock(new() { Type = 3, Source = 15, Target = 0, Activation = PowerActivation.DEATHRATTLE }); Feed(s, Start(3, 15, 0), End());
        });
        Broken("TRIGGER_KEYWORD", s => {
            s.RecordBlock(new() { Type = 5, Source = 15, Target = 0, Activation = PowerActivation.DEATHRATTLE }); Feed(s, Start(3, 15, 0), End());
        });
        Broken("TRIGGER_KEYWORD", s => {
            s.RecordMutation(11, GameTag.DAMAGE, 3, Hit(amount: 2, heal: true, lifesteal: true));
            Feed(s, Start(5, target: 0), Meta(HistoryMeta.Type.HEALING, 2, 11), TC(11, GameTag.DAMAGE, 3), End());
        });
        Broken("EFFECT_INDEX", s => { var entry = new PowerHistoryData { PowerStart = new() { Type = (HistoryBlock.Type)3, Source = 10, Target = 0 } }; Feed(s, entry, End()); });
        Broken("FATIGUE_BLOCK", s => {
            s.RecordMutation(11, GameTag.DAMAGE, 8, Hit(source: 11, fatigue: true));
            Feed(s, Start(target: 0), TC(11, GameTag.DAMAGE, 8), End());
        });
        Broken("FATIGUE_BLOCK", s => Feed(s, TC(12, GameTag.FATIGUE, 2)));
        Broken("FATIGUE_BLOCK", s => Feed(s, Start(3, 12, 0), TC(12, GameTag.FATIGUE, 2), End()));
        Broken("FATIGUE_BLOCK", s => Feed(s, Start(5, 12, 0, (int)GameTag.LIFESTEAL), TC(12, GameTag.FATIGUE, 2), End()));
        Broken("ANON_PUBLIC", s => Feed(s, TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD)));
        Broken("ANON_PUBLIC", s => Feed(s, TC(16, GameTag.ZONE, (int)Zone.REMOVEDFROMGAME)));
        {
            // A Rewind reset retires absent hidden actors before RESET_GAME; that removal needs no identity.
            var retired = Seed();
            Feed(retired, TC(16, GameTag.ZONE, (int)Zone.REMOVEDFROMGAME),
                new() { PowerStart = new() { Type = (HistoryBlock.Type)11, Source = 1 } },
                new() { ResetGame = new() { SourceIsRewind = true, CreateGame = new() { GameEntity = new() { Id = 1, Tags = new() }, Players = new() } } },
                End());
            retired.Finish();
            Check(!retired.Findings.Any(v => v.Rule == "ANON_PUBLIC"), "Rewind retirement of a hidden actor is not ANON_PUBLIC");
        }
        Broken("MILL_ORDER", s => Feed(s, Show(16, "CS2_182", Zone.DECK), TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD)));
        Broken("MILL_ORDER", s => Feed(s, Show(16, "", Zone.GRAVEYARD)));
        Broken("MILL_ORDER", s => Feed(s, Full(16, "CS2_182", zone: Zone.GRAVEYARD)));
        Broken("MILL_ORDER", s => Feed(s, Show(16, "CS2_182", Zone.GRAVEYARD), TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD)));
        Broken("MILL_ORDER", s => {
            Feed(s, Show(16, "CS2_182", Zone.GRAVEYARD));
            Feed(s, new PowerHistoryData { HideEntity = new() { Entity = 16, Zone = (int)Zone.SETASIDE } });
        });
        var forwardFull = Seed();
        Feed(forwardFull, Meta(HistoryMeta.Type.TARGET, 0, 999), Full(999, "CS2_182"));
        Check(!forwardFull.Findings.Any(f => f.Rule == "META_REF"), "metadata can reference a later FULL in its received packet");
        Broken("META_REF", s => Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 999), Show(999, "CS2_182", Zone.PLAY)));
        Broken("META_REF", s => {
            Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 999));
            Feed(s, Full(999, "CS2_182"));
        });
        Broken("META_REF", s => Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 999)));
        Broken("PRIVACY", s => Feed(s, Show(16, "CS2_182", Zone.DECK)));
        Broken("META_REF", s => Feed(s, TC(999, GameTag.ZONE, (int)Zone.PLAY), Meta(HistoryMeta.Type.TARGET, 0, 999)));
        Broken("TARGET_META", s => Feed(s, Start(), TC(11, GameTag.FROZEN, 1), Meta(HistoryMeta.Type.TARGET, 0, 11), End()));
        foreach (string variant in new[] { "absent", "late", "parent", "consumed", "wrong-victim" })
            Broken("TRIGGER_HIT_TARGET", s => {
                if (variant == "parent") Feed(s, Start(target: 0), Meta(HistoryMeta.Type.TARGET, 0, 11));
                Feed(s, Start(5, target: 0));
                if (variant == "wrong-victim") Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 13));
                if (variant == "consumed") Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 6));
                Feed(s, Meta(HistoryMeta.Type.DAMAGE, 3, 11));
                if (variant == "late") Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 11));
                s.RecordMutation(11, GameTag.DAMAGE, 8, Hit(selected: true));
                Feed(s, TC(11, GameTag.DAMAGE, 8), End());
                if (variant == "parent") Feed(s, End());
            });
        foreach (string variant in new[] { "absent-keyword", "wrong-keyword", "missing-target", "late-target", "wrong-source", "card-effect", "absent-index", "explicit-effect", "wrong-block", "parent-target", "missing-poison-meta", "wrong-poison-victim", "wrong-poison-data", "late-poison-meta", "subspell-target" })
            Broken("POISONOUS_KEYWORD", s => {
                s.RecordPoison(15, 11);
                if (variant == "parent-target") Feed(s, Start(target: 0), Meta(HistoryMeta.Type.TARGET, 0, 11));
                var start = Start(variant == "wrong-block" ? 3 : 5, variant == "wrong-source" ? 13 : 15, 0, variant == "wrong-keyword" ? 217 : 363);
                start.PowerStart.EffectIndex = variant == "card-effect" ? 0 : -1;
                if (variant == "absent-keyword") start.PowerStart = new() { Type = (HistoryBlock.Type)5, Source = 15, Target = 0, EffectIndex = -1 };
                if (variant == "absent-index") start.PowerStart = new() { Type = (HistoryBlock.Type)5, Source = 15, Target = 0, TriggerKeyword = 363 };
                if (variant == "explicit-effect") start.PowerStart.EffectCardId = "CS2_120";
                Feed(s, start);
                if (variant is not ("missing-target" or "late-target" or "parent-target" or "subspell-target")) Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 11));
                if (variant == "subspell-target") Feed(s, new PowerHistoryData { SubSpellStart = new() {
                    SpellPrefabGuid = "ReuseFX_Arcane_Impact_Shimmer_Super:4aef9b1bcd17c4d4cb3de76f139730e5", TargetEntityIds = new() { 11 } } });
                if (variant is not ("missing-poison-meta" or "late-poison-meta"))
                    Feed(s, Meta((HistoryMeta.Type)22, variant == "wrong-poison-data" ? 1 : 0, variant == "wrong-poison-victim" ? 13 : 11));
                Feed(s, TC(11, GameTag.TO_BE_DESTROYED, 1));
                if (variant == "late-poison-meta") Feed(s, Meta((HistoryMeta.Type)22, 0, 11));
                if (variant == "late-target") Feed(s, Meta(HistoryMeta.Type.TARGET, 0, 11));
                Feed(s, End());
                if (variant == "parent-target") Feed(s, End());
            });
        foreach (string variant in new[] { "duplicate-block", "unexpected-block", "missing-destroy", "missing-poison-meta" })
            Broken("POISONOUS_KEYWORD", s => {
                if (variant != "unexpected-block") s.RecordPoison(15, 11);
                for (int i = 0; i < (variant == "duplicate-block" ? 2 : 1); i++)
                {
                    var start = Start(5, 15, 0, 363); start.PowerStart.EffectIndex = -1;
                    Feed(s, start, Meta(HistoryMeta.Type.TARGET, 0, 11));
                    if (variant != "missing-poison-meta") Feed(s, Meta((HistoryMeta.Type)22, 0, 11));
                    if (variant != "missing-destroy") Feed(s, TC(11, GameTag.TO_BE_DESTROYED, 1));
                    Feed(s, End());
                }
            });
        Broken("SPLIT_EFFECT_BLOCK", s => Feed(s, Start(target: 0), Start(target: 0),
            Meta(HistoryMeta.Type.TARGET, 10, 11), End(), End()));
        Broken("FULL_ENTITY_ID", s => {
            var malformed = Full(80, "", zone: Zone.DECK);
            malformed.FullEntity.Tags.RemoveAll(t => t.Name == (int)GameTag.ENTITY_ID);
            Feed(s, malformed);
        });
        Check(covered.SetEquals(HistoryShape.Rules), "sensitivity covers every rule");

        var trigger = Seed();
        // Retail Overheal O:2445 has TARGET within TRIGGER; repeated-hit ranges follow native 4.5 / Astalor F:6416.
        Feed(trigger, Start(5, 15, 0), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 6));
        Feed(trigger, Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 7), End());
        // Undying Disciple F:7704: untargeted multi-victim deathrattle has DAMAGE but no TARGET.
        Feed(trigger, Start(5, 15, 0, 217), Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 8),
            Meta(HistoryMeta.Type.DAMAGE, 1, 13), TC(13, GameTag.DAMAGE, 1), End());
        trigger.RecordPoison(15, 11); var poisonStart = Start(5, 15, 0, 363); poisonStart.PowerStart.EffectIndex = -1;
        Feed(trigger, poisonStart, Meta(HistoryMeta.Type.TARGET, 0, 11));
        Feed(trigger, Meta((HistoryMeta.Type)22, 0, 11), TC(11, GameTag.TO_BE_DESTROYED, 1), End()); trigger.Finish();
        Check(trigger.Findings.Count == 0, "TRIGGER repeated-hit ranges, untargeted AoE and keyworded Poisonous pass across packets");

        // Armor+health are ONE resolved hit; metadata cannot be reused or borrowed from parent/sibling blocks.
        var shield = Seed(); Feed(shield, TC(11, GameTag.DIVINE_SHIELD, 1), Start(5, 15, 0),
            Meta(HistoryMeta.Type.DAMAGE, 0, 11), TC(11, GameTag.DIVINE_SHIELD, 0), End());
        Check(shield.Findings.Count == 0, "shield pop accepts zero DAMAGE metadata");
        var armor = Seed(); Feed(armor, TC(11, GameTag.ARMOR, 2));
        var shared = Hit(amount: 5); armor.RecordMutation(11, GameTag.ARMOR, 0, shared); armor.RecordMutation(11, GameTag.DAMAGE, 8, shared);
        Feed(armor, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 5, 11), TC(11, GameTag.ARMOR, 0), TC(11, GameTag.DAMAGE, 8), End());
        Check(armor.Findings.Count == 0, "armor-inclusive amount consumed once");
        var host = Seed(); Feed(host, Full(17, "EDR_482e", CardType.ENCHANTMENT), TC(17, GameTag.ATTACHED, 15));
        host.RecordMutation(11, GameTag.DAMAGE, 8, Hit(source: 15));
        Feed(host, Start(5, 17, 0), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), End());
        Check(host.Findings.Count == 0, "enchantment block may resolve damage from its attached host");
        var creator = Seed(); Feed(creator, Full(17, "EDR_482e", CardType.ENCHANTMENT), TC(17, GameTag.ATTACHED, 1), TC(17, GameTag.CREATOR, 15));
        creator.RecordMutation(11, GameTag.DAMAGE, 8, Hit(source: 15));
        Feed(creator, Start(5, 17, 0), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8), End());
        Check(creator.Findings.Count == 0, "delayed enchantment damage may retain its creator as actual source");
        Broken("DAMAGE_META", s => Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 3, 11),
            TC(11, GameTag.DAMAGE, 8), TC(11, GameTag.DAMAGE, 11), End()));
        Broken("DAMAGE_META", s => Feed(s, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 3, 11),
            Start(5, 10, 0), TC(11, GameTag.DAMAGE, 8), End(), End()));
        var split = Seed(); Feed(split, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11), Meta(HistoryMeta.Type.DAMAGE, 3, 11));
        Feed(split, TC(11, GameTag.DAMAGE, 8), End()); split.Finish(); Check(split.Findings.Count == 0, "block and metadata survive packet boundary");
        var keywords = Seed();
        keywords.RecordBlock(new() { Type = 5, Source = 15, Target = 0, Activation = PowerActivation.DEATHRATTLE });
        Feed(keywords, Start(5, 15, 0, (int)GameTag.DEATHRATTLE), End());
        keywords.RecordMutation(11, GameTag.DAMAGE, 3, Hit(amount: 2, heal: true, lifesteal: true));
        Feed(keywords, Start(5, 10, 0, (int)GameTag.LIFESTEAL), Meta(HistoryMeta.Type.HEALING, 2, 11), TC(11, GameTag.DAMAGE, 3), End());
        var fatigueHit = new HistoryShape.Hit { Source = 12, Victim = 12, Amount = 1, Fatigue = true };
        keywords.RecordMutation(12, GameTag.DAMAGE, 1, fatigueHit);
        Feed(keywords, Start(8, 12, 0), TC(12, GameTag.FATIGUE, 1), Meta(HistoryMeta.Type.DAMAGE, 1, 12), TC(12, GameTag.DAMAGE, 1), End());
        var controllerFatigue = new HistoryShape.Hit { Source = 12, Victim = 12, Amount = 2, Fatigue = true };
        keywords.RecordMutation(12, GameTag.DAMAGE, 3, controllerFatigue);
        Feed(keywords, Start(8, 12, 0), TC(1, GameTag.FATIGUE, 2), Meta(HistoryMeta.Type.DAMAGE, 2, 12), TC(12, GameTag.DAMAGE, 3), End());
        Feed(keywords, Meta(HistoryMeta.Type.SHOW_BIG_CARD, 0, 11, 1000));
        Check(keywords.Findings.Count == 0, "keyword routing, valid fatigue, and non-entity metadata parameters pass");
        var hidden = Seed(); Feed(hidden, Show(16, "CS2_182", Zone.DECK), new() { HideEntity = new() { Entity = 16, Zone = (int)Zone.DECK } });
        Check(hidden.Findings.Count == 0, "temporary reveal then HIDE is private-compatible");
        var reset = Seed(); reset.RecordMutation(11, GameTag.DAMAGE, 0); Feed(reset, Start(6), TC(11, GameTag.DAMAGE, 0), End());
        Check(reset.Findings.Count == 0, "death resets are not healing");
        var transform = Seed(); transform.RequireCorrelation = true;
        var afterTransform = Hit(amount: 3); transform.RegisterHit(afterTransform);
        transform.RecordMutation(11, GameTag.DAMAGE, 0); transform.RecordMutation(11, GameTag.DAMAGE, 3, afterTransform);
        var changed = Full(11, "CS2_120");
        Feed(transform, new() { ChangeEntity = changed.FullEntity }, Start(), Meta(HistoryMeta.Type.TARGET, 0, 11),
            Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 3), End());
        Check(transform.Findings.Count == 0 && afterTransform.Observed, "snapshot reset cannot consume or block next real hit");
        var redirected = Seed(); redirected.RecordMutation(11, GameTag.DAMAGE, 8, Hit(source: 13));
        Feed(redirected, Start(1, 15, 11), TC(1, GameTag.PROPOSED_ATTACKER, 13), TC(1, GameTag.PROPOSED_DEFENDER, 11),
            TC(13, GameTag.ATTACKING, 1), TC(11, GameTag.DEFENDING, 1), Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8),
            TC(1, GameTag.PROPOSED_ATTACKER, 0), TC(1, GameTag.PROPOSED_DEFENDER, 0), TC(13, GameTag.ATTACKING, 0), TC(11, GameTag.DEFENDING, 0), End());
        Check(redirected.Findings.Count == 0, "attack accepts both actual proposed combatants after redirection");
        var cancelled = Seed(); cancelled.RecordBlock(new() { Type = 1, Source = 15, Target = 11, Cancelled = true });
        Feed(cancelled, Start(1, 15), End()); Check(cancelled.Findings.Count == 0, "engine-cancelled attack exemption");
        Console.WriteLine($"PASS history-shape synthetic: retail-shaped stream zero errors; all {HistoryShape.Rules.Length} rules sensitive; TRIGGER hit ranges, Poisonous, armor, consumption, immediate blocks, split packets, privacy, resets, cancelled attack");
    }

    // Phase 2 changes only this table as the native stream is repaired; counts pin today's complete result.
    private static readonly Dictionary<string, Dictionary<string, int>> Expected = new() {
        ["bot-rock"] = new(),
        ["watcher-burn"] = new(),
        ["kronn"] = new(),
        ["lifesteal-attack"] = new(),
        ["lifesteal-spell"] = new(),
        ["heal-power"] = new(),
        ["minion-attack"] = new(),
        ["fatigue"] = new(),
        ["overdraw"] = new()
    };
    private sealed class Fixture
    {
        internal readonly Game Game;
        internal readonly NativeSession Session;
        internal Controller Player => Game.Player1;
        internal Fixture(CardClass hero = CardClass.PRIEST, bool history = true, Game restored = null)
        {
            Game = restored ?? new(new GameConfig { StartPlayer = 1, SkipMulligan = true, Shuffle = false, RandomSeed = 37,
                History = history, Logging = false, FormatType = FormatType.FT_WILD, Player1HeroClass = hero, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            if (restored == null) { Game.StartGame(); Player.BaseMana = 10; }
            else { Game.PowerHistory.Last.Clear(); Game.PowerHistory.Last.AddRange(Game.PowerHistory.Full); }
            if (!history) return;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, Game);
            Session = session = new(match); Session.History.RequireCorrelation = true; Flush();
        }
        internal void Flush() { Session.Flush(); Game.PowerHistory.Last.Clear(); }
        internal Minion Summon(Controller owner, string card)
        {
            var entity = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(card));
            Generic.SummonBlock(Game, entity, -1, null); entity.IsExhausted = false; return entity;
        }
        internal void Act(PlayerTask task)
        {
            Flush(); // Publish fixture setup before NativeMatch.Process clears the pending engine history.
            Session.PublishDecision(task.Controller); Session.Process(task); Game.PowerHistory.Last.Clear();
        }
        internal void Ready(string name)
        {
            Flush(); Session.History.Findings.Clear(); Session.History.Replay = "history-shape-" + name;
        }
        internal void Result(string name)
        {
            Session.History.Finish();
            var actual = Session.History.Findings.Where(v => v.Rule != "SPLIT_EFFECT_BLOCK").GroupBy(v => v.Rule).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine("history-shape scenario " + name + ": " + string.Join(",", actual.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)));
            Check(actual.Count == Expected[name].Count && Expected[name].All(p => actual.GetValueOrDefault(p.Key) == p.Value),
                "focused " + name + "; findings=" + string.Join(";", Session.History.Findings.Select(f => f.Detail)) + "; expected=" + string.Join(',', Expected[name].Select(p => p.Key + "=" + p.Value)));
            HistoryShapeReport.Add(Session.History);
        }
    }
    private static void Focused()
    {
        var rock = new Fixture();
        var target = rock.Summon(rock.Player, "CS2_200");
        rock.Act(EndTurnTask.Any(rock.Player)); rock.Player.Opponent.BaseMana = 10;
        var skipper = Generic.DrawCard(rock.Player.Opponent, Cards.FromId("TLC_427")); rock.Flush();
        rock.Act(PlayCardTask.Minion(rock.Player.Opponent, skipper));
        var stone = rock.Player.Opponent.HandZone.Single(c => c.Card.Id == "WW_001t");
        rock.Ready("bot-rock"); rock.Act(PlayCardTask.SpellTarget(rock.Player.Opponent, stone, target)); rock.Result("bot-rock");

        var watcher = new Fixture(); var watcherMinion = watcher.Summon(watcher.Player, "TLC_621");
        watcher.Ready("watcher-burn"); watcherMinion.Destroy(); watcher.Game.DeathProcessingAndAuraUpdate(); watcher.Flush(); watcher.Result("watcher-burn");

        var kronn = new Fixture();
        for (int i = 0; i < 3; i++) kronn.Summon(kronn.Player, "CS2_120");
        kronn.Player.Opponent.Hero.Health = 100;
        var impaler = Generic.DrawCard(kronn.Player, Cards.FromId("BE_732")); kronn.Ready("kronn");
        kronn.Act(PlayCardTask.Minion(kronn.Player, impaler)); kronn.Result("kronn");
        var contributors = kronn.Game.PowerHistory.Full.OfType<EB>().Where(b => b.BlockType == BlockType.TRIGGER && b.Target != 0
            && kronn.Game.IdEntityDic.TryGetValue(b.Source, out var entity) && entity.Card.Id == "CS2_120").ToArray();
        Check(contributors.Length == 3 && contributors.All(b => b.EffectIndex == -1),
            "Kronn attributes each contributor without playing its card trigger effect");

        var life = new Fixture(); var whelp = life.Summon(life.Player, "TIME_056"); var enemy = life.Summon(life.Player.Opponent, "CS2_200");
        Check(whelp.HasLifeSteal, "registered Whelp of the Bronze has Lifesteal"); life.Player.Hero.Damage = 10;
        life.Ready("lifesteal-attack"); life.Act(MinionAttackTask.Any(life.Player, whelp, enemy)); life.Result("lifesteal-attack");

        var drain = new Fixture(); var drainTarget = drain.Summon(drain.Player.Opponent, "CS2_200");
        drain.Player.Hero.Damage = 10; var drainSpell = Generic.DrawCard(drain.Player, Cards.FromId("CORE_ICC_055"));
        drain.Ready("lifesteal-spell"); drain.Act(PlayCardTask.SpellTarget(drain.Player, drainSpell, drainTarget)); drain.Result("lifesteal-spell");

        var heal = new Fixture(); heal.Player.Hero.Damage = 5; heal.Ready("heal-power");
        heal.Act(HeroPowerTask.Any(heal.Player, heal.Player.Hero)); heal.Result("heal-power");

        var attack = new Fixture(); var attacker = attack.Summon(attack.Player, "CS2_120"); var defender = attack.Summon(attack.Player.Opponent, "CS2_200");
        attack.Ready("minion-attack"); attack.Act(MinionAttackTask.Any(attack.Player, attacker, defender)); attack.Result("minion-attack");

        var fatigue = new Fixture(); foreach (var c in fatigue.Player.DeckZone.ToArray()) fatigue.Player.SetasideZone.Add(fatigue.Player.DeckZone.Remove(c));
        fatigue.Ready("fatigue"); Generic.Draw(fatigue.Player); fatigue.Flush(); fatigue.Result("fatigue");

        var overdraw = new Fixture(); while (overdraw.Player.HandZone.Count < 10) Generic.DrawCard(overdraw.Player, Cards.FromId("CS2_182"));
        overdraw.Ready("overdraw"); Generic.Draw(overdraw.Player); overdraw.Flush(); overdraw.Result("overdraw");
        Console.WriteLine("PASS history-shape focused: bot Rock, Watcher mill, Kronn sources, Lifesteal attack/spell, healing power, minion attack, fatigue, overdraw");
    }
    private static void HookBoundaries()
    {
        var fixture = new Fixture(); var source = fixture.Summon(fixture.Player, "CS2_120");
        var victim = fixture.Summon(fixture.Player.Opponent, "CS2_200"); var hero = fixture.Player.Opponent.Hero;
        hero.Armor = 5; victim.Damage = 2; fixture.Ready("hook-boundaries");
        fixture.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
        hero.Armor -= 2; victim.Damage = 3; victim.Health = 4; // Payments/direct health sets are not combat or healing.
        hero.TakeDamage(source, 5); // 3 armor + 2 health = one hit, not two.
        hero.TakeHeal(source, 10); hero.TakeHeal(source, 10); // Actual capped heal=2; second attempt heals zero.
        hero.Armor = 5; hero.TakeDamage(source, 2); // Fully absorbed: a hit without any DAMAGE tag change.
        fixture.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd()); fixture.Flush();
        var effects = fixture.Game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryMetaData>()
            .Where(m => m.Type is MetaDataType.DAMAGE or MetaDataType.HEALING).ToArray();
        Check(!fixture.Session.History.Findings.Any(v => v.Severity == "error")
            && effects.Where(m => m.Type == MetaDataType.DAMAGE).Select(m => m.Data).SequenceEqual(new[] { 5, 2 })
            && effects.Where(m => m.Type == MetaDataType.HEALING).Select(m => m.Data).SequenceEqual(new[] { 2 })
            && hero.Damage == 0 && hero.Armor == 3, "hook boundaries: armor/payment/direct sets/capped and zero heal");
        Console.WriteLine("PASS history-shape hooks: armor-inclusive and armor-only hits; payments/direct Health sets ignored; capped/zero healing; no unmatched hook hits");
    }
    private static void EmissionRanges()
    {
        var f = new Fixture(); var a = f.Summon(f.Player.Opponent, "CS2_200"); var b = f.Summon(f.Player.Opponent, "CS2_200");
        var spell = Generic.DrawCard(f.Player, Cards.FromId("CS2_029"));
        f.Ready("emission-ranges");
        var history = f.Game.PowerHistory;
        foreach (string task in new[] { "damage", "damage-number", "heal", "heal-number", "heal-full", "context-batch" })
        foreach (var kind in new[] { BlockType.POWER, BlockType.TRIGGER })
        {
            a.Damage = b.Damage = task.StartsWith("heal") ? 4 : 0;
            int cursor = history.Full.Count;
            history.Add(EH.BlockStart(kind, spell.Id, "", -1, 0));
            var stack = new TaskStack { Playables = new List<IPlayable> { a, b }, Number = 1 };
            if (kind == BlockType.TRIGGER) new RandomTask(2, EntityType.STACK).Process(f.Game, f.Player, spell, null, stack);
            for (int i = 0; i < 2; i++)
                switch (task)
                {
                    case "damage": new DamageTask(1, EntityType.STACK).Process(f.Game, f.Player, spell, null, stack); break;
                    case "damage-number": new DamageNumberTask(EntityType.STACK).Process(f.Game, f.Player, spell, null, stack); break;
                    case "heal": new HealTask(1, EntityType.STACK).Process(f.Game, f.Player, spell, null, stack); break;
                    case "heal-number": new HealNumberTask(EntityType.STACK).Process(f.Game, f.Player, spell, null, stack); break;
                    case "heal-full": new HealFullTask(EntityType.STACK).Process(f.Game, f.Player, spell, null, stack); break;
                    case "context-batch": new SabberStoneCore.EffectSystem.EffectContext(f.Game, f.Player.Id, spell.Id)
                        .DamageBatch(new[] { a.Id, b.Id }, 1); break;
                }
            history.Add(EH.BlockEnd());
            var metadata = history.Full.Skip(cursor).OfType<EM>().ToArray();
            var ranges = metadata.Where(m => m.Type == MetaDataType.TARGET).ToArray();
            Check(ranges.Length == 2 && ranges.All(m => m.Data == spell.Id && m.Info.SequenceEqual(new[] { a.Id, b.Id })),
                kind + " " + task + " has one complete target list per invocation, no extra missiles per victim");
            Check(metadata.Count(m => m.Type != MetaDataType.TARGET) == (task == "heal-full" ? 2 : 4),
                task + " emits one metadata per real effect; full healing omits zero heal");
        }
        a.Damage = 0;
        int start = history.Full.Count;
        history.Add(EH.BlockStart(BlockType.POWER, spell.Id, "", -1, a.Id));
        history.Add(new EM { Type = MetaDataType.TARGET, Data = spell.Id, Info = new() { a.Id } });
        new DamageTask(1, EntityType.TARGET).Process(f.Game, f.Player, spell, a);
        history.Last.Clear(); // Packet drain must retain the open block's consumed target range.
        new DamageTask(1, EntityType.TARGET).Process(f.Game, f.Player, spell, a);
        history.Add(EH.BlockStart(BlockType.TRIGGER, spell.Id, "", -1, 0));
        new DamageTask(1, EntityType.TARGET).Process(f.Game, f.Player, spell, b);
        history.Add(EH.BlockEnd());
        new DamageTask(1, EntityType.TARGET).Process(f.Game, f.Player, spell, a);
        history.Add(EH.BlockEnd());
        var targets = history.Full.Skip(start).OfType<EM>().Where(m => m.Type == MetaDataType.TARGET).ToArray();
        Check(targets.Length == 4 && targets.SelectMany(m => m.Info).SequenceEqual(new[] { a.Id, a.Id, b.Id, a.Id }),
            "header deduplicated; resolved nested TRIGGER target and repeated POWER hits survive packet drain");

        int keyword = history.Full.Count;
        history.Add(EH.BlockStart(BlockType.POWER, spell.Id, "", -1, a.Id));
        history.Add(new EM { Type = MetaDataType.TARGET, Data = spell.Id, Info = new() { a.Id } });
        a.HasTaunt = true;
        new DamageTask(1, EntityType.TARGET).Process(f.Game, f.Player, spell, a);
        history.Add(EH.BlockEnd());
        Check(history.Full.Skip(keyword).OfType<EM>().Count(m => m.Type == MetaDataType.TARGET) == 2,
            "keyword effect consumes the header range before the damage invocation");

        int latest = history.Full.Count;
        history.Add(EH.BlockStart(BlockType.POWER, spell.Id, "", -1, 0));
        history.Add(new EM { Type = MetaDataType.TARGET, Data = spell.Id, Info = new() { a.Id } });
        history.Add(new EM { Type = MetaDataType.TARGET, Data = spell.Id, Info = new() { b.Id } });
        new DamageTask(1, EntityType.TARGET).Process(f.Game, f.Player, spell, a);
        history.Add(EH.BlockEnd());
        Check(history.Full.Skip(latest).OfType<EM>().Count(m => m.Type == MetaDataType.TARGET) == 3,
            "only the immediately preceding TARGET can deduplicate an effect invocation");

        var outside = new Fixture(); var healer = outside.Summon(outside.Player, "CS2_120");
        outside.Player.Hero.Damage = 5; outside.Ready("inline-continuation");
        int inlineStart = outside.Game.PowerHistory.Full.Count;
        outside.Player.Hero.TakeHeal(healer, 2);
        outside.Player.Hero.TakeDamage(healer, 1); outside.Flush();
        var inlineBlocks = outside.Game.PowerHistory.Full.Skip(inlineStart).OfType<EB>().ToArray();
        Check(inlineBlocks.Length == 2 && inlineBlocks.All(b => b.BlockType == BlockType.TRIGGER && b.EffectIndex == -1),
            "inline source attribution suppresses the contributor card trigger effect");
        Check(!outside.Session.History.Findings.Any(v => v.Rule is "HEALING_META" or "DAMAGE_META" or "BALANCE"),
            "inline Discover continuations retain effect context for damage and healing metadata");

        int immediate = history.Full.Count;
        f.Game.TaskQueue.Execute(new CustomTask((g, c, s, t, stack) => { }) { IsTrigger = true }, f.Player, spell, a);
        var immediateEntries = history.Full.Skip(immediate).ToArray();
        Check(immediateEntries[0] is EB { BlockType: BlockType.TRIGGER, EffectIndex: 0 }
            && !immediateEntries.OfType<EM>().Any(m => m.Type == MetaDataType.TARGET),
            "fast trigger uses TRIGGER without header TARGET even when source is a targeted spell");

        foreach (bool queued in new[] { false, true })
        {
            int cursor = history.Full.Count;
            var body = new DamageTask(1, EntityType.TARGET);
            if (queued)
            {
                f.Game.TaskQueue.Enqueue(body, f.Player, spell, a, triggerKeyword: (int)GameTag.DEATHRATTLE);
                f.Game.ProcessTasks();
            }
            else f.Game.TaskQueue.Execute(body, f.Player, spell, a, historyTrigger: true);
            var entries = history.Full.Skip(cursor).ToArray();
            Check(entries[0] is EB { BlockType: BlockType.TRIGGER, EffectIndex: 0 } block
                && block.TriggerKeyword == (queued ? (int)GameTag.DEATHRATTLE : (int?)null)
                && entries.OfType<EM>().Count(m => m.Type == MetaDataType.TARGET && m.Info.SequenceEqual(new[] { a.Id })) == 1
                && entries.OfType<EM>().Count(m => m.Type == MetaDataType.DAMAGE) == 1,
                "instance trigger/deathrattle supplies resolved selected target range");
        }

        var attack = new Fixture(); var attacker = attack.Summon(attack.Player, "CS2_200");
        var defender = attack.Summon(attack.Player.Opponent, "CS2_200");
        attacker.AttackDamage = defender.AttackDamage = 1; attack.Ready("repeated-attack");
        for (int i = 0; i < 2; i++)
        {
            Generic.AttackBlock(attack.Player, attacker, defender, true, true); attack.Flush();
            Check(attack.Game.ProposedAttacker == attacker.Id && attack.Game.ProposedDefender == defender.Id,
                "wire proposal resets preserve rule-visible engine proposals after every attack");
        }
        Check(!attack.Session.History.Findings.Any(v => v.Rule.StartsWith("ATTACK_")),
            "repeated identical participants still publish all attack markers and resets");
        Console.WriteLine("PASS history emission ranges: five tasks, EffectContext batch, header dedup, repeated/nested hits, packet drain, attack state preservation");
    }

    private static void ReviewerRegressions()
    {
        var failures = new List<string>();
        void Proof(string name, Action test)
        {
            try { test(); Console.WriteLine("PASS history-shape regression " + name); }
            catch (InvalidOperationException error) { failures.Add(name + ": " + error.Message); }
        }
        Proof("BotChoiceOffers", () => {
            var f = new Fixture(); var bot = f.Player.Opponent; var source = f.Summon(bot, "CS2_120");
            f.Ready("choice-offers");
            Generic.CreateChoiceCards(bot, source, null, ChoiceType.GENERAL, ChoiceAction.HAND,
                new[] { Cards.FromId("TRL_504"), Cards.FromId("LOOT_150"), Cards.FromId("CS2_150") }, null);
            var offers = bot.Choice.Choices.ToArray(); f.Flush();
            Check(f.Session.History.Findings.Count(v => v.Rule == "PRIVACY") == 0, "all three bot SETASIDE offers stay private");
            // A picked offer named immediately before its public move is the identity-before-move order, not a leak;
            // an unpicked offer named on its way to the GRAVEYARD is.
            var picked = Full(offers[1], "LOOT_150", zone: Zone.SETASIDE, owner: 2);
            Feed(f.Session.History, new PowerHistoryData { ShowEntity = picked.FullEntity }, TC(offers[1], GameTag.ZONE, (int)Zone.PLAY));
            var retired = Full(offers[2], "CS2_150", zone: Zone.SETASIDE, owner: 2);
            Feed(f.Session.History, new PowerHistoryData { ShowEntity = retired.FullEntity }, TC(offers[2], GameTag.ZONE, (int)Zone.GRAVEYARD));
            var changed = Full(offers[0], "CS2_182", zone: Zone.SETASIDE, owner: 2);
            Feed(f.Session.History, new PowerHistoryData { ChangeEntity = changed.FullEntity });
            f.Session.History.Finish();
            var leaks = f.Session.History.Findings.Where(v => v.Rule == "PRIVACY").ToArray();
            Check(leaks.Length == 2 && leaks.Any(v => v.VictimCard == "CS2_182") && leaks.Any(v => v.VictimCard == "CS2_150"),
                "named CHANGE and a named retired offer are leaks; a named-then-summoned pick is not: " + string.Join(",", leaks.Select(v => v.VictimCard)));
            // Retiring to REMOVEDFROMGAME clears nothing either.
            var removedFixture = new Fixture(); var removedBot = removedFixture.Player.Opponent; var removedSource = removedFixture.Summon(removedBot, "CS2_120");
            removedFixture.Ready("choice-offers-removed");
            Generic.CreateChoiceCards(removedBot, removedSource, null, ChoiceType.GENERAL, ChoiceAction.HAND,
                new[] { Cards.FromId("TRL_504"), Cards.FromId("LOOT_150"), Cards.FromId("CS2_150") }, null);
            var removedOffer = removedBot.Choice.Choices[0]; removedFixture.Flush();
            var removed = Full(removedOffer, "TRL_504", zone: Zone.SETASIDE, owner: 2);
            Feed(removedFixture.Session.History, new PowerHistoryData { ShowEntity = removed.FullEntity }, TC(removedOffer, GameTag.ZONE, (int)Zone.REMOVEDFROMGAME));
            removedFixture.Session.History.Finish();
            Check(removedFixture.Session.History.Findings.Any(v => v.Rule == "PRIVACY" && v.VictimCard == "TRL_504"), "an offer named on its way out of the game still leaks");
        });
        Proof("PublicQuestAndPrivateSecret", () => {
            var quest = Seed(); var show = Show(16, "END_017", Zone.HAND);
            show.ShowEntity.Tags.Add(new() { Name = (int)GameTag.QUEST, Value = 1 });
            Feed(quest, Start(7, 16, 0), show, Start(3, 16, 0), End(), TC(16, GameTag.ZONE, (int)Zone.SECRET), End());
            quest.Finish(); Check(!quest.Findings.Any(v => v.Rule == "PRIVACY"), "QUEST=1 is public in SECRET");
            var sidequest = Seed(); var sidequestShow = new PowerHistoryData { ShowEntity = Full(16, "TLC_EVENT_400", CardType.SPELL, Zone.HAND, 2).FullEntity };
            sidequestShow.ShowEntity.Tags.Add(new() { Name = 1192, Value = 1 });
            Feed(sidequest, Start(7, 16, 0), sidequestShow, Start(3, 16, 0), End(), TC(16, GameTag.ZONE, (int)Zone.SECRET), End());
            sidequest.Finish(); Check(!sidequest.Findings.Any(v => v.Rule == "PRIVACY"), "SIDE_QUEST=1 is public after entering SECRET");
            foreach(var zone in new[]{Zone.HAND,Zone.DECK}) {
                var hiddenSidequest = Seed(); var hiddenShow = new PowerHistoryData { ShowEntity = Full(16, "TLC_EVENT_400", CardType.SPELL, zone, 2).FullEntity };
                hiddenShow.ShowEntity.Tags.Add(new() { Name = 1192, Value = 1 });
                Feed(hiddenSidequest, hiddenShow); hiddenSidequest.Finish();
                Check(hiddenSidequest.Findings.Any(v => v.Rule == "PRIVACY"), "unplayed Sidequest stays private in "+zone);
            }
            var objective = Seed(); var objectiveShow = new PowerHistoryData { ShowEntity = Full(16, "BE_005", CardType.SPELL, Zone.SECRET, 2).FullEntity };
            objectiveShow.ShowEntity.Tags.Add(new() { Name = 2311, Value = 1 });
            Feed(objective, objectiveShow); objective.Finish();
            Check(!objective.Findings.Any(v => v.Rule == "PRIVACY"), "OBJECTIVE=1 is public in SECRET");
            // An automatic cast (Ohnahra) moves a Sigil from HAND straight to SECRET, with no public zone between.
            var sigil = Seed(); var sigilShow = new PowerHistoryData { ShowEntity = Full(16, "CATA_528", CardType.SPELL, Zone.HAND, 2).FullEntity };
            sigilShow.ShowEntity.Tags.Add(new() { Name = 1749, Value = 1 });
            Feed(sigil, Start(5, 15, 0), sigilShow, Start(7, 16, 0), TC(16, GameTag.ZONE, (int)Zone.SECRET), End(), End());
            sigil.Finish(); Check(!sigil.Findings.Any(v => v.Rule == "PRIVACY"), "SIGIL=1 is public after entering SECRET");
            var heldSigil = Seed(); var heldShow = new PowerHistoryData { ShowEntity = Full(16, "CATA_528", CardType.SPELL, Zone.HAND, 2).FullEntity };
            heldShow.ShowEntity.Tags.Add(new() { Name = 1749, Value = 1 });
            Feed(heldSigil, heldShow); heldSigil.Finish();
            Check(heldSigil.Findings.Any(v => v.Rule == "PRIVACY"), "an unplayed Sigil stays private in HAND");
            var secret = Seed(); var secretShow = Show(16, "EX1_287", Zone.HAND);
            secretShow.ShowEntity.Tags.Add(new() { Name = 2311, Value = 1 });
            secretShow.ShowEntity.Tags.Add(new() { Name = 1192, Value = 1 });
            secretShow.ShowEntity.Tags.Add(new() { Name = 1749, Value = 1 });
            Feed(secret, Start(7, 16, 0), secretShow, TC(16, GameTag.ZONE, (int)Zone.PLAY), TC(16, GameTag.ZONE, (int)Zone.SECRET), End());
            secret.Finish(); Check(secret.Findings.Any(v => v.Rule == "PRIVACY"), "actual secret must remain anonymous");
            var activated = Seed(); var activation = Show(16, "CORE_BAR_812", Zone.SECRET);
            Feed(activated, Start(5, 15, 0), activation, Start(5, 16, 0), End(), End()); activated.Finish();
            Check(!activated.Findings.Any(v => v.Rule == "PRIVACY"), "secret activation explicitly reveals its identity");
            var known = Seed(); Feed(known, TC(13, GameTag.CONTROLLER, 2), TC(13, GameTag.ZONE, (int)Zone.HAND),
                Show(13, "CS2_200", Zone.HAND)); known.Finish();
            Check(!known.Findings.Any(v => v.Rule == "PRIVACY"), "previously public identity remains known without HIDE");
            var buff = Seed(); var enchantment = Full(17, "EX1_004e", CardType.ENCHANTMENT, Zone.PLAY, 2);
            enchantment.FullEntity.Tags.Add(new() { Name = (int)GameTag.ATTACHED, Value = 16 });
            Feed(buff, Start(3, 15, 0), enchantment, End()); buff.Finish();
            Check(!buff.Findings.Any(v => v.Rule == "PRIVACY"), "public modifier name is not underlying hidden card identity");
        });
        Proof("MinionSelfDamageNotFatigue", () => {
            var f = new Fixture(); var minion = f.Summon(f.Player, "CS2_200"); f.Ready("minion-self-damage");
            minion.TakeDamage(minion, 2); f.Flush();
            Check(!f.Session.History.Findings.Any(v => v.Rule == "FATIGUE_BLOCK"), "minion self-damage is not fatigue");
            f.Player.Hero.TakeDamage(f.Player.Hero, 1); f.Flush();
            Check(f.Session.History.Findings.Count(v => v.Rule == "FATIGUE_BLOCK") == 1, "hero self-damage retains fatigue provenance");
        });
        Proof("ConvertedHeroHealingIsNotFatigue", () => {
            var f = new Fixture(); f.Player.RestoreToDamage = true; f.Player.Hero.Fatigue = 2;
            f.Ready("converted-hero-healing"); int damage = f.Player.Hero.Damage;
            f.Player.Hero.TakeHeal(f.Player.Hero, 3); f.Flush();
            Check(f.Player.Hero.Damage == damage + 3 && f.Player.Hero.Fatigue == 2,
                "converted hero healing damages without changing fatigue");
            Check(!f.Session.History.Findings.Any(v => v.Rule == "FATIGUE_BLOCK"), "converted hero healing is not fatigue");
        });
        Proof("DeathListenerNotDeathrattle", () => {
            var f = new Fixture(); var acolyte = f.Summon(f.Player, "CORE_RLK_121");
            var undead = f.Summon(f.Player, "RLK_958"); f.Ready("death-listener"); int deck = f.Player.DeckZone.Count;
            undead.Destroy(); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(f.Player.DeckZone.Count == deck - 1, "Acolyte death listener really executed");
            Check(!f.Session.History.Findings.Any(v => v.Rule == "TRIGGER_KEYWORD" && v.BlockCard == acolyte.Card.Id),
                "ordinary death listener needs no DEATHRATTLE keyword");
        });
        Proof("PublicTransformRetainsPrivateIdentity", () => {
            foreach (bool split in new[] { false, true })
            foreach (Zone changedZone in new[] { Zone.HAND, Zone.PLAY })
            {
                var s = Seed(); Feed(s, Start(5, 15, 0), Show(16, "CS2_182", Zone.HAND));
                var change = new PowerHistoryData { ChangeEntity = Full(16, "CS2_120", zone: changedZone, owner: 2).FullEntity };
                if (split) { Feed(s, change); Feed(s, TC(16, GameTag.ZONE, (int)Zone.PLAY), End()); }
                else Feed(s, change, TC(16, GameTag.ZONE, (int)Zone.PLAY), End());
                s.Finish();
                var leaks = s.Findings.Where(v => v.Rule == "PRIVACY").ToArray();
                Check(leaks.Length == 1 && leaks[0].VictimCard == "CS2_182", "public Y must not absolve private X, including split packets");
            }
            var same = Seed(); Feed(same, Start(5, 15, 0), Show(16, "CS2_182", Zone.HAND),
                new() { ChangeEntity = Full(16, "CS2_182", zone: Zone.PLAY, owner: 2).FullEntity }, End()); same.Finish();
            Check(same.Findings.Count == 0, "public move still resolves same-identity disclosure");
            foreach (bool secret in new[] { false, true })
            {
                var s = Seed(); Feed(s, Start(5, 15, 0), Show(16, "CS2_182", Zone.HAND),
                    new() { ChangeEntity = Full(16, "EX1_287", CardType.SPELL, secret ? Zone.SECRET : Zone.HAND, 2).FullEntity });
                if (secret) Feed(s, Start(5, 16, 0), End());
                else Feed(s, new PowerHistoryData { HideEntity = new() { Entity = 16, Zone = (int)Zone.HAND } });
                Feed(s, End()); s.Finish();
                Check(s.Findings.Count(v => v.Rule == "PRIVACY" && v.VictimCard == "CS2_182") == 1
                    && s.Findings.Count(v => v.Rule == "PRIVACY") == 1, "HIDE/secret activation of Y cannot absolve X");
            }
        });
        Proof("ExplicitGrantedDeathrattle", () => {
            foreach (bool swimmer in new[] { false, true })
            {
                var f = new Fixture(); var host = f.Summon(f.Player, "CS2_182");
                Generic.AddEnchantmentBlock(f.Game, Cards.FromId("JAIL_906e"), host, host);
                f.Ready("explicit-granted-deathrattle");
                if (swimmer)
                    Cards.FromId("JAIL_395").Power.PowerTask.Process(f.Game, f.Player, host, host, new TaskStack());
                else
                    new ActivateDeathrattleTask(EntityType.SOURCE).Process(f.Game, f.Player, host, null, new TaskStack());
                f.Game.ProcessTasks(); f.Flush();
                Check(host.Zone == f.Player.BoardZone && f.Player.BoardZone.Count(m => m.Card.Id == "JAIL_906") == 1,
                    "granted Deathrattle really activated without host death");
                Check(f.Session.History.Findings.Count(v => v.Rule == "TRIGGER_KEYWORD" && v.BlockCard == host.Card.Id) == 0,
                    "explicit granted Deathrattle requires TRIGGER keyword 217 without a death ID");
            }
        });
        Proof("SharedBattlecryNotDeathrattle", () => {
            foreach (string card in new[] { "CORE_DMF_067", "EDR_847" })
            {
                var f = new Fixture(); var host = f.Summon(f.Player, card); f.Ready("shared-battlecry");
                host.ActivateTask(PowerActivation.POWER); f.Game.ProcessTasks(); f.Flush();
                Check(!f.Session.History.Findings.Any(v => v.Rule == "TRIGGER_KEYWORD"), "shared Battlecry body needs no DEATHRATTLE keyword");
                f.Ready("explicit-shared-deathrattle");
                new ActivateDeathrattleTask(EntityType.SOURCE).Process(f.Game, f.Player, host, null, new TaskStack());
                f.Game.ProcessTasks(); f.Flush();
                Check(host.Zone == f.Player.BoardZone && f.Session.History.Findings.Count(v => v.Rule == "TRIGGER_KEYWORD") == 0,
                    "same shared body explicitly activated as Deathrattle still requires keyword");
            }
        });
        Proof("SubSpellExplicitTargetBuff", () => {
            // Synthetic shape: entities 7/21/77 already exist before this POWER. No TARGET metadata; the sub-spell names its targets.
            PowerHistoryEntity Snapshot(int id, string card, params (GameTag Tag, int Value)[] tags) => new() {
                Entity = id, Name = card, Tags = tags.Select(t => new Tag { Name = (int)t.Tag, Value = t.Value }).ToList()
            };
            var s = new HistoryShape(); Feed(s, Full(7, "TSC_215", CardType.SPELL, Zone.HAND), Full(21, "TSC_052"), Full(77, "HERO_09r", CardType.HERO));
            Feed(s, Start(3, 7, 21), new() { SubSpellStart = new() {
                SpellPrefabGuid = "ReuseFX_Synthetic_Impact:00000000000000000000000000000001", SourceEntityId = 77, TargetEntityIds = new() { 21 }
            } });
            Feed(s, new() { FullEntity = Snapshot(134, "", (GameTag.ZONE, (int)Zone.SETASIDE), (GameTag.CONTROLLER, 1), (GameTag.ENTITY_ID, 134)) },
                new() { ShowEntity = Snapshot(134, "TSC_215e", (GameTag.CONTROLLER, 1), (GameTag.CARDTYPE, (int)CardType.ENCHANTMENT),
                    (GameTag.PREMIUM, 1), (GameTag.ATTACHED, 21), (GameTag.ZONE, (int)Zone.SETASIDE), (GameTag.ENTITY_ID, 134), (GameTag.CREATOR, 7)) },
                TC(134, GameTag.ZONE, (int)Zone.PLAY),
                TC(21, GameTag.HEALTH, 6), TC(21, GameTag.ATK, 5), new() { SubSpellEnd = new() },
                new() { SubSpellStart = new() { SpellPrefabGuid = "ReuseFX_Synthetic_SpawnToHand:00000000000000000000000000000002", SourceEntityId = 21 } },
                new() { FullEntity = Snapshot(135, "TSC_215", (GameTag.CONTROLLER, 1), (GameTag.CARDTYPE, (int)CardType.SPELL),
                    (GameTag.COST, 1), (GameTag.ZONE, (int)Zone.HAND), (GameTag.ENTITY_ID, 135),
                    (GameTag.RARITY, (int)Rarity.EPIC), (GameTag.ZONE_POSITION, 5), (GameTag.CREATOR, 7)) },
                TC(135, GameTag.DISPLAYED_CREATOR, 7), new() { SubSpellEnd = new() }, End()); s.Finish();
            Check(s.Findings.Count == 0, "explicit sub-spell targeting buff and spawn-to-hand needs no TARGET");
        });
        Proof("SubSpellTargetScope", () => {
            PowerHistoryData Sub(int target) => new() { SubSpellStart = new() {
                SpellPrefabGuid = "ReuseFX_Arcane_Impact_Shimmer_Super:4aef9b1bcd17c4d4cb3de76f139730e5", TargetEntityIds = new() { target }
            } };
            foreach (string variant in new[] { "absent", "wrong", "expired", "parent", "late", "sibling", "nested", "empty-prefab" })
            {
                var s = Seed(); Feed(s, Start());
                if (variant != "absent" && variant != "late") Feed(s, Sub(variant == "wrong" ? 13 : 11));
                if (variant == "expired") Feed(s, new PowerHistoryData { SubSpellEnd = new() });
                if (variant == "parent") Feed(s, Start());
                if (variant == "sibling") Feed(s, new() { SubSpellEnd = new() }, Sub(13));
                if (variant == "nested") Feed(s, Sub(13));
                if (variant == "empty-prefab")
                {
                    var empty = Sub(11); empty.SubSpellStart.SpellPrefabGuid = "";
                    Feed(s, new() { SubSpellEnd = new() }, empty);
                }
                Feed(s, TC(11, GameTag.HEALTH, 6));
                if (variant == "late") Feed(s, Sub(11));
                if (variant == "parent") Feed(s, End());
                if (variant == "nested") Feed(s, new PowerHistoryData { SubSpellEnd = new() });
                if (variant != "absent" && variant != "expired") Feed(s, new PowerHistoryData { SubSpellEnd = new() });
                Feed(s, End()); s.Finish();
                Check(s.Findings.Any(v => v.Rule == "TARGET_META"), variant + " sub-spell cannot authorize effect");
            }
            var restored = Seed(); Feed(restored, Start(), Sub(11), Sub(13), TC(13, GameTag.HEALTH, 6), new() { SubSpellEnd = new() });
            Feed(restored, TC(11, GameTag.HEALTH, 6), new() { SubSpellEnd = new() }, End()); restored.Finish();
            Check(restored.Findings.Count == 0, "nested sub-spell end restores enclosing explicit targets across packets");
        });
        Proof("SelfTargetedDrawBookkeeping", () => {
            // Synthetic shape: a self-targeted inner POWER performs draw bookkeeping, no TARGET.
            var s = Seed(); Feed(s, Full(27, "SW_046", CardType.SPELL, Zone.HAND), Start(13, 27, 0),
                TC(27, GameTag.CARD_TARGET, 27), TC(27, (GameTag)2045, 1), TC(27, GameTag.REVEALED, 1),
                TC(27, GameTag.ZONE, (int)Zone.DECK), Start(3, 27, 27), Show(32, "CORE_CS2_093", Zone.HAND, 1),
                TC(32, GameTag.ZONE_POSITION, 1), TC(1, GameTag.NUM_CARDS_DRAWN_THIS_TURN, 2), End(),
                new() { HideEntity = new() { Entity = 27, Zone = (int)Zone.DECK } }, TC(27, (GameTag)2045, 0), TC(27, GameTag.REVEALED, 0), End());
            s.Finish(); Check(s.Findings.Count == 0, "self-targeted draw bookkeeping needs no TARGET");
        });
        Proof("DamageRequiresEffectContext", () => {
            foreach (bool play in new[] { false, true })
            {
                var s = Seed(); s.RequireCorrelation = true; var hit = Hit();
                if (play) Feed(s, Start(7));
                s.RegisterHit(hit); s.RecordMutation(11, GameTag.DAMAGE, 8, hit);
                Feed(s, Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 8));
                if (play) Feed(s, End());
                Check(hit.Observed && s.Findings.Any(v => v.Rule == "DAMAGE_META"), "hit outside effect wrapper rejected even with metadata");
            }
        });
        Proof("SplitRevealAndMill", () => {
            var reveal = Seed(); Feed(reveal, Start(10, 10, 16), Show(16, "CS2_182", Zone.DECK));
            Check(!reveal.Findings.Any(v => v.Rule == "PRIVACY"), "open reveal waits across packet boundary");
            Feed(reveal, new() { HideEntity = new() { Entity = 16, Zone = (int)Zone.DECK } }, End()); reveal.Finish();
            Check(reveal.Findings.Count == 0, "next-packet HIDE resolves reveal");
            var mill = Seed(); Feed(mill, Start(5, 15, 0));
            Feed(mill, Show(16, "CS2_182", Zone.GRAVEYARD), End()); mill.Finish();
            Check(mill.Findings.Count == 0, "named destination SHOW mills across packets");
            var refresh = Seed(); Feed(refresh, Show(16, "CS2_182", Zone.GRAVEYARD));
            Feed(refresh, Show(16, "CS2_182", Zone.GRAVEYARD)); refresh.Finish();
            Check(refresh.Findings.Count == 0, "public graveyard snapshot refresh is not another zone move");
            var cast = Seed(); Feed(cast, Show(16, "CS2_182", Zone.DECK));
            Feed(cast, Start(7, 16, 0), TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD), End()); cast.Finish();
            Check(cast.Findings.Count == 0, "automatic PLAY retirement retains its own earlier DECK reveal");
            var wrongCast = Seed(); Feed(wrongCast, Show(16, "CS2_182", Zone.DECK), Start(7, 15, 0),
                TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD), End()); wrongCast.Finish();
            Check(wrongCast.Findings.Any(v => v.Rule == "MILL_ORDER"), "another actor's PLAY cannot exempt a mill");
            var hiddenCast = Seed(); Feed(hiddenCast, Show(16, "CS2_182", Zone.DECK),
                new() { HideEntity = new() { Entity = 16, Zone = (int)Zone.DECK } });
            Feed(hiddenCast, Start(7, 16, 0), TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD), End()); hiddenCast.Finish();
            Check(hiddenCast.Findings.Any(v => v.Rule == "MILL_ORDER"), "HIDE expires automatic cast reveal evidence");
            var hidden = Seed(); Feed(hidden, Show(16, "CS2_182", Zone.DECK), new() { HideEntity = new() { Entity = 16, Zone = (int)Zone.DECK } });
            Feed(hidden, TC(16, GameTag.ZONE, (int)Zone.GRAVEYARD)); hidden.Finish();
            Check(hidden.Findings.Any(v => v.Rule == "MILL_ORDER"), "old reveal before HIDE cannot authorize a later mill");
            var reshown = Seed(); Feed(reshown, Show(16, "CS2_182", Zone.DECK), new() { HideEntity = new() { Entity = 16, Zone = (int)Zone.DECK } });
            Feed(reshown, Show(16, "CS2_182", Zone.GRAVEYARD)); reshown.Finish();
            Check(reshown.Findings.Count == 0, "fresh destination SHOW after a temporary reveal is valid");
            var recycled = Seed(); Feed(recycled, Show(16, "CS2_182", Zone.GRAVEYARD), TC(16, GameTag.ZONE, (int)Zone.HAND));
            Feed(recycled, new PowerHistoryData { HideEntity = new() { Entity = 16, Zone = (int)Zone.DECK } }); recycled.Finish();
            Check(recycled.Findings.Count == 0, "later genuine movement ends burn presentation lifetime");
            var joust = Seed(); Feed(joust, Start(2, 15, 0), Show(16, "CS2_182", Zone.DECK)); Feed(joust, End()); joust.Finish();
            Check(joust.Findings.Count == 0, "Joust is public reveal intent");
            var text = Seed(); Feed(text, TC(16, GameTag.REVEALED, 1), Show(16, "CS2_182", Zone.DECK)); text.Finish();
            Check(text.Findings.Count == 0, "revealed-by-text identity is public");
        });
        Proof("RepeatedHitTargetRanges", () => {
            foreach (int target in new[] { 0, 11 })
            {
                var s = Seed(); Feed(s, Start(target: target), Meta(HistoryMeta.Type.TARGET, 0, 11),
                    Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 6));
                Feed(s, Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 7), End()); s.Finish();
                Check(s.Findings.Any(v => v.Rule == "TARGET_META"), "second hit needs its own TARGET, including repeated header victim");
            }
            var minion = Seed(); Feed(minion, Start(3, 15, 0), Meta(HistoryMeta.Type.TARGET, 0, 11),
                Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 6),
                Meta(HistoryMeta.Type.DAMAGE, 1, 11), TC(11, GameTag.DAMAGE, 7), End());
            Check(minion.Findings.Any(v => v.Rule == "TARGET_META"), "Astalor minion repeated victim needs another TARGET");
            var late = Seed(); Feed(late, Start(), Meta(HistoryMeta.Type.DAMAGE, 1, 11), Meta(HistoryMeta.Type.TARGET, 0, 11),
                TC(11, GameTag.DAMAGE, 6), End());
            Check(late.Findings.Any(v => v.Rule == "TARGET_META"), "TARGET must precede DAMAGE metadata, not just tag mutation");
        });
        Proof("NestedAttackOwnership", () => {
            void Mark(HistoryShape s, int a, int d, int value) => Feed(s,
                TC(1, GameTag.PROPOSED_ATTACKER, value == 0 ? 0 : a), TC(1, GameTag.PROPOSED_DEFENDER, value == 0 ? 0 : d),
                TC(a, GameTag.ATTACKING, value), TC(d, GameTag.DEFENDING, value));
            var s = Seed(); Feed(s, Start(1, 15, 11)); Mark(s, 15, 11, 1);
            Feed(s, Start(1, 13, 12)); Mark(s, 13, 12, 1); Mark(s, 13, 12, 0); Feed(s, End());
            s.RecordMutation(15, GameTag.DAMAGE, 2, new() { Source = 11, Victim = 15, Amount = 2 });
            Feed(s, Meta(HistoryMeta.Type.DAMAGE, 2, 15), TC(15, GameTag.DAMAGE, 2)); Mark(s, 15, 11, 0); Feed(s, End()); s.Finish();
            Check(s.Findings.Count == 0, "nested attack cannot overwrite outer combatants or retaliation");
            var missing = Seed(); Feed(missing, Start(1, 15, 11), Start(1, 15, 11)); Mark(missing, 15, 11, 1);
            Mark(missing, 15, 11, 0); Feed(missing, End(), End()); missing.Finish();
            Check(missing.Findings.Any(v => v.Rule == "ATTACK_SHAPE"), "child flags cannot satisfy missing parent flags");
            var proposed = Seed(); Feed(proposed, Start(1, 15, 11), TC(15, GameTag.ATTACKING, 1), TC(11, GameTag.DEFENDING, 1),
                TC(15, GameTag.ATTACKING, 0), TC(11, GameTag.DEFENDING, 0), End()); proposed.Finish();
            Check(!proposed.Findings.Any(v => v.Rule == "ATTACK_SHAPE") && proposed.Findings.Any(v => v.Rule == "ATTACK_PROPOSED"),
                "proposal defect buckets separately from actual attack markers");
            var redirected = Seed(); redirected.RecordBlock(new() { Type = 1, Source = 15, Target = 13 });
            Feed(redirected, Start(1, 15, 11)); Mark(redirected, 15, 11, 1);
            Feed(redirected, TC(1, GameTag.PROPOSED_DEFENDER, 13));
            redirected.RecordMutation(15, GameTag.DAMAGE, 2, new() { Source = 13, Victim = 15, Amount = 2 });
            Feed(redirected, Meta(HistoryMeta.Type.DAMAGE, 2, 15), TC(15, GameTag.DAMAGE, 2));
            Mark(redirected, 15, 13, 0); Feed(redirected, End());
            Check(redirected.Findings.Any(v => v.Rule == "ATTACK_SHAPE") && !redirected.Findings.Any(v => v.Rule == "FOREIGN_SOURCE"),
                "redirected combat provenance requires final defender's flags without rejecting valid retaliation");
        });
        Check(failures.Count == 0, string.Join("\n", failures));
    }
    private static void TriggerHitsAndPoisonous()
    {
        var spore = new Fixture(); var source = spore.Summon(spore.Player, "EDR_110");
        var victim = spore.Summon(spore.Player.Opponent, "CS2_200");
        spore.Ready("sporegnasher"); int cursor = spore.Game.PowerHistory.Full.Count;
        source.Destroy(); spore.Game.DeathProcessingAndAuraUpdate(); spore.Flush();
        var entries = spore.Game.PowerHistory.Full.Skip(cursor).ToArray();
        int damage = Array.FindIndex(entries, e => e is EM m && m.Type == MetaDataType.DAMAGE);
        Check(damage > 0 && entries.Take(damage).OfType<EM>().Any(m => m.Type == MetaDataType.TARGET && m.Info.SequenceEqual(new[] { victim.Id })),
            "registered Sporegnasher deathrattle has resolved random-hit TARGET before DAMAGE");
        CheckPoison(entries, source.Id, victim.Id, "Sporegnasher spell-like hit");
        Check(victim.Zone.Type == Zone.GRAVEYARD, "Sporegnasher Poisonous result preserved");
        Check(spore.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "Sporegnasher checker clean");
        HistoryShapeReport.Add(spore.Session.History);

        foreach (bool corrupt in new[] { false, true })
        {
            var lethal = new Fixture(); var poisonous = lethal.Summon(lethal.Player, "EDR_110");
            var fragile = lethal.Summon(lethal.Player.Opponent, "CS2_231");
            lethal.Ready("lethal-poison-metadata-" + corrupt); int first = lethal.Game.PowerHistory.Full.Count;
            poisonous.Destroy(); lethal.Game.DeathProcessingAndAuraUpdate();
            Check(fragile.Zone.Type == Zone.GRAVEYARD && fragile.Health <= 0, "lethal Poisonous hit still destroys");
            CheckPoison(lethal.Game.PowerHistory.Full.Skip(first).ToArray(), poisonous.Id, fragile.Id, "lethal Sporegnasher");
            if (corrupt) lethal.Game.PowerHistory.Last.RemoveAll(e => e is EM { Type: MetaDataType.POISONOUS });
            lethal.Flush();
            Check(corrupt ? lethal.Session.History.Findings.Any(f => f.Rule == "POISONOUS_KEYWORD" && f.Severity == "error")
                : lethal.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "pre-hit oracle detects absent POISONOUS meta on lethal damage");
            if (!corrupt) HistoryShapeReport.Add(lethal.Session.History);
        }

        var cannon = new Fixture(); var cannoneer = cannon.Summon(cannon.Player, "CAP_107t");
        cannon.Summon(cannon.Player, "CAP_106"); cannon.Player.Opponent.Hero.Health = 100;
        cannon.Ready("cannoneer-repeated"); cursor = cannon.Game.PowerHistory.Full.Count;
        cannon.Act(EndTurnTask.Any(cannon.Player));
        var shots = cannon.Game.PowerHistory.Full.Skip(cursor).OfType<EM>().Where(m => m.Type is MetaDataType.TARGET or MetaDataType.DAMAGE).ToArray();
        Check(shots.Length == 4 && shots.Select(m => m.Type).SequenceEqual(new[] { MetaDataType.TARGET, MetaDataType.DAMAGE, MetaDataType.TARGET, MetaDataType.DAMAGE })
            && shots.All(m => m.Info.SequenceEqual(new[] { cannon.Player.Opponent.Hero.Id })), "registered Cannoneer preserves two occurrences of same random victim");
        Check(cannon.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "Cannoneer checker clean");
        HistoryShapeReport.Add(cannon.Session.History);
        foreach (var kind in new[] { BlockType.POWER, BlockType.TRIGGER })
        {
            cannon.Ready("foreign-selected-hit"); cursor = cannon.Game.PowerHistory.Full.Count;
            var parent = cannon.Player.BoardZone.Single(m => m.Card.Id == "CAP_106");
            cannon.Game.PowerHistory.Add(EH.BlockStart(kind, parent.Id, "", -1, 0));
            new RandomTask(1, EntityType.OP_HERO).Process(cannon.Game, cannon.Player, parent, null, new TaskStack());
            cannon.Player.Opponent.Hero.TakeDamage(cannoneer, 1);
            cannon.Game.PowerHistory.Add(EH.BlockEnd()); cannon.Flush();
            var attributed = cannon.Game.PowerHistory.Full.Skip(cursor).ToArray();
            Check(attributed.OfType<EB>().Any(b => b.BlockType == BlockType.TRIGGER && b.Source == cannoneer.Id && b.EffectIndex == -1)
                && attributed.OfType<EM>().Count(m => m.Type == MetaDataType.TARGET && m.Info.SequenceEqual(new[] { cannon.Player.Opponent.Hero.Id })) == 1,
                kind + " resolved selection survives foreign-source attribution TRIGGER");
            Check(cannon.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "foreign selected-hit checker clean");
        }

        var aoe = new Fixture(); var aoeSource = aoe.Summon(aoe.Player, "CAP_107t");
        var aoeVictims = new[] { aoe.Summon(aoe.Player.Opponent, "CS2_200"), aoe.Summon(aoe.Player.Opponent, "CS2_200") };
        aoe.Ready("trigger-aoe"); cursor = aoe.Game.PowerHistory.Full.Count;
        var aoeStack = new TaskStack(); aoe.Game.PowerHistory.Add(EH.BlockStart(BlockType.TRIGGER, aoeSource.Id, "", -1, aoeVictims[0].Id));
        new RandomTask(1, EntityType.OP_MINIONS).Process(aoe.Game, aoe.Player, aoeSource, null, aoeStack);
        new IncludeTask(EntityType.OP_MINIONS).Process(aoe.Game, aoe.Player, aoeSource, null, aoeStack);
        new DamageTask(1, EntityType.STACK).Process(aoe.Game, aoe.Player, aoeSource, null, aoeStack);
        aoe.Game.PowerHistory.Add(EH.BlockEnd()); aoe.Flush();
        Check(!aoe.Game.PowerHistory.Full.Skip(cursor).OfType<EM>().Any(m => m.Type == MetaDataType.TARGET)
            && aoeVictims.All(v => v.Damage == 1), "fixed-set IncludeTask replaces random selection; header target does not authorize AoE TARGET");
        Check(aoe.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "untargeted TRIGGER AoE checker clean");

        foreach (bool defending in new[] { false, true })
        {
            var combat = new Fixture(); var poison = combat.Summon(combat.Player, "BE_193");
            var enemy = combat.Summon(combat.Player.Opponent, "CS2_200");
            poison.AttackDamage = 1; poison.Health = 20; enemy.AttackDamage = 1; enemy.Health = 20;
            if (defending) combat.Act(EndTurnTask.Any(combat.Player));
            combat.Ready("poisonous-combat"); cursor = combat.Game.PowerHistory.Full.Count;
            combat.Act(defending ? MinionAttackTask.Any(combat.Player.Opponent, enemy, poison) : MinionAttackTask.Any(combat.Player, poison, enemy));
            CheckPoison(combat.Game.PowerHistory.Full.Skip(cursor).ToArray(), poison.Id, enemy.Id, "Bloatray combat " + defending);
            Check(enemy.Zone.Type == Zone.GRAVEYARD && poison.Zone.Type == Zone.PLAY, "combat Poisonous result preserved");
            Check(combat.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "combat Poisonous checker clean");
            HistoryShapeReport.Add(combat.Session.History);
        }
        var broken = new Fixture(); var brokenSource = broken.Summon(broken.Player, "EDR_110");
        broken.Summon(broken.Player.Opponent, "CS2_200"); broken.Ready("corrupt-trigger-wire");
        brokenSource.Destroy(); broken.Game.DeathProcessingAndAuraUpdate();
        // Corrupt only unsent presentation entries after record-only hooks have captured real resolution.
        broken.Game.PowerHistory.Last.RemoveAll(e => e is EM { Type: MetaDataType.TARGET });
        // EDR_110's damage cue also names its victims; a hit counts as targeted by TARGET or sub-spell target, so strip both.
        foreach (var cue in broken.Game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>()) cue.Targets.Clear();
        foreach (var block in broken.Game.PowerHistory.Last.OfType<EB>().Where(b => b.TriggerKeyword == 363)) block.TriggerKeyword = null;
        broken.Flush();
        Check(new[] { "TRIGGER_HIT_TARGET", "POISONOUS_KEYWORD" }.All(rule => broken.Session.History.Findings.Any(f => f.Rule == rule && f.Severity == "error")),
            "record-only hooks detect removed TRIGGER targets and Poisonous keyword from actual registered activation");

        var brokenCombat = new Fixture(); var brokenPoison = brokenCombat.Summon(brokenCombat.Player, "BE_193");
        var brokenVictim = brokenCombat.Summon(brokenCombat.Player.Opponent, "CS2_200");
        brokenPoison.AttackDamage = brokenVictim.AttackDamage = 1; brokenPoison.Health = brokenVictim.Health = 20;
        brokenCombat.Ready("corrupt-combat-wire"); Generic.AttackBlock(brokenCombat.Player, brokenPoison, brokenVictim, true, true);
        foreach (var block in brokenCombat.Game.PowerHistory.Last.OfType<EB>().Where(b => b.TriggerKeyword == 363)) block.TriggerKeyword = null;
        brokenCombat.Flush();
        Check(brokenCombat.Session.History.Findings.Any(f => f.Rule == "POISONOUS_KEYWORD" && f.Severity == "error"),
            "combat Poisonous oracle detects missing keyword independently of helper/header");
        Console.WriteLine("PASS history-shape TRIGGER hits/Poisonous: registered Sporegnasher, repeated Cannoneer, fixed-set AoE, Bloatray attacker/defender");
    }
    private static void NestedTriggerDamageDuringCombat()
    {
        var combat = new Fixture();
        var boss = combat.Summon(combat.Player, "BRM_006");
        var juggler = combat.Summon(combat.Player, "NEW1_019");
        juggler[GameTag.POISONOUS] = 1;
        var attacker = combat.Summon(combat.Player.Opponent, "CS2_200");
        var victim = combat.Summon(combat.Player.Opponent, "CS2_200");
        attacker.AttackDamage = 1;
        combat.Act(EndTurnTask.Any(combat.Player));
        combat.Game.SetRandomSeed(3); // First knife hits the noncombatant minion, not the attacker or hero.
        combat.Ready("nested-trigger-damage-during-combat");
        int cursor = combat.Game.PowerHistory.Full.Count;
        combat.Act(MinionAttackTask.Any(combat.Player.Opponent, attacker, boss));
        var entries = combat.Game.PowerHistory.Full.Skip(cursor).ToArray();
        Check(combat.Player.BoardZone.Any(m => m.Card.Id == "BRM_006t"), "combat damage summons Imp Gang Boss's Imp");
        Check(entries.OfType<EB>().Any(b => b.BlockType == BlockType.TRIGGER && b.Source == juggler.Id)
            && victim.Damage == 1 && victim.Zone.Type == Zone.GRAVEYARD,
            "Poisonous Knife Juggler's nested trigger destroys noncombatant minion");
        CheckPoison(entries, juggler.Id, victim.Id, "nested Knife Juggler");
        Check(combat.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "nested trigger damage during combat checker clean");
        HistoryShapeReport.Add(combat.Session.History);
        Console.WriteLine("PASS history-shape nested-trigger-damage-during-combat: Poisonous Knife Juggler, Imp Gang Boss, poisoned victim");
    }
    private static void HeroDamageProvenance()
    {
        var f = new Fixture(); var p = f.Player;
        var knife = Generic.DrawCard(p, Cards.FromId("ULD_715t"));
        f.Act(PlayCardTask.Any(p, knife));
        var victim = f.Summon(p.Opponent, "CS2_200");
        var hagatha = Generic.DrawCard(p, Cards.FromId("GIL_504"));
        f.Ready("hero-damage-provenance");
        f.Act(PlayCardTask.Any(p, hagatha));
        Check(victim.Zone.Type == Zone.PLAY && victim.Damage == 3, "Hagatha with Poisonous weapon leaves damaged minion alive");
        var bolf = f.Summon(p, "AT_124"); f.Flush();
        p.Hero.Armor = 0; p.Hero.TakeDamage(p.Hero, 1); f.Flush();
        Check(bolf.Zone.Type == Zone.PLAY && bolf.Damage == 1, "redirected fatigue leaves Bolf alive");
        var nested = f.Summon(p.Opponent, "CS2_200"); f.Flush();
        victim.PreDamageTrigger += _ => nested.TakeDamage(p.Hero, 1);
        f.Act(HeroAttackTask.Any(p, victim));
        Check(victim.Zone.Type == Zone.GRAVEYARD && nested.Zone.Type == Zone.PLAY && nested.Damage == 1,
            "weapon poisons combat hit while nested hero hit only damages");
        p.Hero[GameTag.POISONOUS] = 1;
        nested.TakeDamage(p.Hero, 1); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(nested.Zone.Type == Zone.GRAVEYARD, "hero entity's Poisonous applies outside combat");
        f.Session.History.Finish();
        Check(f.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "hero combat/non-combat independent Poisonous oracle clean");
        HistoryShapeReport.Add(f.Session.History);
        Console.WriteLine("PASS history-shape hero provenance: Hagatha, redirected fatigue, weapon combat, nested hero damage, hero Poisonous");
    }
    private static void CheckPoison(SabberStoneCore.Kettle.IPowerHistoryEntry[] entries, int source, int victim, string name)
    {
        int start = Array.FindIndex(entries, e => e is EB b && b.BlockType == BlockType.TRIGGER && b.Source == source
            && b.TriggerKeyword == 363 && b.EffectIndex == -1 && b.EffectCardId == "");
        Check(start >= 0 && entries.Skip(start + 1).FirstOrDefault() is EM m && m.Type == MetaDataType.TARGET && m.Info.SequenceEqual(new[] { victim }),
            name + " requests Poisonous actor spell with victim TARGET, suppressing card trigger effect");
        Check(entries.ElementAtOrDefault(start + 2) is EM poison && poison.Type == MetaDataType.POISONOUS
            && poison.Data == 0 && poison.Info.SequenceEqual(new[] { victim }), name + " emits POISONOUS metadata after TARGET");
    }
    private static void DisabledHistoryHooks()
    {
        foreach (bool clone in new[] { false, true })
        {
            var fixture = new Fixture(history: clone);
            var game = clone ? fixture.Game.Clone(history: false, resetRandomSeed: false) : fixture.Game;
            var player = game.Player1;
            Check(clone ? game.History && game.PowerHistory != null : !game.History && game.PowerHistory == null,
                "disabled-history fixture exercises " + (clone ? "disabled recorder clone" : "absent recorder"));
            var stack = new TaskStack();
            new RandomTask(1, EntityType.OP_HERO).Process(game, player, player.Hero, null, stack);
            new DamageTask(3, EntityType.STACK).Process(game, player, player.Hero, null, stack);
            new IncludeTask(EntityType.TARGET).Process(game, player, player.Hero, player.Opponent.Hero, stack);
            new HealTask(2, EntityType.STACK).Process(game, player, player.Hero, null, stack);
            Check(player.Opponent.Hero.Damage == 1, "installed hooks preserve damage/heal/selection without history");
            player.Hero.AttackDamage = 1;
            Generic.AttackBlock(player, player.Hero, player.Opponent.Hero, true, true);
            Check(player.Opponent.Hero.Damage == 2, "installed hooks preserve combat without history");
            Generic.DrawCard(player, Cards.FromId("CS2_029"));
            string before = HsSim.Canonical.State(game);
            var decision = BotSearch.Choose(FairBotState.Create(game, player.PlayerId, 37, DeckStyle.Control), new BotBudget(2, 8, 16, 10000));
            Check(decision.Nodes > 0 && decision.Action != null && HsSim.Canonical.State(game) == before,
                "installed hooks preserve disabled-history AI search and its source");
            if (clone) Check(game.PowerHistory.Full.Count == 0, "AI clone emits no history");
        }
        Console.WriteLine("PASS history-shape disabled history: absent recorder, Clone(history:false), AI search");
    }
    private static void DisabledRecorderDoesNotEnumerateTargets()
    {
        var clone = new Fixture().Game.Clone(history: false, resetRandomSeed: false);
        int enumerated = 0;
        IEnumerable<IPlayable> Targets()
        {
            enumerated++;
            yield return clone.Player2.Hero;
        }
        var method = typeof(SabberStoneCore.Kettle.PowerHistory).GetMethod("TargetRange",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            new[] { typeof(IEnumerable<IPlayable>), typeof(EntityType) }, null);
        method.Invoke(clone.PowerHistory, new object[] { Targets(), EntityType.TARGET });
        Check(enumerated == 0 && clone.PowerHistory.Full.Count == 0, "disabled recorder never enumerates task targets");
        Console.WriteLine("PASS history-shape disabled recorder: lazy targets untouched");
    }
    private static void CustomResolvedHits()
    {
        foreach (bool corrupt in new[] { false, true })
        {
            var f = new Fixture(); var source = f.Summon(f.Player, "TLC_631t");
            for (int i = 0; i < 2; i++) source.Card.Power.PowerTask.Process(f.Game, f.Player, source, null, new TaskStack());
            var effect = f.Player.AppliedEnchantments.Single(e => e.Card.Id == "TLC_631e");
            Check(effect.ScriptTag1 == 2, "registered Colossus listener has two stacks");
            f.Ready("gorishi-repeated"); int cursor = f.Game.PowerHistory.Full.Count;
            f.Game.PowerHistory.Add(EH.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
            new DamageTask(2, EntityType.OP_HERO).Process(f.Game, f.Player, source, null, new TaskStack());
            f.Game.PowerHistory.Add(EH.BlockEnd()); f.Game.ProcessTasks();
            Check(f.Player.Opponent.Hero.Damage == 6, "Colossus original plus two extra hits preserves result");
            if (corrupt)
            {
                // Remove only custom TRIGGER targets after hooks observed the registered recipe's actual hits.
                var blocks = new Stack<int>();
                f.Game.PowerHistory.Last.RemoveAll(e => {
                    if (e is EB b) blocks.Push(b.Source);
                    if (e is SabberStoneCore.Kettle.PowerHistoryBlockEnd) blocks.Pop();
                    return e is EM { Type: MetaDataType.TARGET } && blocks.Count > 0 && blocks.Peek() == effect.Id;
                });
            }
            f.Flush();
            if (corrupt)
                Check(f.Session.History.Findings.Count(v => v.Rule == "TRIGGER_HIT_TARGET" && v.Severity == "error") == 2,
                    "independent custom-hit oracle rejects both removed Gorishi TARGET occurrences");
            else
            {
                var entries = f.Game.PowerHistory.Full.Skip(cursor).ToArray();
                int start = Array.FindIndex(entries, e => e is EB b && b.Source == effect.Id && b.BlockType == BlockType.TRIGGER);
                var hits = entries.Skip(start + 1).OfType<EM>().Where(m => m.Type is MetaDataType.TARGET or MetaDataType.DAMAGE).ToArray();
                Check(start >= 0 && hits.Select(m => m.Type).SequenceEqual(new[] { MetaDataType.TARGET, MetaDataType.DAMAGE, MetaDataType.TARGET, MetaDataType.DAMAGE })
                    && hits.All(m => m.Info.SequenceEqual(new[] { f.Player.Opponent.Hero.Id })), "registered Gorishi repeats resolved TARGET before each extra hit");
                Check(f.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "Gorishi repeated-hit checker clean");
                HistoryShapeReport.Add(f.Session.History);
            }
        }
        var wisp = new Fixture(CardClass.MAGE);
        var power = SabberStoneCore.Model.Entities.Entity.FromCard(wisp.Player, Cards.FromId("EDR_851p"));
        power[GameTag.TAG_SCRIPT_DATA_NUM_1] = 2;
        wisp.Ready("wisp-repeated"); int wispCursor = wisp.Game.PowerHistory.Full.Count;
        wisp.Game.PowerHistory.Add(EH.BlockStart(BlockType.TRIGGER, power.Id, "", -1, 0));
        power.Card.Power.PowerTask.Process(wisp.Game, wisp.Player, power, null, new TaskStack());
        wisp.Game.PowerHistory.Add(EH.BlockEnd()); wisp.Flush();
        var missiles = wisp.Game.PowerHistory.Full.Skip(wispCursor).OfType<EM>().Where(m => m.Type is MetaDataType.TARGET or MetaDataType.DAMAGE).ToArray();
        Check(wisp.Player.Opponent.Hero.Damage == 2 && wisp.Player.BoardZone.Count == 2
            && missiles.Select(m => m.Type).SequenceEqual(new[] { MetaDataType.TARGET, MetaDataType.DAMAGE, MetaDataType.TARGET, MetaDataType.DAMAGE }),
            "registered Wisp custom random loop preserves missiles and per-hit TARGET in TRIGGER");
        Check(wisp.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "Wisp repeated-hit checker clean");

        // Exercise the installed power through NativeMatch, not just the synthetic TRIGGER above.
        foreach (string id in new[] { "EX1_277", "EDR_851p" })
        {
            var f = new Fixture(CardClass.MAGE);
            IPlayable source;
            if (id == "EDR_851p") { ImbueRules.Imbue(f.Player, f.Player.Hero, 3); source = f.Player.Hero.HeroPower; }
            else source = Generic.DrawCard(f.Player, Cards.FromId(id));
            f.Ready("wisp-native-" + id); int cursor = f.Session.Packets.Count;
            f.Act(id == "EDR_851p" ? HeroPowerTask.Any(f.Player) : PlayCardTask.Spell(f.Player, source));
            var rows = f.Session.Packets.Skip(cursor).Where(p => p.Id == 19).SelectMany(p => {
                var packet = new PegasusGame.PowerHistory();
                using var stream = new MemoryStream(Convert.FromBase64String(p.Base64));
                packet.Deserialize(stream); return packet.List;
            }).ToArray();
            Check(rows.Count(r => r.HasPowerStart && r.PowerStart.Source == source.Id && (int)r.PowerStart.Type == 3) == 1,
                id + " keeps one POWER activation for the burst");
            var hits = rows.Where(r => r.HasMetaData).Select(r => r.MetaData).ToArray();
            Check(hits.Select(m => (int)m.Type).SequenceEqual(new[] { 0, 1, 0, 1, 0, 1 })
                && hits.All(m => m.Info.SequenceEqual(new[] { f.Player.Opponent.Hero.Id }))
                && hits.Where(m => (int)m.Type == 0).All(m => m.Data == source.Id)
                && hits.Where(m => (int)m.Type == 1).All(m => m.Data == 1),
                id + " native wire matches retail Arcane Missiles TARGET/DAMAGE order");
            var shots = rows.Where(r => r.HasSubSpellStart).Select(r => r.SubSpellStart).ToArray();
            Check(shots.Length == (id == "EDR_851p" ? 3 : 0) && rows.Count(r => r.HasSubSpellEnd) == shots.Length
                && shots.All(s => s.SourceEntityId == f.Player.Hero.Id && s.TargetEntityIds.SequenceEqual(new[] { f.Player.Opponent.Hero.Id })),
                id + " empty CardDef needs per-hit sub-spells; authored analogue does not");
            f.Session.History.Finish();
            Check(f.Session.History.Findings.Count == 0 && !Invariants.Compare(f.Game, f.Session.Client).Any(),
                id + " native HistoryShape and client state clean");
            HistoryShapeReport.Add(f.Session.History);
        }

        var rex = new Fixture(); var beast = Generic.DrawCard(rex.Player, Cards.FromId("CS2_171"));
        rex.Act(PlayCardTask.Minion(rex.Player, beast)); rex.Act(EndTurnTask.Any(rex.Player)); rex.Act(EndTurnTask.Any(rex.Player.Opponent));
        var dinosaur = rex.Summon(rex.Player, "DINO_138");
        var enemies = Enumerable.Range(0, 3).Select(_ => rex.Summon(rex.Player.Opponent, "CS2_200")).ToArray();
        foreach (var enemy in enemies) enemy.Health = 20;
        rex.Ready("diabolus-edges"); int rexCursor = rex.Game.PowerHistory.Full.Count;
        rex.Game.PowerHistory.Add(EH.BlockStart(BlockType.TRIGGER, dinosaur.Id, "", -1, 0));
        dinosaur.Card.Power.PowerTask.Process(rex.Game, rex.Player, dinosaur, null, new TaskStack());
        rex.Game.PowerHistory.Add(EH.BlockEnd()); rex.Flush();
        var edges = rex.Game.PowerHistory.Full.Skip(rexCursor).OfType<EM>().Where(m => m.Type is MetaDataType.TARGET or MetaDataType.DAMAGE).ToArray();
        Check(enemies.Select(e => e.Damage).SequenceEqual(new[] { 6, 0, 6 }) && edges.Select(m => m.Type)
            .SequenceEqual(new[] { MetaDataType.TARGET, MetaDataType.DAMAGE, MetaDataType.TARGET, MetaDataType.DAMAGE }),
            "registered Diabolus custom edge loop preserves results and per-hit TARGET in TRIGGER");
        Check(rex.Session.History.Findings.All(finding => finding.Rule == "SPLIT_EFFECT_BLOCK"), "Diabolus selected-hit checker clean");
        Console.WriteLine("PASS history-shape custom resolved hits: two-stack Gorishi, independent TARGET corruption, Wisp missiles, Diabolus edges");
    }
    private static void SpellVfxTargets()
    {
        foreach (string card in new[] { "END_000", "TIME_001", "CORE_CS2_029", "EX1_277" })
        foreach (int removeTargets in new[] { 0, 1, 2 })
        {
            var f = new Fixture(CardClass.ROGUE);
            var source = Generic.DrawCard(f.Player, Cards.FromId(card));
            f.Ready("spell-vfx-" + card);
            var task = f.Player.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                && (t.Target == null || t.Target.Id == f.Player.Opponent.Hero.Id));
            f.Game.Process(task);
            if (removeTargets > 0) f.Game.PowerHistory.Last.RemoveAll(e => e is EM { Type: MetaDataType.TARGET });
            bool explicitTargets = f.Game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>().Any(s => s.Targets.Count > 0);
            if (removeTargets == 2)
                foreach (var sub in f.Game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>()) sub.Targets.Clear();
            f.Flush();
            var findings = f.Session.History.Findings.Where(v => v.Severity == "error").ToArray();
            if (removeTargets > 0 && (!explicitTargets || removeTargets == 2))
                Check(findings.Any(v => v.Rule is "POWER_HIT_TARGET" or "TARGET_META" && v.Card == card),
                    card + " selected damage without TARGET or explicit sub-spell targets must fail strict history checks");
            else
            {
                Check(findings.Length == 0, card + " metadata or explicit sub-spell targets satisfy animation contract");
                if (removeTargets == 0) HistoryShapeReport.Add(f.Session.History);
            }
        }
        Console.WriteLine("PASS spell VFX target sensitivity: Eventuality, Chrono Daggers, Fireball, Arcane Missiles");
    }

    private static void NarrowBaselineExemptions()
    {
        var baseline = HistoryShapeReport.LoadBaseline();
        var controls = new List<HistoryShapeFinding>();
        var ordinary = Seed();
        Feed(ordinary, Start(3, 12, 11), Meta(HistoryMeta.Type.TARGET, 12, 11),
            Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 9), End());
        controls.Add(ordinary.Findings.Single(f => f.Rule == "DAMAGE_META_AMOUNT"));
        var missing = Seed();
        Feed(missing, Full(10, "BE_258", CardType.SPELL, Zone.GRAVEYARD), Start(3, 10, 0));
        missing.RecordMutation(11, GameTag.DAMAGE, 9, Hit(amount: 4, selected: true));
        Feed(missing, Meta(HistoryMeta.Type.DAMAGE, 4, 11), TC(11, GameTag.DAMAGE, 9), End());
        controls.Add(missing.Findings.Single(f => f.Rule == "POWER_HIT_TARGET"));
        var added = new[] {
            ("SPLIT_EFFECT_BLOCK", "BE_153"), ("DAMAGE_META_AMOUNT", "TIME_024"), ("SPLIT_EFFECT_BLOCK", "BE_153t"),
            ("HAND_BUFF_FX", "CATA_721"), ("HAND_BUFF_FX", "CATA_724"), ("DAMAGE_META_AMOUNT", "BE_283"),
            ("SPLIT_EFFECT_BLOCK", "CATA_470t1"), ("DAMAGE_META_AMOUNT", "TLC_601"), ("HAND_BUFF_FX", "CATA_723"),
            ("HAND_BUFF_FX", "EDR_255"), ("HAND_BUFF_FX", "JAIL_395"), ("DAMAGE_META_AMOUNT", "BE_013t1"),
            ("DAMAGE_META_AMOUNT", "BE_258"), ("DAMAGE_META_AMOUNT", "CATA_470t1"), ("DAMAGE_META_AMOUNT", "FIR_902"),
            ("DAMAGE_META_AMOUNT", "HERO_01"), ("DAMAGE_META_AMOUNT", "JAIL_881"), ("POWER_HIT_TARGET", "BE_013t1"),
            ("POWER_HIT_TARGET", "BE_258"), ("POWER_HIT_TARGET", "JAIL_881"), ("SPLIT_EFFECT_BLOCK", "TLC_100t1"),
            ("SPLIT_EFFECT_BLOCK", "TLC_100t3"), ("TRIGGER_HIT_TARGET", "FIR_902"), ("HAND_BUFF_FX", "CATA_722"), ("HAND_BUFF_FX", "CATA_EVENT_400"),
            ("HAND_BUFF_FX", "CORE_AV_107"), ("FATIGUE_BLOCK", "TIME_004")
        };
        foreach (var entry in baseline.Where(e => added.Contains((e.Rule, e.Card))))
            if (!controls.Any(f => f.Rule == entry.Rule && f.Card == entry.Card))
                controls.Add(new(entry.Rule, "error", entry.Card, entry.Card, entry.Card, "CS2_182",
                    "negative-control", 0, 1, entry.Shapes?.FirstOrDefault() ?? "corrupt ordinary effect"));
        var failures = new List<string>();
        foreach (var finding in controls)
        {
            var bucket = HistoryShapeReport.Bucket(new[] { finding }, finding.Rule, finding.Card);
            bool flagged = HistoryShapeReport.Classify(new[] { bucket }, baseline, false).Fresh.Length == 1;
            Console.WriteLine($"{(flagged ? "PASS" : "FAIL")} narrow exemption control {finding.Rule}/{finding.Card}");
            if (!flagged) failures.Add(finding.Rule + "/" + finding.Card);
        }
        Check(controls.Count == 27, "all 27 added exemptions have negative controls");
        Check(failures.Count == 0, "ordinary corruption must remain unaccepted: " + string.Join(",", failures));
    }

    private static void ExemptionConditionStreams()
    {
        var baseline = HistoryShapeReport.LoadBaseline();
        void Classify(HistoryShape shape, string rule, string card, bool accepted, string label)
        {
            shape.Finish();
            var bucket = HistoryShapeReport.Bucket(shape.Findings, rule, card);
            Check(bucket.Count > 0, label + " produces the finding under test");
            Check((HistoryShapeReport.Classify(new[] { bucket }, baseline, false).Fresh.Length == 0) == accepted, label);
        }
        foreach (bool saturation in new[] { false, true })
        {
            var s = Seed();
            Feed(s, Start(3, 12, 0), Meta(HistoryMeta.Type.DAMAGE, saturation ? int.MaxValue : 3, 11),
                TC(11, GameTag.DAMAGE, saturation ? int.MaxValue : 9), End());
            Classify(s, "DAMAGE_META_AMOUNT", "HERO_01", saturation, "hero counter saturation=" + saturation);
        }
        foreach (string sourceCard in new[] { "CATA_721", "CORE_BT_416", "EDR_226", "TIME_704", "CATA_999", "JAIL_327", "CATA_722", "CATA_EVENT_400", "CORE_AV_107", "EDR_231" })
        foreach (bool transform in new[] { false, true })
        {
            var s = Seed();
            var held = Full(11, transform ? "CS2_182" : "CATA_615t", zone: Zone.HAND);
            held.FullEntity.Tags.Add(new() { Name = (int)GameTag.ATK, Value = 6 });
            held.FullEntity.Tags.Add(new() { Name = (int)GameTag.HEALTH, Value = transform ? 7 : 5 });
            var enchant = Full(20, "CS2_009e", CardType.ENCHANTMENT);
            enchant.FullEntity.Tags.Add(new() { Name = (int)GameTag.ATTACHED, Value = 11 });
            Feed(s, Full(10, sourceCard), held, enchant, Start(3, 10, 0));
            if (transform)
            {
                var changed = Full(11, "CATA_615t", zone: Zone.HAND).FullEntity;
                changed.Tags.Add(new() { Name = (int)GameTag.ATK, Value = 6 });
                changed.Tags.Add(new() { Name = (int)GameTag.HEALTH, Value = 5 });
                Feed(s, new PowerHistoryData { ChangeEntity = changed });
            }
            Feed(s, TC(11, GameTag.ATK, 8), TC(11, GameTag.HEALTH, 7), End());
            Classify(s, "HAND_BUFF_FX", sourceCard, transform, sourceCard + " retained hand transform=" + transform);
        }
        foreach (bool automatic in new[] { false, true })
        {
            var s = Seed();
            var source = Full(10, "BE_153", CardType.SPELL, automatic ? Zone.HAND : Zone.GRAVEYARD);
            source.FullEntity.Tags.Add(new() { Name = (int)GameTag.CASTSWHENDRAWN, Value = 1 });
            Feed(s, source, Start(5, 10, 0));
            Feed(s, Start(3, 10, 11), Meta(HistoryMeta.Type.TARGET, 10, 11), Meta(HistoryMeta.Type.DAMAGE, 1, 11),
                TC(11, GameTag.DAMAGE, 6), End(), End());
            Classify(s, "SPLIT_EFFECT_BLOCK", "BE_153", automatic, "current draw-cast wrapper=" + automatic);
        }
        // Only the reviewed Earthen Drake partners (CORE_EX1_012, DINO_435) qualify, under active Sandfury, at MAIN_END.
        // A damaging partner (CORE_NEW1_020), a Drake-less Creation, or another step stays unaccepted.
        foreach (var (first, second, step, accepted) in new[] {
            ("CATA_999", "CORE_EX1_012", SabberStoneCore.Enums.Step.MAIN_END, true), ("DINO_435", "CATA_999", SabberStoneCore.Enums.Step.MAIN_END, true),
            ("CATA_999", "CORE_NEW1_020", SabberStoneCore.Enums.Step.MAIN_END, false), ("DINO_435", "CORE_EX1_012", SabberStoneCore.Enums.Step.MAIN_END, false),
            ("CATA_999", "DINO_435", SabberStoneCore.Enums.Step.MAIN_ACTION, false) })
        foreach (bool aura in new[] { false, true })
        {
            var s = Seed(); var creation = Full(10, "CATA_470t1");
            creation.FullEntity.Tags.Add(new() { Name = (int)GameTag.MODULAR_ENTITY_PART_1, Value = Cards.FromId(first).AssetId });
            creation.FullEntity.Tags.Add(new() { Name = (int)GameTag.MODULAR_ENTITY_PART_2, Value = Cards.FromId(second).AssetId });
            Feed(s, creation, TC(1, GameTag.STEP, (int)step));
            if (aura) Feed(s, Full(20, "CATA_480", CardType.SPELL, Zone.SECRET));
            var wrapper = Start(5, 10, 0); wrapper.PowerStart.EffectIndex = -1;
            Feed(s, wrapper, Start(5, 10, 0), Meta(HistoryMeta.Type.DAMAGE, 4, 12), End(),
                Start(5, 10, 0), Meta(HistoryMeta.Type.DAMAGE, 4, 12), End(), End());
            Classify(s, "SPLIT_EFFECT_BLOCK", "CATA_470t1", aura && accepted, $"modular Sandfury {first}+{second} at {step} co-presence=" + aura);
        }
        foreach (bool chest in new[] { false, true })
        {
            var s = Seed(); var creation = Full(10, "CATA_470t1");
            creation.FullEntity.Tags.Add(new() { Name = (int)GameTag.MODULAR_ENTITY_PART_1, Value = Cards.FromId("TIME_713").AssetId });
            creation.FullEntity.Tags.Add(new() { Name = (int)GameTag.MODULAR_ENTITY_PART_2, Value = Cards.FromId("EDR_815").AssetId });
            Feed(s, creation, Full(20, chest ? "TIME_713t" : "CS2_182", owner: 2), Start(3, 10, 0), Start(3, 10, 20),
                Meta(HistoryMeta.Type.TARGET, 10, 20), Meta(HistoryMeta.Type.DAMAGE, 3, 20), End(), End());
            Classify(s, "SPLIT_EFFECT_BLOCK", "CATA_470t1", chest, "modular response to enemy Chest=" + chest);
        }
        foreach (var (baron, retired, deathrattle, accepted) in new[] {
            (true, true, true, true), (false, true, true, false),
            (true, false, true, false), (true, true, false, false) })
        {
            var s = Seed(); var creation = Full(10, "CATA_470t1");
            creation.FullEntity.Tags.Add(new() { Name = (int)GameTag.MODULAR_ENTITY_PART_1, Value = Cards.FromId("TIME_004").AssetId });
            creation.FullEntity.Tags.Add(new() { Name = (int)GameTag.MODULAR_ENTITY_PART_2, Value = Cards.FromId(baron ? "RLK_708" : "CORE_EX1_012").AssetId });
            Feed(s, creation, Start(7, 10, 0), Start(3, 10, 0), Meta(HistoryMeta.Type.TARGET, 10, 11),
                Meta(HistoryMeta.Type.DAMAGE, 7, 11), End(), TC(10, GameTag.ZONE, (int)(retired ? Zone.GRAVEYARD : Zone.PLAY)),
                Start(deathrattle ? 5 : 3, 10, 0, deathrattle ? 217 : 0), Meta(HistoryMeta.Type.TARGET, 0, 10), End(), End());
            Classify(s, "SPLIT_EFFECT_BLOCK", "CATA_470t1", accepted,
                $"modular Battlecry/Deathrattle Baron={baron} retired={retired} deathrattle={deathrattle}");
        }
        foreach (string locationCard in new[] { "TLC_100t1", "TLC_100t2", "TLC_100t3" })
        foreach (bool exhausted in new[] { false, true })
        {
            var s = Seed(); var location = Full(10, locationCard, CardType.LOCATION);
            location.FullEntity.Tags.Add(new() { Name = (int)GameTag.HEALTH, Value = 2 });
            Feed(s, location, Start(7, 10, 0), Start(3, 10, 0), End());
            if (exhausted) Feed(s, TC(10, GameTag.DAMAGE, 2));
            Feed(s, Start(6, 1, 0), TC(10, GameTag.ZONE, (int)Zone.GRAVEYARD), TC(10, GameTag.DAMAGE, 0), End(),
                Start(5, 10, 0, 217), Meta(HistoryMeta.Type.DAMAGE, 1, 12), End(), End());
            Classify(s, "SPLIT_EFFECT_BLOCK", locationCard, exhausted, locationCard + " exhausted before Deathrattle=" + exhausted);
        }
        // Presence alone cannot excuse missing TARGETs or ordinary amount corruption.
        var corrupt = Seed();
        Feed(corrupt, Full(10, "BE_258", CardType.SPELL), Full(15, "EDR_258"), Start(3, 10, 0));
        corrupt.RecordMutation(11, GameTag.DAMAGE, 9, Hit(amount: 4, selected: true));
        Feed(corrupt, Meta(HistoryMeta.Type.DAMAGE, 3, 11), TC(11, GameTag.DAMAGE, 9), End());
        Classify(corrupt, "POWER_HIT_TARGET", "BE_258", false, "Toreth present with genuinely missing TARGET");
        Classify(corrupt, "DAMAGE_META_AMOUNT", "BE_258", false, "Toreth present with ordinary damage corruption");
        Console.WriteLine("PASS history-shape exemption condition streams: saturation, transform, draw marker, Sandfury, target corruption");
    }

    private static void SaturatedDamage()
    {
        foreach (string variant in new[] { "valid", "wrong-amount", "missing" })
        {
            var f = new Fixture(); var source = f.Summon(f.Player, "TLC_254");
            var victim = f.Summon(f.Player.Opponent, "BE_045");
            f.Ready("saturated-damage-" + variant);
            f.Game.PowerHistory.Add(EH.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0));
            victim.TakeDamage(source, int.MaxValue);
            victim.TakeDamage(source, 2);
            f.Game.PowerHistory.Add(EH.BlockEnd());
            var last = f.Game.PowerHistory.Last.OfType<EM>().Last(m => m.Type == MetaDataType.DAMAGE);
            Check(victim.Damage == int.MaxValue && last.Data == 2, "saturated counter retains the second hit metadata");
            if (variant == "wrong-amount") last.Data = 1;
            if (variant == "missing") f.Game.PowerHistory.Last.Remove(last);
            bool unmatched = false;
            try { f.Flush(); }
            catch (InvalidOperationException error) when (error.Message.StartsWith("history-shape unmatched hook hit")) { unmatched = true; }
            Check(variant == "missing" ? unmatched : !unmatched, "saturated hit correlation " + variant);
            Check(variant != "wrong-amount" || f.Session.History.Findings.Any(v => v.Rule == "DAMAGE_META_AMOUNT"),
                "saturated hit still checks the exact amount");
            Check(variant != "valid" || f.Session.History.Findings.Count == 0, "saturated hit has valid metadata and provenance");
        }
        Console.WriteLine("PASS history-shape saturated damage: same packet ordering, exact amount, missing metadata rejection");
    }
    private static void FullEntityRegistration()
    {
        foreach (int? embedded in new int?[] { null, 0, 999, 80 })
        {
            var shape = new HistoryShape();
            var full = Full(80, "", zone: Zone.DECK);
            full.FullEntity.Tags.RemoveAll(t => t.Name == (int)GameTag.ENTITY_ID);
            if (embedded.HasValue) full.FullEntity.Tags.Add(new Tag { Name = (int)GameTag.ENTITY_ID, Value = embedded.Value });
            Feed(shape, full); shape.Finish();
            Check(shape.Findings.Any(f => f.Rule == "FULL_ENTITY_ID") == (embedded != 80),
                "FULL identity admission rejects missing/zero/mismatched embedded ID");
        }
    }

    internal static void Run() { FullEntityRegistration(); ConditionalDeathrattle(); FutureFullMetadata(); DebtShapes(); NarrowBaselineExemptions(); SaturatedDamage(); B14Presentation(); SpellVfxTargets(); SplitKeywordBlocksAreIndependent(); SplitAcrossKeywordWrappers(); SplitBaselineShapes(); SplitDevouringPlagueRegression(); SplitDistinctActivations(); SplitEffectBlocks(); DisabledHistoryHooks(); DisabledRecorderDoesNotEnumerateTargets(); CustomResolvedHits(); TriggerHitsAndPoisonous(); NestedTriggerDamageDuringCombat(); HeroDamageProvenance(); ReviewerRegressions(); Synthetic(); HookBoundaries(); EmissionRanges(); Focused(); }
}
