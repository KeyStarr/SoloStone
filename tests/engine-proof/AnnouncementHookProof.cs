using System.Diagnostics;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// "Announcement hook: Dracorex then egg rain" (docs/agent-workflows/live-test-hooks.md), headless: the exact scenario the
// capture stages through DebugScenario.Apply, then the two player actions. Proves the sequence neither hangs nor
// throws and pins the outcome the video relies on, so a rules change that breaks the shot fails here, not live.
internal static class AnnouncementHookProof
{
    private const string HumanSide = "humanBoard=DINO_401,TIME_064,EDR_031\nhumanBoardStats=999,999\nhumanSeat=first\n";
    // Draw order: the turn-one junk 1-drop, then six spells Ohn'ahra plays (twice per turn under Deios).
    private const string SpellsA = "humanDeckTop=CORE_EX1_011,CATA_308,JAIL_307,CATA_308,JAIL_307,JAIL_445,JAIL_445\n";
    // Variant A: mixed Taunt deathrattle bodies. Variant B (the recipe): seven Eggs of Khelos, each death cracks one stage.
    internal const string ScenarioA = HumanSide + "npcBoard=CORE_EX1_110,CORE_LOOT_368,CORE_AV_337,TLC_237,DINO_130,DINO_410,DINO_410\n" + SpellsA;
    internal const string ScenarioB = HumanSide + "npcBoard=DINO_410,DINO_410,DINO_410,DINO_410,DINO_410,DINO_410,DINO_410\n" + SpellsA;
    // Variant C (the recipe): eggs again. Four board wipes crack each egg to the 20/20 Khelos (one stage per spell), then two
    // Decimations (1 + minions on board damage each, 11 here) kill the Khelos: 7 x (5 cracks + 1 hatch) = 42 deaths.
    internal const string ScenarioC = HumanSide + "npcBoard=DINO_410,DINO_410,DINO_410,DINO_410,DINO_410,DINO_410,DINO_410\n"
        + "humanDeckTop=CORE_EX1_011,CATA_308,JAIL_307,CATA_308,JAIL_307,CATA_581,CATA_581\n";

    // Variant D (reversal gag, no End Turn): seven Eggs at their final stage; every hatched Khelos is 9999/9999 Taunt.
    internal const string ScenarioD = HumanSide + "npcBoard=DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5\n"
        + "npcSummonStats=DINO_410t,9999,9999\n";

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);

    internal sealed class Counts
    {
        public int Deaths, Summons, Damage;
        public override string ToString() => $"deaths={Deaths} summons={Summons} damage={Damage}";
    }

    internal sealed class Outcome
    {
        public Game Game;
        public int NpcBodiesBefore, NpcMinionDeathsAfterAttack, NpcBodiesAfterAttack, NpcBodiesAfterEndTurn, NpcMinionDeathsTotal;
        public int HumanBoardAfterEndTurn, HumanDeaths, OhnahraCardsPlayed;
        public int NpcHeroHealth, HumanHeroHealth, HistoryEntries, KhelosSummoned, KhelosDeaths;
        public long Milliseconds;
        public Controller NpcGame() => Game.Player2;
        public Counts Attack, EndTurn;
        public List<(string Spell, Counts Counts)> Waves = new();
        public string[] NpcSurvivorsAfterEndTurn, NpcBoardAfterAttack, NpcGraveAfterAttack, NpcGraveAfterEndTurn;
    }

    private static string[] Grave(Controller p) => p.GraveyardZone.Where(c => c is Minion).GroupBy(c => c.Card.Id)
        .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + "x" + g.Count()).ToArray();

    // What the client must animate between two history positions: minion deaths (ZONE -> GRAVEYARD), minions created
    // (FULL_ENTITY of a minion) and damage tag changes. Spell waves are the blocks whose source is a spell.
    private static void Tally(Game g, int from, int to, Counts total, List<(string, Counts)> waves)
    {
        // Deaths resolve after the PLAY block that dealt the damage, so a wave owns everything up to the next spell's PLAY block.
        var entries = g.PowerHistory.Full;
        Counts wave = null; string waveSpell = null;
        for (int i = from; i < to; i++)
        {
            var entry = entries[i];
            if (entry is PowerHistoryBlockStart start)
            {
                if (waves != null && start.BlockType == BlockType.PLAY && g.IdEntityDic.TryGetValue(start.Source, out var source) && source.Card.Type == CardType.SPELL)
                {
                    if (wave != null) waves.Add((waveSpell, wave));
                    wave = new Counts(); waveSpell = source.Card.Id;
                }
            }
            var kinds = new Counts();
            if (entry is PowerHistoryTagChange tag)
            {
                bool minion = g.IdEntityDic.TryGetValue(tag.EntityId, out var e) && e.Card.Type == CardType.MINION;
                if (tag.Tag == GameTag.ZONE && tag.Value == (int)Zone.GRAVEYARD && minion) kinds.Deaths = 1;
                if (tag.Tag == GameTag.DAMAGE && tag.Value > 0) kinds.Damage = 1;
            }
            else if (entry is PowerHistoryFullEntity full && g.IdEntityDic.TryGetValue(full.Entity.Id, out var created)
                && created.Card.Type == CardType.MINION) kinds.Summons = 1;
            foreach (var target in new[] { total, wave }.Where(c => c != null))
            { target.Deaths += kinds.Deaths; target.Summons += kinds.Summons; target.Damage += kinds.Damage; }
        }
        if (wave != null) waves.Add((waveSpell, wave));
    }

    internal static Outcome Play(string scenarioText, string attackTarget, bool endTurn = true)
    {
        var timer = Stopwatch.StartNew();
        var scenario = DebugScenario.Parse(scenarioText);
        // The human is the first player with a Druid hero and a plain 30-card deck; the scenario stacks 7 cards on top.
        var g = B15GameSetupProof.Opening(Enumerable.Repeat("CS2_182", 30).ToArray());
        foreach (var p in new[] { g.Player1, g.Player2 })
            Check(g.Process(ChooseTask.Mulligan(p, p.Choice.Choices.ToList())), "keep opening");
        Check(scenario.Validate(g) == null, "scenario fits the match");
        string applied = scenario.Apply(g);   // same call, same point as NativeMatch.ApplyDebugScenario
        g.MainBegin();
        var human = g.Player1; var npc = g.Player2;
        var result = new Outcome { Game = g };

        Check(applied.Contains("humanBoard=DINO_401,TIME_064,EDR_031 stats=999,999"), "applied summary names the stats: " + applied);
        Check(g.CurrentPlayer == human && g.Turn == 1, "the human plays turn one");
        Check(human.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "DINO_401", "TIME_064", "EDR_031" }), "human board in order");
        foreach (Minion m in human.BoardZone)
            Check(m.AttackDamage == 999 && m.BaseHealth == 999 && m.Health == 999 && m.Damage == 0, m.Card.Id + " is 999/999 with no damage");
        Check(npc.BoardZone.Count == 7, "NPC board holds seven bodies");
        if (endTurn)
        {
            Check(human.HandZone.Any(c => c.Card.Id == "CORE_EX1_011"), "the junk 1-drop is the turn-one draw");
            Check(human.DeckZone.TopCard.Card.Id == "CATA_308", "Medivh's Triumph is next in the deck");
        }
        result.NpcBodiesBefore = npc.BoardZone.Count;
        int humanDeck = human.DeckZone.Count;

        // Action 1: the Great Dracorex attacks (Rush lets it attack at once); its trigger damages every other enemy minion.
        var dracorex = (Minion)human.BoardZone.First(m => m.Card.Id == "DINO_401");
        var target = (Minion)npc.BoardZone.First(m => m.Card.Id == attackTarget);
        var attack = human.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
            && t.Source?.Id == dracorex.Id && t.Target?.Id == target.Id);
        Check(attack != null, "Dracorex can attack " + attackTarget + " on turn one (Rush, no summoning sickness)");
        int npcGraveBefore = npc.GraveyardZone.Count(c => c is Minion);
        int historyStart = g.PowerHistory.Full.Count;
        Check(g.Process(attack), "attack accepted");
        int afterAttack = g.PowerHistory.Full.Count;
        result.NpcMinionDeathsAfterAttack = npc.GraveyardZone.Count(c => c is Minion) - npcGraveBefore;
        result.NpcBodiesAfterAttack = npc.BoardZone.Count;
        result.NpcBoardAfterAttack = npc.BoardZone.Select(m => m.Card.Id + "(" + ((Minion)m).AttackDamage + "/" + ((Minion)m).Health + ")").ToArray();
        result.NpcGraveAfterAttack = Grave(npc);
        Check(dracorex.Zone == human.BoardZone, "Dracorex survives the attack");
        Check(target.Zone != npc.BoardZone, "the attacked body died in combat");

        result.Attack = new Counts(); result.EndTurn = new Counts();
        Tally(g, historyStart, afterAttack, result.Attack, null);
        if (!endTurn) { Finish(result, timer); return result; }

        // Action 2: End Turn. Deios doubles Ohn'ahra's end-of-turn effect: six cards leave the deck.
        Check(g.Process(EndTurnTask.Any(human)), "end turn accepted (no hang)");
        int afterEnd = g.PowerHistory.Full.Count;
        result.NpcBodiesAfterEndTurn = npc.BoardZone.Count;
        result.NpcSurvivorsAfterEndTurn = npc.BoardZone.Select(m => m.Card.Id).ToArray();
        result.HumanBoardAfterEndTurn = human.BoardZone.Count;
        result.OhnahraCardsPlayed = humanDeck - human.DeckZone.Count;
        result.NpcGraveAfterEndTurn = Grave(npc);
        result.NpcMinionDeathsTotal = npc.GraveyardZone.Count(c => c is Minion) - npcGraveBefore;
        result.HumanDeaths = human.GraveyardZone.Count(c => c is Minion);
        result.NpcHeroHealth = npc.Hero.Health; result.HumanHeroHealth = human.Hero.Health;
        Tally(g, afterAttack, afterEnd, result.EndTurn, result.Waves);
        Finish(result, timer);
        return result;
    }

    private static void Finish(Outcome result, Stopwatch timer)
    {
        var g = result.Game; var npc = g.Player2;
        result.NpcHeroHealth = npc.Hero.Health; result.HumanHeroHealth = g.Player1.Hero.Health;
        result.HistoryEntries = g.PowerHistory.Full.Count;
        result.HumanDeaths = g.Player1.GraveyardZone.Count(c => c is Minion);
        result.HumanBoardAfterEndTurn = g.Player1.BoardZone.Count;
        result.KhelosSummoned = g.IdEntityDic.Values.Count(e => e.Card.Id == "DINO_410t" && e.Controller == npc);
        result.KhelosDeaths = npc.GraveyardZone.Count(c => c.Card.Id == "DINO_410t");
        result.Milliseconds = timer.ElapsedMilliseconds;
    }

    private static string Report(string name, Outcome r) =>
        $"ANNOUNCEMENT-HOOK {name} PASS npcBodiesBefore={r.NpcBodiesBefore} deathsAfterAttack={r.NpcMinionDeathsAfterAttack} "
        + $"bodiesAfterAttack={r.NpcBodiesAfterAttack} bodiesAfterEndTurn={r.NpcBodiesAfterEndTurn} deathsTotal={r.NpcMinionDeathsTotal} "
        + $"khelos20Summoned={r.KhelosSummoned} khelos20Died={r.KhelosDeaths} ohnahraCards={r.OhnahraCardsPlayed} "
        + $"humanBoard={r.HumanBoardAfterEndTurn} humanDeaths={r.HumanDeaths} npcHero={r.NpcHeroHealth} humanHero={r.HumanHeroHealth} "
        + $"history={r.HistoryEntries} ms={r.Milliseconds}\n    attack[{r.Attack}] endTurn[{r.EndTurn}] waves: "
        + string.Join(" | ", r.Waves.Select(w => w.Spell + " " + w.Counts))
        + "\n    graveAfterAttack=" + string.Join(",", r.NpcGraveAfterAttack) + " graveAfterEndTurn=" + string.Join(",", r.NpcGraveAfterEndTurn)
        + " boardAfterAttack=" + string.Join(",", r.NpcBoardAfterAttack) + " survivors=" + string.Join(",", r.NpcSurvivorsAfterEndTurn);

    private static void Common(Outcome r, bool ended = true)
    {
        var g = r.Game; var human = g.Player1; var npc = g.Player2;
        Check(g.State != State.COMPLETE, "the match goes on");
        if (ended)
        {
            Check(g.CurrentPlayer == npc, "the NPC's turn follows");
            Check(r.OhnahraCardsPlayed == 6, "Deios doubles Ohn'ahra: six cards played, got " + r.OhnahraCardsPlayed);
            Check(r.HumanDeaths == 0 && r.HumanBoardAfterEndTurn == 3, "all three 999/999 bodies survive every wave");
        }
        foreach (Minion m in human.BoardZone)
            Check(m.Health > 0 && m.AttackDamage == 999 && m.BaseHealth == 999, m.Card.Id + " alive and still 999/999 (max health)");
        Check(human.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "DINO_401", "TIME_064", "EDR_031" }), "the same three human bodies, same order");
        Check(r.Milliseconds < 20000, "the sequence finishes without stalling: " + r.Milliseconds + " ms");
    }

    [GroupProof("announcement-hook")]
    internal static void Run()
    {
        // Variant A: Dracorex attacks Cairne (a Taunt); the other six bodies die to its 999.
        var a = Play(ScenarioA, "CORE_EX1_110");
        Common(a);
        Check(a.NpcMinionDeathsAfterAttack == 8 && a.NpcBodiesAfterEndTurn == 0 && a.NpcMinionDeathsTotal == 15 && a.NpcHeroHealth == 24,
            "variant A pinned outcome: " + Report("A", a));
        Console.WriteLine(Report("A", a));

        // Variant B: seven Eggs of Khelos, Dracorex attacks the first.
        var b = Play(ScenarioB, "DINO_410");
        Common(b);
        Console.WriteLine(Report("B", b));
        PinnedB(b);

        var c = Play(ScenarioC, "DINO_410");
        Common(c);
        Console.WriteLine(Report("C", c));
        PinnedC(c);

        // Variant D: the reversal gag. One attack, no End Turn.
        var d = Play(ScenarioD, "DINO_410t5", endTurn: false);
        Common(d, ended: false);
        PinnedD(d);
        Console.WriteLine($"ANNOUNCEMENT-HOOK D PASS attack[{d.Attack}] npcBoard={string.Join(",", d.NpcBoardAfterAttack)} "
            + $"khelosSummoned={d.KhelosSummoned} khelosDied={d.KhelosDeaths} humanBoard={d.Game.Player1.BoardZone.Count} "
            + $"humanDeaths={d.Game.Player1.GraveyardZone.Count(m => m is Minion)} history={d.HistoryEntries} ms={d.Milliseconds}");
    }

    private static void PinnedD(Outcome r)
    {
        var g = r.Game; var human = g.Player1; var npc = g.Player2;
        Check(r.NpcMinionDeathsAfterAttack == 7 && r.NpcBodiesAfterAttack == 7 && r.KhelosSummoned == 7 && r.KhelosDeaths == 0,
            "variant D: seven eggs die, seven Khelos hatch and live: deaths " + r.NpcMinionDeathsAfterAttack + ", board " + r.NpcBodiesAfterAttack
            + ", hatched " + r.KhelosSummoned);
        foreach (Minion k in npc.BoardZone)
            Check(k.Card.Id == "DINO_410t" && k.AttackDamage == 9999 && k.BaseHealth == 9999 && k.HasTaunt && (k.Health == 9999 || k.Health == 9000),
                "every NPC body is a 9999/9999 Taunt Khelos: " + k.Card.Id + " " + k.AttackDamage + "/" + k.Health + "/" + k.BaseHealth);
        // The attacked egg dies first, so its Khelos hatches before Dracorex's 999 lands and takes it (9000 left); the other six hatch after.
        Check(npc.BoardZone.Count(m => ((Minion)m).Health == 9000) == 1 && npc.BoardZone.Count(m => ((Minion)m).Health == 9999) == 6,
            "one Khelos took the shockwave, six hatched whole: " + string.Join(",", r.NpcBoardAfterAttack));
        Check(human.BoardZone.Count == 3 && r.HumanDeaths == 0, "all three of ours stand, none died");
        Check(r.Attack.Deaths == 7 && r.Attack.Summons == 7, "client events on the attack: " + r.Attack);
        Check(r.HistoryEntries < 1500, "bounded history for the client: " + r.HistoryEntries);
        // The rule is NPC-side only and card definitions are untouched: a Khelos the HUMAN really summons (SummonBlock fires
        // AfterSummon, so the handler runs and must skip it) stays 20/20.
        var ours = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(human, Cards.FromId("DINO_410t"));
        SabberStoneCore.Actions.Generic.SummonBlock(g, ours, -1, null);
        g.DeathProcessingAndAuraUpdate();
        Check(ours.Zone == human.BoardZone && ours.AttackDamage == 20 && ours.BaseHealth == 20, "a human-side summoned Khelos stays 20/20: "
            + ours.AttackDamage + "/" + ours.BaseHealth);
        Check(Cards.FromId("DINO_410t").ATK == 20 && Cards.FromId("DINO_410t").Health == 20, "the shared card definition is untouched");
        // A later NPC-side summon of the same card is re-statted (the rule stays armed for the whole match).
        var theirs = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(npc, Cards.FromId("DINO_410t"));
        npc.SetasideZone.Add(npc.BoardZone.Remove(npc.BoardZone.Last()));
        SabberStoneCore.Actions.Generic.SummonBlock(g, theirs, -1, null);
        Check(theirs.AttackDamage == 9999 && theirs.BaseHealth == 9999, "a later NPC-side summon is 9999/9999 too");

        // The rule is armed after the staged boards: the same card ON npcBoard keeps its printed stats.
        var staged = B15GameSetupProof.Opening(Enumerable.Repeat("CS2_182", 30).ToArray());
        foreach (var p in new[] { staged.Player1, staged.Player2 })
            Check(staged.Process(ChooseTask.Mulligan(p, p.Choice.Choices.ToList())), "keep opening");
        DebugScenario.Parse("npcBoard=DINO_410,DINO_410\nnpcSummonStats=DINO_410,5,5").Apply(staged);
        Check(staged.Player2.BoardZone.Count == 2 && staged.Player2.BoardZone.All(m => ((Minion)m).AttackDamage == 0 && ((Minion)m).BaseHealth == 3),
            "the staged npcBoard minions keep 0/3: the summon rule only affects later summons");
    }

    private static void PinnedB(Outcome r)
    {
        // Four wipes crack each egg to a 20/20 Khelos; the two Bone Flurries cannot kill them.
        Check(r.NpcMinionDeathsTotal == 35 && r.KhelosSummoned == 7 && r.KhelosDeaths == 0 && r.NpcBodiesAfterEndTurn == 7,
            "variant B: 35 deaths, seven Khelos stand: " + Report("B", r));
    }

    private static void PinnedC(Outcome r)
    {
        // One stage per wipe (Medivh's Triumph, Crowd Control, x2), then two Decimations (11 each) kill the seven Khelos.
        Check(r.NpcMinionDeathsAfterAttack == 8 && r.NpcMinionDeathsTotal == 42 && r.KhelosSummoned == 7 && r.KhelosDeaths == 7
            && r.NpcBodiesAfterEndTurn == 0 && r.NpcHeroHealth == 30, "variant C: 42 deaths, empty board: " + Report("C", r));
        Check(string.Join(",", r.Waves.Select(w => w.Spell)) == "CATA_308,JAIL_307,CATA_308,JAIL_307,CATA_581,CATA_581",
            "six spell waves in deck order: " + string.Join(",", r.Waves.Select(w => w.Spell)));
        Check(string.Join(",", r.Waves.Select(w => w.Counts.Deaths + "/" + w.Counts.Summons)) == "7/7,7/7,7/7,6/6,0/0,7/0",
            "per-wave deaths/summons: " + string.Join(",", r.Waves.Select(w => w.Counts.Deaths + "/" + w.Counts.Summons)));
        Check(r.Waves[0].Counts.Deaths == 7 && r.Waves[5].Counts.Deaths == 7 && r.Waves[4].Counts.Deaths == 0,
            "every egg cracks once per early wave; the Khelos die to the second Decimation");
        Check(r.NpcGame().BoardZone.Count == 0 && r.NpcSurvivorsAfterEndTurn.Length == 0, "the NPC board is empty after End Turn");
        Check(r.Attack.Deaths == 8 && r.Attack.Summons == 8 && r.EndTurn.Deaths == 34 && r.EndTurn.Summons == 27,
            "client events: attack " + r.Attack + ", end turn " + r.EndTurn);
        Check(r.HumanHeroHealth == 30, "the human hero is untouched");
        Check(r.HistoryEntries < 2000, "bounded history for the client: " + r.HistoryEntries);
    }
}
