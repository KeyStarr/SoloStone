using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class PoisonousDamageProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, "Poisonous: " + message);
    private static void Grant(Game g, Minion source)
    {
        Verify(new AddEnchantmentTask("CORE_EDR_002e", EntityType.TARGET).Process(g, source.Controller, source, source) == TaskState.COMPLETE
            && source.Poisonous && source.AppliedEnchantments.Any(e => e.Card.Id == "CORE_EDR_002e"), "real Deathly Poison enchantment grants keyword");
    }
    private static void History(Game g, int cursor, params (IPlayable Source, ICharacter Victim)[] expected)
    {
        var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
        var blocks = entries.Select((entry, index) => (entry, index))
            .Where(x => x.entry is PowerHistoryBlockStart { TriggerKeyword: 363 }).ToArray();
        Verify(blocks.Length == expected.Length, $"expected {expected.Length} activations, got {blocks.Length}");
        var remaining = expected.Select(x => (x.Source.Id, x.Victim.Id)).ToList();
        foreach (var (entry, index) in blocks)
        {
            var block = (PowerHistoryBlockStart)entry;
            Verify(block.BlockType == BlockType.TRIGGER && block.EffectIndex == -1 && block.Target == 0 && block.EffectCardId == "", "keyword block header");
            Verify(entries.ElementAtOrDefault(index + 1) is PowerHistoryMetaData { Type: MetaDataType.TARGET, Data: 0, Info.Count: 1 }, "immediate TARGET metadata");
            int victim = ((PowerHistoryMetaData)entries[index + 1]).Info[0];
            Verify(remaining.Remove((block.Source, victim)), "exact source and victim, no duplicates");
            Verify(entries.ElementAtOrDefault(index + 2) is PowerHistoryMetaData meta && (int)meta.Type == 22
                && meta.Data == 0 && meta.Info.SequenceEqual(new[] { victim }), "immediate POISONOUS=22 metadata");
            Verify(entries.ElementAtOrDefault(index + 3) is PowerHistoryTagChange tag && tag.EntityId == victim
                && tag.Tag == GameTag.TO_BE_DESTROYED && tag.Value == 1, "destroy tag inside keyword block");
            Verify(entries.ElementAtOrDefault(index + 4) is PowerHistoryBlockEnd, "keyword block closes after destroy tag");
        }
    }
    internal static void Run()
    {
        KnifeJuggler(); AreaDamage(); DamageWaves(); NestedWaves(); SingleTarget(); Combat(); Weapon(); HeroSources(); Deathrattles(); KeywordTiming(); Prevention(); GenericSources(); KillAndLifestealTiming();
        Console.WriteLine("PASS Poisonous waves: damage before poison before collected Lifesteal/deaths; nested waves isolated; serial/single-target history unchanged");
        Console.WriteLine("PASS Poisonous all damage: granted Juggler/Pyromancer; combat/Patron/Acolyte; weapon attack/counter; Hagatha/fatigue/nested hero provenance; printed/granted Deathrattles; timing; prevention; spell/power/location; history on/off");
    }
    private static void KnifeJuggler()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var juggler = Summon(p, "NEW1_019"); Grant(g, juggler);
        var victim = Summon(p.Opponent, "CS2_200"); g.SetRandomSeed(3);
        int cursor = g.PowerHistory.Full.Count;
        Play(g, p, "CS2_231");
        Verify(victim.Zone.Type == Zone.GRAVEYARD && victim.Damage == 1, "granted Poisonous Knife Juggler destroys knife victim");
        History(g, cursor, (juggler, victim));
        cursor = g.PowerHistory.Full.Count;
        Play(g, p, "CS2_231");
        Verify(p.Opponent.Hero.Damage == 1 && !p.Opponent.Hero.ToBeDestroyed, "knife to hero only damages");
        History(g, cursor);
    }
    private static void AreaDamage()
    {
        foreach (bool lifesteal in new[] { false, true })
        {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var pyro = Summon(p, "CORE_NEW1_020"); Grant(g, pyro);
        pyro.HasLifeSteal = lifesteal; p.Hero.Damage = 10;
        var friendly = Summon(p, "CS2_182");
        var shield = Summon(p.Opponent, "CS2_200"); shield.HasDivineShield = true;
        var lethal = Summon(p.Opponent, "CS2_231");
        int cursor = g.PowerHistory.Full.Count;
        Play(g, p, "GAME_005");
        Verify(shield.Zone.Type == Zone.PLAY && !shield.HasDivineShield && shield.Damage == 0, "AoE shield pops without poison");
        Verify(friendly.Zone.Type == Zone.GRAVEYARD && lethal.Zone.Type == Zone.GRAVEYARD && lethal.Health <= 0,
            "AoE poisons friendly and lethally damaged victims");
        Verify(p.Hero.Damage == (lifesteal ? 7 : 10) && p.Opponent.Hero.Damage == 0, "Pyromancer never damages heroes");
        History(g, cursor, (pyro, pyro), (pyro, friendly), (pyro, lethal));
        // A source already marked for death still applies the keyword to later hits in the same AoE.
        Verify(pyro.Zone.Type == Zone.GRAVEYARD, "Poisonous self-hit does not stop remaining AoE hits");
        var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
        Wave(entries, new[] { pyro.Id, friendly.Id, lethal.Id }, 4);
        int deaths = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { BlockType: BlockType.DEATHS });
        Verify(entries.OfType<PowerHistoryBlockStart>().Count(b => b.BlockType == BlockType.DEATHS) == 1,
            "one DEATHS pass for whole AoE");
        int depth = 1, end = deaths + 1;
        for (; end < entries.Length && depth > 0; end++)
        {
            if (entries[end] is PowerHistoryBlockStart) depth++;
            if (entries[end] is PowerHistoryBlockEnd) depth--;
        }
        Verify(entries.Skip(deaths + 1).Take(end - deaths - 2).OfType<PowerHistoryTagChange>()
            .Where(t => t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD).Select(t => t.EntityId)
            .Order().SequenceEqual(new[] { pyro.Id, friendly.Id, lethal.Id }.Order()), "DEATHS contains every poisoned removal");
        if (lifesteal)
        {
            int heal = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: (int)GameTag.LIFESTEAL });
            int lastPoison = Array.FindLastIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: 363 });
            Verify(heal > lastPoison + 4 && heal < deaths && p.Hero.Damage == 7, "collected Lifesteal follows all poison before deaths");
        }
        }
    }
    private static void Wave(IPowerHistoryEntry[] entries, int[] victims, int hits)
    {
        int poison = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: 363 });
        Verify(poison >= 0 && entries.OfType<PowerHistoryMetaData>().Count(m => m.Type == MetaDataType.DAMAGE) == hits
            && Array.FindLastIndex(entries, e => e is PowerHistoryMetaData { Type: MetaDataType.DAMAGE }) < poison
            && Array.FindLastIndex(entries, e => e is PowerHistoryTagChange { Tag: GameTag.DAMAGE, Value: > 0 } tag && victims.Contains(tag.EntityId)) < poison,
            "all wave damage metadata/tags precede poison");
        Verify(entries.OfType<PowerHistoryBlockStart>().Count(b => b.TriggerKeyword == 363) == victims.Length,
            "one poison block per eligible victim, none for shield");
        Verify(victims.Select((id, i) => entries.ElementAtOrDefault(poison + i * 5 + 1) is PowerHistoryMetaData m
            && m.Type == MetaDataType.TARGET && m.Info.SequenceEqual(new[] { id })).All(x => x), "poison blocks consecutive in victim order");
    }
    private static void DamageWaves()
    {
        foreach (bool number in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var source = Summon(p, "BE_193"); source.HasLifeSteal = true; p.Hero.Damage = 10;
            var first = Summon(p.Opponent, "CS2_200"); var last = Summon(p.Opponent, "CS2_200");
            int cursor = g.PowerHistory.Full.Count;
            if (number) new DamageNumberTask(EntityType.OP_MINIONS).Process(g, p, source, null, new TaskStack { Number = 1 });
            else new DamageTask(1, EntityType.OP_MINIONS).Process(g, p, source, null);
            var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
            Wave(entries, new[] { first.Id, last.Id }, 2);
            History(g, cursor, (source, first), (source, last));
            Verify(first.ToBeDestroyed && last.ToBeDestroyed && p.Hero.Damage == 8, "both damage tasks retain poison/Lifesteal state");
            Verify(Array.FindIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: (int)GameTag.LIFESTEAL })
                > Array.FindLastIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: 363 }) + 4,
                "both damage tasks flush poison before collected Lifesteal");
        }
    }
    private static void NestedWaves()
    {
        foreach (bool wave in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var source = Summon(p, "BE_193");
            var first = Summon(p.Opponent, "CS2_200"); var last = Summon(p.Opponent, "CS2_200");
            var nested = Summon(p, "CS2_200"); var secondNested = Summon(p, "CS2_200");
            first.TakeDamageTrigger += _ => {
                Verify(first.ToBeDestroyed, "deferred history leaves immediate poison state visible to reactions");
                if (wave) new DamageNumberTask(EntityType.STACK).Process(g, p, source, null,
                    new TaskStack { Number = 1, Playables = new List<IPlayable> { nested, secondNested } });
                else nested.TakeDamage(source, 1);
            };
            int cursor = g.PowerHistory.Full.Count;
            new DamageTask(1, EntityType.STACK).Process(g, p, source, null,
                new TaskStack { Playables = new List<IPlayable> { first, last } });
            var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
            int Poison(int id) => Array.FindIndex(entries, e => e is PowerHistoryMetaData m && m.Type == MetaDataType.POISONOUS && m.Info.Contains(id));
            int lastHit = Array.FindIndex(entries, e => e is PowerHistoryMetaData m && m.Type == MetaDataType.DAMAGE && m.Info.Contains(last.Id));
            Verify(Poison(nested.Id) < lastHit && Poison(first.Id) > lastHit && Poison(last.Id) > Poison(first.Id),
                "same-source nested hit stays local while outer poison waits");
            if (wave) Verify(Poison(secondNested.Id) > Poison(nested.Id) && Poison(secondNested.Id) < lastHit,
                "nested wave flushes its own poison before outer wave resumes");
            History(g, cursor, wave ? new[] { (source as IPlayable, first as ICharacter), (source, last), (source, nested), (source, secondNested) }
                : new[] { (source as IPlayable, first as ICharacter), (source, last), (source, nested) });
        }
    }
    private static void SingleTarget()
    {
        string baseline = null;
        foreach (string kind in new[] { "direct", "task", "number" })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var source = Summon(p, "BE_193"); var victim = Summon(p.Opponent, "CS2_200");
            int cursor = g.PowerHistory.Full.Count;
            if (kind == "direct") victim.TakeDamage(source, 1);
            if (kind == "task") new DamageTask(1, EntityType.OP_MINIONS).Process(g, p, source, null);
            if (kind == "number") new DamageNumberTask(EntityType.OP_MINIONS).Process(g, p, source, null, new TaskStack { Number = 1 });
            string history = string.Concat(g.PowerHistory.Full.Skip(cursor).Select(e => e.Print()));
            baseline ??= history;
            Verify(history == baseline, kind + " single-target history byte-identical to direct hit");
            History(g, cursor, (source, victim));
        }
        var serial = HistoryFactsProof.Fixture(); var actor = Summon(serial.Player1, "BE_193");
        var a = Summon(serial.Player2, "CS2_200"); var b = Summon(serial.Player2, "CS2_200");
        int start = serial.PowerHistory.Full.Count;
        foreach (var victim in new[] { a, b }) new DamageTask(1, EntityType.TARGET).Process(serial, serial.Player1, actor, victim);
        var shots = serial.PowerHistory.Full.Skip(start).ToArray();
        Verify(Array.FindIndex(shots, e => e is PowerHistoryMetaData { Type: MetaDataType.POISONOUS })
            < Array.FindLastIndex(shots, e => e is PowerHistoryMetaData { Type: MetaDataType.DAMAGE }), "serial hits retain separate poison boundaries");
    }
    private static void Combat()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var attacker = Summon(p, "BE_193"); var defender = Summon(p.Opponent, "BE_193");
        attacker.AttackDamage = defender.AttackDamage = 1;
        int cursor = g.PowerHistory.Full.Count;
        Attack(g, attacker, defender);
        Verify(attacker.Zone.Type == Zone.GRAVEYARD && defender.Zone.Type == Zone.GRAVEYARD, "both combat directions poison even pending-dead source");
        History(g, cursor, (attacker, defender), (defender, attacker));

        foreach (string id in new[] { "BRM_019", "CORE_EX1_007" })
        {
            g = HistoryFactsProof.Fixture(); p = g.Player1;
            attacker = Summon(p, "BE_193"); attacker.AttackDamage = 1;
            defender = Summon(p.Opponent, id);
            int hand = p.Opponent.HandZone.Count;
            cursor = g.PowerHistory.Full.Count; Attack(g, attacker, defender);
            Verify(defender.Zone.Type == Zone.GRAVEYARD, id + " poisoned");
            Verify(id == "BRM_019" ? p.Opponent.BoardZone.Count == 0 : p.Opponent.HandZone.Count == hand + 1,
                id == "BRM_019" ? "Grim Patron cannot spawn after poison" : "Acolyte still draws after poison");
            History(g, cursor, (attacker, defender));
        }
        g = HistoryFactsProof.Fixture(); p = g.Player1;
        attacker = Summon(p, "BE_193"); attacker.AttackDamage = 0;
        defender = Summon(p.Opponent, "CS2_200"); cursor = g.PowerHistory.Full.Count;
        Verify(Generic.AttackBlock(p, attacker, defender, true, false), "forced zero-Attack combat resolves");
        Verify(defender.Zone.Type == Zone.PLAY && defender.Damage == 0, "zero Attack cannot poison"); History(g, cursor);
    }
    private static void Weapon()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var knife = (Weapon)Play(g, p, "ULD_715t");
        Verify(knife.Poisonous, "Plagued Knife printed Poisonous");
        var victim = Summon(p.Opponent, "CS2_200"); int cursor = g.PowerHistory.Full.Count;
        Verify(g.Process(HeroAttackTask.Any(p, victim)), "weapon hero attack accepted");
        Verify(victim.Zone.Type == Zone.GRAVEYARD, "weapon poison kills minion"); History(g, cursor, (knife, victim));
        End(g); End(g); cursor = g.PowerHistory.Full.Count;
        Verify(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "weapon attack on hero accepted");
        Verify(p.Opponent.Hero.Damage == knife.AttackDamage && !p.Opponent.Hero.ToBeDestroyed, "weapon cannot poison hero"); History(g, cursor);

        g = HistoryFactsProof.Fixture(); p = g.Player1;
        Play(g, p, "CS2_082"); var snake = Summon(p.Opponent, "LOE_010");
        cursor = g.PowerHistory.Full.Count;
        Verify(g.Process(HeroAttackTask.Any(p, snake)), "hero attacks Poisonous defender");
        Verify(p.Hero.Damage == 2 && !p.Hero.ToBeDestroyed && snake.Zone.Type == Zone.GRAVEYARD,
            "Poisonous defender cannot destroy attacking hero"); History(g, cursor);

        // Off-turn weapons add no Attack; independently granted hero Attack can still deal counter-damage.
        g = HistoryFactsProof.Fixture(); p = g.Player1; knife = (Weapon)Play(g, p, "ULD_715t"); End(g);
        victim = Summon(p.Opponent, "CS2_200"); victim.AttackDamage = 1;
        cursor = g.PowerHistory.Full.Count; Attack(g, victim, p.Hero);
        Verify(victim.Zone.Type == Zone.PLAY && victim.Damage == 0, "inactive weapon alone deals no counter-damage"); History(g, cursor);
        p.Hero.AttackDamage = 1; victim.NumAttacksThisTurn = 0; cursor = g.PowerHistory.Full.Count;
        Attack(g, victim, p.Hero);
        Verify(victim.Zone.Type == Zone.GRAVEYARD, "hero counter-damage while wielding Poisonous weapon poisons"); History(g, cursor, (knife, victim));
    }
    private static void HeroSources()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var knife = (Weapon)Play(g, p, "ULD_715t");
        var victim = Summon(p.Opponent, "CS2_200"); int cursor = g.PowerHistory.Full.Count;
        Play(g, p, "GIL_504");
        Verify(p.Hero.Card.Id == "GIL_504" && p.Hero.Weapon == knife && victim.Damage == 3
            && victim.Zone.Type == Zone.PLAY, "Hagatha Battlecry does not inherit weapon Poisonous");
        History(g, cursor);

        g = HistoryFactsProof.Fixture(); p = g.Player1;
        Play(g, p, "ULD_715t"); var bolf = Summon(p, "AT_124");
        PoolPilotProof.ClassDeck(p);
        cursor = g.PowerHistory.Full.Count;
        Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Verify(p.Hero.Fatigue == 1 && p.Hero.Damage == 0 && bolf.Damage == 1 && bolf.Zone.Type == Zone.PLAY,
            "self-sourced fatigue redirected by Bolf does not inherit weapon Poisonous");
        History(g, cursor);

        g = HistoryFactsProof.Fixture(); p = g.Player1;
        knife = (Weapon)Play(g, p, "ULD_715t");
        victim = Summon(p.Opponent, "CS2_200"); var nested = Summon(p.Opponent, "CS2_200");
        victim.PreDamageTrigger += _ => nested.TakeDamage(p.Hero, 1);
        cursor = g.PowerHistory.Full.Count;
        Verify(g.Process(HeroAttackTask.Any(p, victim)), "nested damage weapon attack accepted");
        Verify(victim.Zone.Type == Zone.GRAVEYARD && nested.Zone.Type == Zone.PLAY && nested.Damage == 1,
            "nested hero-sourced non-combat hit cannot inherit outer combat provenance");
        History(g, cursor, (knife, victim));

        g = HistoryFactsProof.Fixture(); p = g.Player1;
        Play(g, p, "ULD_715t"); p.Hero[GameTag.POISONOUS] = 1;
        victim = Summon(p.Opponent, "CS2_200"); cursor = g.PowerHistory.Full.Count;
        victim.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Verify(victim.Zone.Type == Zone.GRAVEYARD, "hero's own Poisonous applies to non-combat damage");
        History(g, cursor, (p.Hero, victim));
    }
    private static void Deathrattles()
    {
        foreach (bool granted in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var source = Summon(p, granted ? "OG_179" : "EDR_110");
            if (granted) Grant(g, source);
            var victim = Summon(p.Opponent, "CS2_200"); g.SetRandomSeed(3);
            int cursor = g.PowerHistory.Full.Count; Kill(g, source);
            Verify(source.Zone.Type == Zone.GRAVEYARD && victim.Zone.Type == Zone.GRAVEYARD && victim.Damage == 1,
                (granted ? "granted" : "printed") + " Poisonous damages and destroys from dead source");
            History(g, cursor, (source, victim));
        }
    }
    private static void KeywordTiming()
    {
        foreach (string timing in new[] { "before", "predamage", "after" })
        {
            var g = HistoryFactsProof.Fixture(); var source = Summon(g.Player1, "BE_193");
            var victim = Summon(g.Player2, "CS2_200");
            if (timing == "before") source.Silence();
            if (timing == "predamage") victim.PreDamageTrigger += _ => source.Silence();
            bool observedPending = false;
            if (timing == "after") victim.TakeDamageTrigger += _ => { observedPending = victim.ToBeDestroyed; source.Silence(); };
            int cursor = g.PowerHistory.Full.Count; victim.TakeDamage(source, 1); g.DeathProcessingAndAuraUpdate();
            Verify(!source.Poisonous && victim.Zone.Type == (timing == "after" ? Zone.GRAVEYARD : Zone.PLAY), timing + " silence uses keyword at hit");
            if (timing == "after") { Verify(observedPending, "poison precedes ordinary damage reaction"); History(g, cursor, (source, victim)); }
            else History(g, cursor);
        }
    }
    private static void Prevention()
    {
        foreach (string kind in new[] { "immune", "shield", "zero", "prevented", "already-destroyed", "outside-play", "already-lethal" })
        {
            var g = HistoryFactsProof.Fixture(); var source = Summon(g.Player1, "BE_193");
            var victim = Summon(g.Player2, "CS2_200");
            if (kind == "immune") victim.IsImmune = true;
            if (kind == "shield") victim.HasDivineShield = true;
            if (kind == "prevented") victim.PreDamageTrigger += _ => g.CurrentEventData.EventNumber = 0;
            if (kind == "already-destroyed") victim.ToBeDestroyed = true;
            if (kind == "outside-play") Kill(g, victim);
            if (kind == "already-lethal") { victim.Damage = victim.BaseHealth; victim.ToBeDestroyed = false; }
            int before = victim.Damage, cursor = g.PowerHistory.Full.Count;
            victim.TakeDamage(source, kind == "zero" ? 0 : 1);
            if (kind == "already-lethal") { Verify(victim.ToBeDestroyed, "lethal Health still emits poison"); History(g, cursor, (source, victim)); }
            else
            {
                Verify(victim.ToBeDestroyed == (kind is "already-destroyed" or "outside-play"), kind + " no new destruction");
                Verify(victim.Damage == before + (kind == "already-destroyed" ? 1 : 0), kind + " expected damage"); History(g, cursor);
            }
        }
        var noHistory = HistoryFactsProof.Fixture(history: false);
        var poison = Summon(noHistory.Player1, "BE_193"); var enemy = Summon(noHistory.Player2, "CS2_200");
        enemy.TakeDamage(poison, 1); noHistory.DeathProcessingAndAuraUpdate();
        Verify(enemy.Zone.Type == Zone.GRAVEYARD && noHistory.PowerHistory == null, "keyword works without history");
    }
    private static void GenericSources()
    {
        foreach (string kind in new[] { "spell", "hero-power", "location" })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            IPlayable source = kind == "spell" ? Add(p, "CS2_008") : kind == "hero-power" ? p.Hero.HeroPower
                : Entity.FromCard(p, Cards.FromId("CORE_REV_990"));
            source[GameTag.POISONOUS] = 1;
            var victim = Summon(p.Opponent, "CS2_200"); int cursor = g.PowerHistory.Full.Count;
            victim.TakeDamage(source, 1); g.DeathProcessingAndAuraUpdate();
            Verify(victim.Zone.Type == Zone.GRAVEYARD && victim.Damage == 1, kind + " source poisons"); History(g, cursor, (source, victim));
            cursor = g.PowerHistory.Full.Count; p.Opponent.Hero.TakeDamage(source, 1);
            Verify(!p.Opponent.Hero.ToBeDestroyed && p.Opponent.Hero.Damage == 1, kind + " cannot poison hero"); History(g, cursor);
        }
    }
    private static void KillAndLifestealTiming()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var acidmaw = Summon(p, "AT_063");
        var victim = Summon(p.Opponent, "CS2_200");
        var bane = Add(p, "EX1_320"); bane[GameTag.POISONOUS] = 1;
        int cursor = g.PowerHistory.Full.Count;
        PlayEntity(g, p, bane, victim);
        Verify(victim.Zone.Type == Zone.GRAVEYARD && victim.Health > 0 && p.BoardZone.Count == 2
            && p.BoardZone.Any(m => m != acidmaw && m.IsRace(Race.DEMON)), "if that kills it counts pending poison destruction with Acidmaw");
        History(g, cursor, (bane, victim));

        g = HistoryFactsProof.Fixture(); p = g.Player1;
        var source = Summon(p, "BE_193"); source.HasLifeSteal = true; p.Hero.Damage = 5;
        victim = Summon(p.Opponent, "CS2_200"); cursor = g.PowerHistory.Full.Count;
        victim.TakeDamage(source, 1); g.DeathProcessingAndAuraUpdate();
        Verify(victim.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 4, "poison and Lifesteal both resolve");
        History(g, cursor, (source, victim));
        var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
        int poison = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: 363 });
        int lifesteal = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { TriggerKeyword: (int)GameTag.LIFESTEAL });
        int death = Array.FindIndex(entries, e => e is PowerHistoryTagChange t && t.EntityId == victim.Id
            && t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD);
        Verify(poison >= 0 && lifesteal > poison && death > lifesteal, "Poisonous precedes LIFESTEAL and death movement");
    }

}
