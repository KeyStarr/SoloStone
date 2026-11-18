using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wild wave Y0a, lane l3a: Legacy Mage, Warlock and Rogue basics. The group proof is declared in
// Sets/EXPERT1/WildY0aL3AProof.cs (group names are unique) and calls Run here.
internal static class WildY0aL3ALegacyProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case.
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    private const string Yeti = "CS2_182", Squire = "EX1_008", Thalnos = "EX1_012", Shieldmasta = "CS2_179", Axe = "CS2_106";

    internal static void Run()
    {
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_022", (g, p, id) => {
            var taunt = Summon(p.Opponent, Shieldmasta); var shield = Summon(p.Opponent, Squire); var frozen = Summon(p.Opponent, Yeti);
            var mine = Summon(p, Yeti); taunt.Damage = 2; frozen.IsFrozen = true;
            Core2026Proof.Check(taunt.HasTaunt && shield.HasDivineShield, "Polymorph targets have Taunt and Divine Shield first");
            var held = Add(p, id);
            var targets = Targets(p, held);
            Core2026Proof.Check(targets.Count == 4 && targets.All(t => t is Minion) && !targets.Contains(p.Hero) && !targets.Contains(p.Opponent.Hero),
                "Polymorph targets any minion on either side, never a hero");
            Play(g, p, id, taunt);
            var sheep = p.Opponent.BoardZone[0];
            Core2026Proof.Check(sheep.Card.Id == "CS2_tk1" && sheep.AttackDamage == 1 && sheep.Health == 1 && sheep.Damage == 0 && !sheep.HasTaunt
                && sheep.Controller == p.Opponent && p.Opponent.BoardZone.Count == 3, "Polymorph makes a clean 1/1 Sheep in place, losing Taunt and damage");
            Play(g, p, id, shield);
            Core2026Proof.Check(p.Opponent.BoardZone[1].Card.Id == "CS2_tk1" && !p.Opponent.BoardZone[1].HasDivineShield, "Polymorph removes Divine Shield");
            Play(g, p, id, frozen);
            Core2026Proof.Check(p.Opponent.BoardZone[2].Card.Id == "CS2_tk1", "Polymorph transforms a Frozen minion");
            Play(g, p, id, mine);
            Core2026Proof.Check(p.BoardZone[0].Card.Id == "CS2_tk1" && p.BoardZone[0].Controller == p, "Polymorph may target a friendly minion");
            Core2026Proof.Check(Targets(p, Add(p, id)).Count == 4, "Polymorph may re-target a Sheep");
            ((Minion)p.Opponent.BoardZone[0]).Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.Opponent.BoardZone[0].AttackDamage == 1 && p.Opponent.BoardZone[0].Health == 1, "Silence leaves the Sheep a 1/1");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "Polymorph unplayable with no minion on either board");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_023", (g, p, id) => DrawSpell(g, p, id, 2));
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_024", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti); var squire = Summon(p.Opponent, Squire);
            var held = Add(p, id);
            var targets = Targets(p, held);
            Core2026Proof.Check(targets.Count == 5 && targets.Contains(p.Hero) && targets.Contains(p.Opponent.Hero), "Frostbolt targets any character");
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.Damage == 3 && enemy.IsFrozen, "Frostbolt deals 3 and Freezes a minion");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.Opponent.Hero.IsFrozen && !p.Hero.IsFrozen, "Frostbolt hits and Freezes the enemy hero only");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 3 && p.Hero.IsFrozen, "Frostbolt may hit own hero");
            Play(g, p, id, squire);
            Core2026Proof.Check(!squire.HasDivineShield && squire.Damage == 0 && squire.IsFrozen, "Frostbolt pops Divine Shield and still Freezes");
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Frostbolt kills at exact remaining Health scaling-free");
            var mate = Summon(p.Opponent, Yeti); Summon(p, Thalnos);
            Play(g, p, id, mate);
            Core2026Proof.Check(mate.Damage == 4, "Frostbolt scales with Spell Damage");
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.IsFrozen && mine.Damage == 4, "Frostbolt may target a friendly minion");
            End(g);
            Core2026Proof.Check(mate.IsFrozen && p.Opponent.Hero.IsFrozen, "Freeze lasts through the target's own turn start"); End(g);
            Core2026Proof.Check(!mate.IsFrozen && !p.Opponent.Hero.IsFrozen, "Freeze thaws at the end of the frozen side's turn");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_026", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var other = Summon(p.Opponent, Squire); var mine = Summon(p, Yeti);
            Play(g, p, id);
            Core2026Proof.Check(enemy.IsFrozen && other.IsFrozen && enemy.Damage == 0 && other.HasDivineShield, "Frost Nova Freezes every enemy minion without damage");
            Core2026Proof.Check(!mine.IsFrozen && !p.Hero.IsFrozen && !p.Opponent.Hero.IsFrozen, "Frost Nova spares friendly minions and heroes");
            End(g);
            Core2026Proof.Check(enemy.IsFrozen && !p.Opponent.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id),
                "Frozen enemy minion cannot attack on its turn");
            End(g);
            Core2026Proof.Check(!enemy.IsFrozen && !other.IsFrozen, "Frost Nova thaws at the end of the owner's next turn");
            StandardSetsProof.ParkBoard(p.Opponent); Play(g, p, id);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 0, "Frost Nova with empty enemy board does nothing");
            var late = Summon(p.Opponent, Yeti);
            Core2026Proof.Check(!late.IsFrozen, "Frost Nova does not Freeze later summons");
            Summon(p, Thalnos); Play(g, p, id);
            Core2026Proof.Check(late.IsFrozen && late.Damage == 0, "Frost Nova is unaffected by Spell Damage");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_027", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti);
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "CS2_mirror" && m.AttackDamage == 0 && m.Health == 2 && m.HasTaunt)
                && p.Opponent.BoardZone.Count == 1 && p.Opponent.BoardZone[0] == enemy, "Mirror Image summons two 0/2 Taunt images on own side only");
            p.BoardZone[0].Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!p.BoardZone[0].HasTaunt && p.BoardZone[1].HasTaunt, "Silence removes one image's Taunt only");
            for (int i = 0; i < 3; i++) Summon(p, Yeti);
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_mirror") == 4 - 0, "Mirror Image fills to seven (two on five minions)");
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "Mirror Image unplayable on a full board");
            var m6 = p.BoardZone.Last(); m6.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.BoardZone.Count == 6, "setup six minions");
            PlayEntity(g, p, spell);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_mirror") == 4, "Mirror Image with one slot summons exactly one");
            End(g);
            Play(g, p.Opponent, id);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 3 && p.BoardZone.Count == 7, "Opponent's Mirror Image fills their own side");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_029", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
            var held = Add(p, id);
            Core2026Proof.Check(Targets(p, held).Count == 4, "Fireball targets any character");
            PlayEntity(g, p, held, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 6 && p.Hero.Damage == 0, "Fireball deals 6 to the enemy hero");
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Fireball kills a 5-Health minion");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 6, "Fireball may hit own hero");
            Summon(p, Thalnos); Play(g, p, id, mine);
            Core2026Proof.Check(mine.Zone.Type == Zone.GRAVEYARD, "Fireball scales with Spell Damage (7 on 5 Health)");
            var squire = Summon(p.Opponent, Squire); Play(g, p, id, squire);
            Core2026Proof.Check(squire.Zone.Type == Zone.PLAY && !squire.HasDivineShield && squire.Damage == 0, "Fireball only pops Divine Shield");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 13, "Fireball plus Spell Damage hits for 7");
        });
        WildSetsProof.Case("CS2_057", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
            var held = Add(p, id);
            var targets = Targets(p, held);
            Core2026Proof.Check(targets.Count == 2 && targets.All(t => t is Minion), "Shadow Bolt targets minions on both sides, never heroes");
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.Damage == 4 && p.Opponent.Hero.Damage == 0, "Shadow Bolt deals 4 to a minion");
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.Damage == 4, "Shadow Bolt may hit a friendly minion");
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Shadow Bolt kills at exact remaining Health");
            Summon(p, Thalnos); Play(g, p, id, mine);
            Core2026Proof.Check(mine.Zone.Type == Zone.GRAVEYARD, "Shadow Bolt scales with Spell Damage");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "Shadow Bolt unplayable with no minion");
        });
        WildSetsProof.Case("CS2_061", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti);
            p.Hero.Damage = 10;
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Damage == 2 && p.Hero.Damage == 8, "Drain Life deals 2 and restores 2 to own hero");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 6, "Drain Life hits the enemy hero and heals own hero");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 6, "Drain Life on own hero nets zero");
            Summon(p, Thalnos); Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Damage == 5 || enemy.Zone.Type == Zone.GRAVEYARD, "Drain Life damage scales with Spell Damage");
            Core2026Proof.Check(p.Hero.Damage == 4, "Drain Life healing is not boosted by Spell Damage");
            var squire = Summon(p.Opponent, Squire); p.Hero.Damage = 5;
            Play(g, p, id, squire);
            Core2026Proof.Check(!squire.HasDivineShield && squire.Damage == 0 && p.Hero.Damage == 3, "Drain Life still restores when Divine Shield blocks the damage");
            p.Hero.Damage = 1; p.Opponent.Hero.Damage = 4; Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 7, "Drain Life heal caps at full Health and never heals the enemy hero");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_062", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
            Play(g, p, id);
            Core2026Proof.Check(enemy.Damage == 3 && mine.Damage == 3 && p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3,
                "Hellfire deals 3 to every character, both sides");
            Play(g, p, id);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && mine.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 6 && p.Opponent.Hero.Damage == 6,
                "Hellfire kills at exact remaining Health and repeats on heroes");
            var a = Summon(p, Yeti); var b = Summon(p.Opponent, Yeti); Summon(p, Thalnos);
            Play(g, p, id);
            Core2026Proof.Check(a.Damage == 4 && b.Damage == 4 && p.Hero.Damage == 10 && p.Opponent.Hero.Damage == 10, "Hellfire scales with Spell Damage");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Play(g, p, id);
            Core2026Proof.Check(p.Hero.Damage == 13 && p.Opponent.Hero.Damage == 13, "Hellfire with empty boards hits only the heroes");
        });
        WildSetsProof.Case("CS2_063", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti); var silenced = Summon(p.Opponent, Yeti);
            var held = Add(p, id);
            var targets = Targets(p, held);
            Core2026Proof.Check(targets.Count == 2 && targets.All(t => t is Minion m && m.Controller == p.Opponent), "Corruption targets enemy minions only");
            PlayEntity(g, p, held, enemy);
            Play(g, p, id, silenced);
            Core2026Proof.Check(enemy.Zone.Type == Zone.PLAY && enemy.Damage == 0 && silenced.Zone.Type == Zone.PLAY, "Corruption does nothing immediately");
            silenced.Silence(); g.DeathProcessingAndAuraUpdate();
            Resume(g, next => {
                End(next);
                Core2026Proof.Check(next.IdEntityDic[enemy.Id].Zone.Type == Zone.PLAY, "Corruption does not trigger at the minion owner's turn start");
                End(next);
                Core2026Proof.Check(next.IdEntityDic[enemy.Id].Zone.Type == Zone.GRAVEYARD, "Corruption destroys at the start of the caster's turn");
                Core2026Proof.Check(next.IdEntityDic[silenced.Id].Zone.Type == Zone.PLAY, "Silence before the turn start removes Corruption");
            });
            End(g); End(g);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && silenced.Zone.Type == Zone.PLAY && mine.Zone.Type == Zone.PLAY, "Corruption destroys only its target");
            var cursed = Summon(p, Yeti); End(g);
            Play(g, p.Opponent, id, cursed);
            End(g);
            Core2026Proof.Check(cursed.Zone.Type == Zone.PLAY, "Opponent's Corruption does not fire at my turn start");
            End(g);
            Core2026Proof.Check(cursed.Zone.Type == Zone.GRAVEYARD, "Opponent's Corruption fires at their own turn start");
            End(g);
            var shielded = Summon(p.Opponent, Squire);
            Play(g, p, id, shielded); End(g); End(g);
            Core2026Proof.Check(shielded.Zone.Type == Zone.GRAVEYARD, "Corruption destroys through Divine Shield");
        });
        // held: used by Standard (data/pools/pool-eligibility-v1.json) until #87
        HeldForStandard("CS2_064", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti); var shield = Summon(p.Opponent, Squire);
            var infernal = (Minion)Play(g, p, id);
            Core2026Proof.Check(enemy.Damage == 1 && mine.Damage == 1 && p.Hero.Damage == 1 && p.Opponent.Hero.Damage == 1 && infernal.Damage == 0
                && !shield.HasDivineShield, "Dread Infernal Battlecry deals 1 to every other character");
            Summon(p, id);
            Core2026Proof.Check(enemy.Damage == 1 && p.Hero.Damage == 1, "Summoning without playing does not trigger the Battlecry");
            Summon(p, Thalnos);
            Play(g, p, id);
            Core2026Proof.Check(enemy.Damage == 2 && p.Hero.Damage == 2, "Dread Infernal Battlecry is not a spell, no Spell Damage bonus");
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_012") == 0, "Thalnos (1 Health) dies to the Battlecry");
            infernal.Silence();
            Core2026Proof.Check(infernal.AttackDamage == 6 && infernal.Zone.Type == Zone.PLAY, "Silenced Dread Infernal keeps its body");
        });
        // held: used by Standard (plugin/Npc/TriggerMeasure.cs) until #87
        HeldForStandard("CS2_065", (g, p, id) => {
            var v = (Minion)Play(g, p, id);
            Core2026Proof.Check(v.AttackDamage == 1 && v.Health == 3 && v.HasTaunt && v.Card.IsRace(Race.DEMON), "Voidwalker is a 1/3 Demon with Taunt");
            var enemyAttacker = Summon(p.Opponent, Yeti); Summon(p, Yeti);
            End(g);
            var attackTargets = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemyAttacker.Id)
                .Select(t => t.Target?.Id).ToArray();
            Core2026Proof.Check(attackTargets.Length == 1 && attackTargets[0] == v.Id, "Taunt forces attacks onto Voidwalker");
            End(g);
            v.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!v.HasTaunt && v.Health == 3, "Silence removes Taunt");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_072", (g, p, id) => {
            var fresh = Summon(p.Opponent, Yeti); var hurt = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti); hurt.Damage = 1;
            var held = Add(p, id);
            var targets = Targets(p, held);
            Core2026Proof.Check(targets.Contains(fresh) && targets.Contains(mine) && !targets.Contains(hurt) && targets.All(t => t is Minion),
                "Backstab targets undamaged minions on either side, no heroes, no damaged minion");
            PlayEntity(g, p, held, fresh);
            Core2026Proof.Check(fresh.Damage == 2, "Backstab deals 2");
            Core2026Proof.Check(!Targets(p, Add(p, id)).Contains(fresh), "Backstab cannot target the minion it just damaged");
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.Damage == 2 && mine.Zone.Type == Zone.PLAY, "Backstab may hit a friendly minion");
            Summon(p, Thalnos);
            var other = Summon(p.Opponent, Yeti); Play(g, p, id, other);
            Core2026Proof.Check(other.Damage == 3, "Backstab scales with Spell Damage");
            var squire = Summon(p.Opponent, Squire); Play(g, p, id, squire);
            Core2026Proof.Check(squire.Zone.Type == Zone.PLAY && !squire.HasDivineShield && squire.Damage == 0, "Backstab only pops Divine Shield");
            Core2026Proof.Check(Targets(p, Add(p, id)).Contains(squire), "Divine Shield minion that took no damage is still undamaged");
        });
        WildSetsProof.Case("CS2_075", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
            Play(g, p, id);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.Hero.Damage == 0 && enemy.Damage == 0 && mine.Damage == 0, "Sinister Strike deals 3 to the enemy hero only");
            Summon(p, Thalnos); Play(g, p, id);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 7, "Sinister Strike scales with Spell Damage");
            End(g);
            Play(g, p.Opponent, id);
            Core2026Proof.Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 7, "Opponent's Sinister Strike hits my hero");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_076", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti); var squire = Summon(p.Opponent, Squire); var stealth = Summon(p.Opponent, "CS2_059");
            var held = Add(p, id);
            var targets = Targets(p, held);
            Core2026Proof.Check(targets.Contains(enemy) && targets.Contains(squire) && !targets.Contains(mine) && !targets.Contains(stealth)
                && targets.All(t => t is Minion), "Assassinate targets enemy minions only, not Stealthed ones");
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 0, "Assassinate destroys the minion");
            Play(g, p, id, squire);
            Core2026Proof.Check(squire.Zone.Type == Zone.GRAVEYARD, "Assassinate destroys through Divine Shield");
            var thalnos = Summon(p.Opponent, Thalnos); int ph = p.HandZone.Count, oh = p.Opponent.HandZone.Count, od = p.Opponent.DeckZone.Count;
            Play(g, p, id, thalnos);
            Core2026Proof.Check(p.Opponent.HandZone.Count == oh + 1 && p.Opponent.DeckZone.Count == od - 1 && p.HandZone.Count == ph,
                "Assassinate triggers the victim's Deathrattle for its owner only");
            stealth.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(Targets(p, Add(p, id)).Contains(stealth), "Silenced Stealth minion becomes targetable");
            StandardSetsProof.ParkBoard(p.Opponent);
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "Assassinate unplayable with no enemy minion");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_074", (g, p, id) => {
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "Deadly Poison unplayable without a weapon");
            Play(g, p, Axe);
            var axe = p.Hero.Weapon;
            Core2026Proof.Check(axe != null && axe.AttackDamage == 3 && axe.Durability == 2, "Fiery War Axe equipped");
            PlayEntity(g, p, spell);
            Core2026Proof.Check(axe.AttackDamage == 5 && axe.Durability == 2 && p.Hero.AttackDamage == 5, "Deadly Poison gives the weapon +2 Attack, durability unchanged");
            Play(g, p, id);
            Core2026Proof.Check(axe.AttackDamage == 7 && p.Hero.AttackDamage == 7, "Deadly Poison stacks");
            End(g);
            Core2026Proof.Check(axe.AttackDamage == 7, "Deadly Poison is permanent");
            Play(g, p.Opponent, Axe);
            Core2026Proof.Check(p.Opponent.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.AttackDamage == 7, "Opponent's fresh weapon is untouched");
            Play(g, p.Opponent, id);
            Core2026Proof.Check(p.Opponent.Hero.Weapon.AttackDamage == 5 && p.Hero.Weapon.AttackDamage == 7, "Opponent's Deadly Poison buffs only their own weapon");
            Play(g, p.Opponent, Axe);
            Core2026Proof.Check(p.Opponent.Hero.Weapon.AttackDamage == 3, "Replacing the weapon drops the poison");
        });
        WildSetsProof.Case("CS2_077", (g, p, id) => DrawSpell(g, p, id, 4));
    }

    // CS2_025 Arcane Explosion: BLOCKED-LOCALCATALOG. Its 37.0 identity moves the set from CORE to LEGACY, but
    // plugin/LocalCardCatalog.cs requires every NewBasicIds card (CS2_025 is one) to be a CORE FREE card, so listing it
    // in the batch makes the catalog's static constructor throw. Wire WildSetsProof.Case("CS2_025", ArcaneExplosion) in
    // Run and add the id to the batch once the catalog accepts the retail set.
    internal static void ArcaneExplosion(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, Yeti); var other = Summon(p.Opponent, Squire); var mine = Summon(p, Yeti);
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 1 && other.Zone.Type == Zone.PLAY && !other.HasDivineShield && other.Damage == 0,
            "Arcane Explosion deals 1 to each enemy minion (Divine Shield absorbs)");
        Core2026Proof.Check(mine.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Arcane Explosion spares friendly minions and both heroes");
        Summon(p, Thalnos); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && mine.Damage == 0 && other.Zone.Type == Zone.GRAVEYARD, "Arcane Explosion scales with Spell Damage and kills the 1-Health minion");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Arcane Explosion kills at exact remaining Health");
        StandardSetsProof.ParkBoard(p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.Opponent.Hero.Damage == 0, "Arcane Explosion with empty enemy board does nothing");
        var kill = Summon(p.Opponent, Squire); kill.Damage = 0; kill.HasDivineShield = false; Play(g, p, id);
        Core2026Proof.Check(kill.Zone.Type == Zone.GRAVEYARD, "Arcane Explosion with Spell Damage kills a fresh 1-Health minion");
    }

    // CS2_032 Flamestrike: BLOCKED-HSDATA. Retail 37.0 deals 5 (the card file and engine text say 4) and the English text
    // change cannot be regenerated without the hsdata CardDefs (locale rule). Once it is available, add CS2_032 to
    // tests/progression/batches/wild/wild-y0a-l3a.txt, regenerate the card file and wire WildSetsProof.Case("CS2_032", Flamestrike) in Run.
    internal static void Flamestrike(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, Yeti); var other = Summon(p.Opponent, Squire); var mine = Summon(p, Yeti);
        Play(g, p, id);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && other.Zone.Type == Zone.PLAY && !other.HasDivineShield && other.Damage == 0,
            "Flamestrike kills a 5-Health minion; Divine Shield absorbs");
        Core2026Proof.Check(mine.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Flamestrike spares friendly minions and both heroes");
        var tough = Summon(p.Opponent, "CS2_200"); // Boulderfist Ogre 6/7
        Play(g, p, id);
        Core2026Proof.Check(tough.Damage == 5 && tough.Health == 2, "Flamestrike deals exactly 5 to a 7-Health minion");
        Summon(p, Thalnos); Play(g, p, id);
        Core2026Proof.Check(tough.Zone.Type == Zone.GRAVEYARD, "Flamestrike scales with Spell Damage");
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0, "Flamestrike with empty enemy board does nothing");
    }

    internal static List<ICharacter> Targets(Controller p, IPlayable source) =>
        p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id && t.Target != null)
            .Select(t => (ICharacter)t.Target).ToList();

    internal static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }

    // Draw-N spell: hidden zones on both seats, full hand burns, empty deck fatigues.
    private static void DrawSpell(Game g, Controller p, string id, int n)
    {
        int hand = p.HandZone.Count, deck = p.DeckZone.Count, oHand = p.Opponent.HandZone.Count, oDeck = p.Opponent.DeckZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == hand + n && p.DeckZone.Count == deck - n, id + " draws exactly " + n + " from own deck");
        Core2026Proof.Check(p.Opponent.HandZone.Count == oHand && p.Opponent.DeckZone.Count == oDeck && p.Hero.Damage == 0, id + " touches nothing on the opponent seat");
        for (int i = p.HandZone.Count; i < 9; i++) Add(p, Yeti);
        deck = p.DeckZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - n && p.Hero.Damage == 0, id + " with a full hand burns the excess but still takes " + n + " from the deck");
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        int handBefore = p.HandZone.Count;
        Play(g, p, id);
        int fatigue = Enumerable.Range(1, n).Sum();
        Core2026Proof.Check(p.Hero.Damage == fatigue && p.HandZone.Count == handBefore, id + " on an empty deck deals fatigue 1.." + n + " and draws nothing");
        End(g);
        Play(g, p.Opponent, id);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == oDeck - 1 - n && p.Hero.Damage == fatigue, "Opponent's " + id + " draws from their own deck");
    }
}
