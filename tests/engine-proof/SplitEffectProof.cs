using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SplitEffectProof
{
    internal static void Run(string only = null)
    {
        if (only == null || only == "hand-progress") HandProgressActivation();
        if (only == null || only == "weapon-battlecry") WeaponBattlecryActivation();
        if (only == null) { Scheduling(); FlushDuringActivation(); RevealStaysInsideActivation(); }
        foreach (var id in new[] { "EX1_277", "TIME_027", "CORE_BAR_311", "TLC_249", "FIR_953" })
        {
            if (only != null && id != only) continue;
            var g = HistoryFactsProof.Fixture();
            var p = g.Player1;
            if (id == "CORE_BAR_311") { var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 30; p.Hero.Damage = 15; }
            var source = Add(p, id);
            int cursor = 0;
            PlayEntity(g, p, source);
            var type = BlockType.POWER;
            int hits = id == "EX1_277" ? 3 : id == "TIME_027" ? 6 : 4;
            if (id == "TLC_249")
            {
                cursor = g.PowerHistory.Last.Count;
                Kill(g, (Minion)source);
                type = BlockType.TRIGGER; hits = 2;
            }
            if (id == "FIR_953")
            {
                var victim = Summon(p.Opponent, "CS2_171");
                cursor = 0;
                Attack(g, (Minion)source, victim);
                type = BlockType.TRIGGER; hits = 5;
            }
            SplitMissileHistory.Flat(g, cursor, source, hits, type);
            Console.WriteLine($"PASS split-effect history {id} {type} hits={hits} Data=source");
        }
    }
    private static void HandProgressActivation()
    {
        foreach (var id in new[] { "JAIL_445", "CORE_CS2_029" })
        foreach (var progressId in new[] { "JAIL_801", "JAIL_803" })
        foreach (int remaining in new[] { 3, 1 })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var held = Add(p, progressId);
            string upgradedId = held.Card.HandProgress.TargetId;
            held[GameTag.TAG_SCRIPT_DATA_NUM_1] = remaining;
            if (id == "JAIL_445") Kill(g, Summon(p, "CS2_182"));
            var source = Add(p, id);
            int cursor = 0;
            PlayEntity(g, p, source, id == "CORE_CS2_029" ? p.Opponent.Hero : null);
            var label = $"{id} holding {progressId} remaining={remaining}";
            Core2026Proof.Check(p.Opponent.Hero.Damage == 6 && source.Zone == p.GraveyardZone,
                label + " resolves all six damage and finishes graveyard move");
            var progressed = (IPlayable)g.IdEntityDic[held.Id];
            Core2026Proof.Check(remaining == 1 ? progressed.Card.Id == upgradedId
                : progressed.Card.Id == progressId && progressed[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                label + " credits hand progress exactly once");
            foreach (var entries in new[] { g.PowerHistory.Last.Skip(cursor), g.PowerHistory.Full })
            {
                var blocks = new Stack<PowerHistoryBlockStart>();
                int powers = 0;
                foreach (var entry in entries)
                {
                    if (entry is PowerHistoryBlockStart block)
                    {
                        if (block.Source == source.Id && block.BlockType == BlockType.POWER)
                        {
                            powers++;
                            Core2026Proof.Check(blocks.Count > 0 && blocks.Peek().BlockType == BlockType.PLAY
                                && blocks.Peek().Source == source.Id, label + " body POWER is directly under PLAY");
                        }
                        blocks.Push(block);
                    }
                    else if (entry is PowerHistoryBlockEnd) blocks.Pop();
                }
                Core2026Proof.Check(powers == 1 && blocks.Count == 0, label + $" has exactly one balanced POWER, got {powers}");
            }
            SplitMissileHistory.Flat(g, cursor, source, id == "JAIL_445" ? 6 : 1);
            Console.WriteLine($"PASS hand-progress activation {label}: one POWER under PLAY; TARGET/DAMAGE Full/Last; progress credited");
        }
    }
    private static void WeaponBattlecryActivation()
    {
        // PlayWeapon once wrapped the Battlecry in a targetless same-source POWER around TaskProcessor's own.
        foreach (var id in new[] { "EX1_133", "CS2_106" })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var source = Add(p, id);
            bool targeted = id == "EX1_133";
            PlayEntity(g, p, source, targeted ? p.Opponent.Hero : null);
            Core2026Proof.Check(p.Hero.Weapon?.Id == source.Id && p.Opponent.Hero.Damage == (targeted ? 1 : 0),
                id + " equips and resolves its Battlecry once");
            foreach (var entries in new[] { g.PowerHistory.Last, g.PowerHistory.Full })
            {
                var blocks = new Stack<PowerHistoryBlockStart>();
                int powers = 0, targetedPowers = 0;
                foreach (var entry in entries)
                {
                    if (entry is PowerHistoryBlockStart block)
                    {
                        if (block.Source == source.Id && block.BlockType == BlockType.POWER)
                        {
                            powers++;
                            Core2026Proof.Check(blocks.Count > 0 && blocks.Peek().BlockType == BlockType.PLAY
                                && blocks.Peek().Source == source.Id, id + " Battlecry POWER is directly under PLAY");
                        }
                        blocks.Push(block);
                    }
                    else if (entry is PowerHistoryBlockEnd) blocks.Pop();
                    else if (entry is PowerHistoryMetaData meta && meta.Type == MetaDataType.TARGET && blocks.Count > 0
                        && blocks.Peek().Source == source.Id && blocks.Peek().BlockType == BlockType.POWER
                        && meta.Info.Contains(p.Opponent.Hero.Id)) targetedPowers++;
                }
                Core2026Proof.Check(blocks.Count == 0 && powers == (targeted ? 1 : 0) && targetedPowers == (targeted ? 1 : 0),
                    id + $" has {(targeted ? "exactly one" : "no")} same-source POWER carrying the TARGET, got powers={powers} targeted={targetedPowers}");
            }
            if (targeted) SplitMissileHistory.Flat(g, 0, source, 1);
            Console.WriteLine($"PASS weapon-battlecry activation {id}: {(targeted ? "one targeted POWER under PLAY; TARGET/DAMAGE Full/Last" : "no empty POWER")}");
        }
    }
    private static void Scheduling()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool cleanup in new[] { false, true })
        {
            (Game Game, string Trace) Resolve(bool flat)
            {
                var g = HistoryFactsProof.Fixture(history);
                var p = g.Player1;
                var source = Summon(p, "CORE_EX1_082");
                var golem = Summon(p.Opponent, "EX1_556"); golem.Damage = 2;
                p.Opponent.Hero.Armor = 100;
                var trace = new List<string>();
                g.TriggerManager.DamageTrigger += victim => g.TaskQueue.Enqueue(
                    new CustomTask((game, controller, actor, target, stack) => trace.Add("damage:" + victim.Id)), p, p.Hero, null);
                ISimpleTask hit = ComplexTask.Create(
                    new CustomTask((game, controller, actor, target, stack) => trace.Add("hit:" + p.Opponent.Hero.Armor + ":" + p.Opponent.BoardZone.Count)),
                    ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1),
                    new CustomTask((game, controller, actor, target, stack) => game.TaskQueue.Enqueue(
                        new CustomTask((g2, c2, s2, t2, stack2) => trace.Add("deferred")), controller, controller.Hero, null)));
                var tail = new CustomTask((game, controller, actor, target, stack) => trace.Add("tail"));
                var oldHit = ComplexTask.Create(hit,
                    new CustomTask((game, controller, actor, target, stack) => { if (cleanup) game.DeathProcessingAndAuraUpdate(); }));
                var recipe = flat ? ComplexTask.Create(tail, SplitHits.Inline(6, hit, cleanup))
                    : ComplexTask.Create(new EnqueueTask(6, oldHit), tail);
                g.TaskQueue.Enqueue(recipe, p, source, null);
                int cursor = g.PowerHistory?.Last.Count ?? 0;
                g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(trace[0] == "tail" && trace.Count(t => t.StartsWith("hit:")) == 6
                    && trace.TakeLast(6).All(t => t == "deferred"), $"recipe tail precedes burst; hit-recipe reactions follow burst: flat={flat} cleanup={cleanup}: {string.Join("|", trace)}");
                if (flat && history) SplitMissileHistory.Flat(g, cursor, source, 6);
                return (g, string.Join("|", trace));
            }
            var old = Resolve(false); var current = Resolve(true);
            Core2026Proof.Check(old.Trace == current.Trace && Canonical.State(old.Game) == Canonical.State(current.Game)
                && old.Game.Random.CaptureState().Equals(current.Game.Random.CaptureState()),
                $"split hits preserve queued reactions, deaths, recipe tails and exact RNG: history={history} cleanup={cleanup}");
        }
        var doubled = HistoryFactsProof.Fixture(); var owner = doubled.Player1;
        var bomber = Summon(owner, "CORE_EX1_082");
        var burst = SplitHits.Inline(3, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false);
        int start = doubled.PowerHistory.Last.Count;
        doubled.TaskQueue.Enqueue(burst, owner, bomber, null);
        doubled.TaskQueue.Enqueue(burst, owner, bomber, null);
        doubled.ProcessTasks();
        SplitMissileHistory.Flat(doubled, start, bomber, 3);
        Core2026Proof.Check(doubled.PowerHistory.Last.Skip(start).OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
            .Count(b => b.Source == bomber.Id && b.BlockType == BlockType.POWER) == 2,
            "doubled activations retain two distinct flat blocks");
        Console.WriteLine("PASS split-effect scheduling: recipe tails, distinct doubled activations, queued reactions, per-recipe deaths, exact state/RNG; history on/off");
    }

    private static void RevealStaysInsideActivation()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029")); spell.Cost = 5; p.DeckZone.Add(spell);
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 30;
        var source = Play(g, p, "JAIL_379");
        SplitMissileHistory.Flat(g, 0, source, 5);
        foreach (var entries in new[] { g.PowerHistory.Full, g.PowerHistory.Last })
        {
            var blocks = new Stack<SabberStoneCore.Kettle.PowerHistoryBlockStart>();
            bool revealed = false;
            foreach (var entry in entries)
            {
                if (entry is SabberStoneCore.Kettle.PowerHistoryBlockStart block)
                {
                    if (block.BlockType == BlockType.REVEAL_CARD && block.Source == source.Id)
                        revealed |= blocks.Count > 0 && blocks.Peek().Source == source.Id && blocks.Peek().BlockType == BlockType.POWER;
                    blocks.Push(block);
                }
                else if (entry is SabberStoneCore.Kettle.PowerHistoryBlockEnd) blocks.Pop();
            }
            Core2026Proof.Check(revealed, "Spire Security reveal stays inside the same activation as its five hits");
        }
        Console.WriteLine("PASS split-effect reveal: Spire Security reveal and five hits share activation in Full/Last");
    }

    private static void FlushDuringActivation()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var source = Add(p, "EX1_277");
        var sent = new List<SabberStoneCore.Kettle.IPowerHistoryEntry>();
        int hits = 0, reactions = 0;
        g.TriggerManager.DamageTrigger += victim => {
            hits++;
            if (hits == 1)
            {
                sent.AddRange(g.PowerHistory.Last);
                g.PowerHistory.Last.Clear(); // Simulate a transport flush while the activation is open.
            }
            g.TaskQueue.Enqueue(new CustomTask((game, controller, actor, target, stack) => {
                reactions++;
            }), p, p.Hero, null);
        };
        int fullCursor = g.PowerHistory.Full.Count;
        PlayEntity(g, p, source);
        sent.AddRange(g.PowerHistory.Last);
        Core2026Proof.Check(hits == 3 && reactions == 3 && sent.SequenceEqual(g.PowerHistory.Full.Skip(fullCursor)),
            "mid-activation flush loses or rewrites no entries; sent stream equals Full");
        int depth = 0;
        foreach (var entry in sent)
        {
            if (entry is SabberStoneCore.Kettle.PowerHistoryBlockStart) depth++;
            if (entry is SabberStoneCore.Kettle.PowerHistoryBlockEnd) depth--;
            Core2026Proof.Check(depth >= 0, "flushed stream has no unmatched block end");
        }
        Core2026Proof.Check(depth == 0, "flushed stream closes every activation/reaction block");
        Console.WriteLine("PASS split-effect flush: open activation flushed after first hit; append-only balanced stream equals Full; immediate damage listeners retain per-hit timing");
    }
}
