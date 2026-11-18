using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static WildY0aL1ALegacyProof;

// Wild wave Y0a, lane l1a: Expert1 neutral minions. Run from the group in Sets/LEGACY/WildY0aL1AProof.cs.
internal static class WildY0aL1AExpertProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case.
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    internal static void Run()
    {
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS1_069", Taunt);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_146", SouthseaDeckhand);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_151", SilverHandKnight);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_161", Stealth);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_169", Windfury);
    }

    private static void Equip(Game g, Controller p) => Play(g, p, "CS2_082");

    private static void SouthseaDeckhand(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id);
        Core2026Proof.Check(!m.HasCharge && AttackTargets(p, m).Count == 0, id + " has no Charge without a weapon");
        var theirWeapon = (Weapon)Entity.FromCard(p.Opponent, Cards.FromId("CS2_082"));
        p.Opponent.Hero.AddWeapon(theirWeapon);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!m.HasCharge && AttackTargets(p, m).Count == 0, id + " ignores the opponent's weapon");
        Equip(g, p);
        Core2026Proof.Check(m.HasCharge && AttackTargets(p, m).Contains(p.Opponent.Hero.Id), id + " gains Charge and may attack when I equip a weapon");
        var fresh = (Minion)Play(g, p, id);
        Core2026Proof.Check(fresh.HasCharge && AttackTargets(p, fresh).Contains(p.Opponent.Hero.Id), id + " enters play charging while armed");
        Resume(g, next => Core2026Proof.Check(((Minion)next.IdEntityDic[m.Id]).HasCharge
            && AttackTargets(next.Player1, (Minion)next.IdEntityDic[m.Id]).Count > 0, id + " Charge survives clone and snapshot"));
        Attack(g, m, p.Opponent.Hero);
        p.Hero.RemoveWeapon(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!m.HasCharge && !fresh.HasCharge && AttackTargets(p, fresh).Count == 0 && AttackTargets(p, m).Count == 0,
            id + " loses Charge, and a fresh one is summoning sick again, when the weapon leaves");
        Silence(g, fresh);
        Equip(g, p);
        Core2026Proof.Check(m.HasCharge && !fresh.HasCharge && fresh.IsSilenced, id + " Silenced copy never charges again");
        Silence(g, m);
        Core2026Proof.Check(!m.HasCharge, id + " Silence removes Charge");
        p.Hero.RemoveWeapon(); g.DeathProcessingAndAuraUpdate();
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(!theirs.HasCharge || p.Opponent.Hero.Weapon != null, id + " opponent copy follows its own weapon");
        End(g);
    }

    private static void SilverHandKnight(Game g, Controller p, string id)
    {
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = Add(p, id);
        PlayEntity(g, p, held);
        var knight = (Minion)held;
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone[knight.ZonePosition + 1].Card.Id == "CS2_152", id + " summons a Squire " + string.Join(",", p.BoardZone.Select(x => x.Card.Id)));
        var squire = p.BoardZone[knight.ZonePosition + 1];
        Core2026Proof.Check(squire.AttackDamage == 2 && squire.Health == 2 && squire.Card.Cost == 1 && p.Opponent.BoardZone.Count == 1,
            "Squire is a 2/2 on my side only");
        Core2026Proof.Check(knight.AttackDamage == 4 && knight.Health == 4, id + " is a 4/4 with the Squire to its right");
        var left = Add(p, id);
        Core2026Proof.Check(g.Process(new PlayCardTask(p, left, null, 0)), id + " played at the left edge");
        Core2026Proof.Check(p.BoardZone[0] == left && p.BoardZone[1].Card.Id == "CS2_152", id + " Squire lands immediately right of the knight");
        Core2026Proof.Check(p.BoardZone.Count == 6, "six minions on my board");
        Kill(g, a); Kill(g, b); Kill(g, p.BoardZone[1]);
        Silence(g, knight); Kill(g, knight);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Count(x => x.Card.Id == "CS2_152") == 1, id + " Silence and death keep the Squire and add none");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(x => x.Card.Id == "CS2_152") == 1, id + " with room for exactly one more summons the Squire");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.All(x => x.Card.Id != "CS2_152"), id + " on a board of six is full and finds no room for a Squire");
        End(g);
        var theirs = Add(p.Opponent, id);
        PlayEntity(g, p.Opponent, theirs);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 3 && p.Opponent.BoardZone.Count(x => x.Card.Id == "CS2_152") == 1, id + " opponent copy summons for the opponent");
        Resume(g, next => Core2026Proof.Check(next.Player2.BoardZone.Count == 3, id + " board survives clone and snapshot"));
        End(g);
    }

    private static void Stealth(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id, GameTag.STEALTH);
        var theirs = Summon(p.Opponent, id);
        var attacker = Summon(p, "CS2_182"); attacker.IsExhausted = false;
        var probe = Add(p, "CS2_008");
        var moon = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == probe.Id).Select(t => t.Target?.Id ?? 0).ToHashSet();
        Core2026Proof.Check(moon.Contains(m.Id) && !moon.Contains(theirs.Id), id + " my own Stealth minion is targetable, the enemy's is not");
        Core2026Proof.Check(AttackTargets(p, attacker).SetEquals(new[] { p.Opponent.Hero.Id }), id + " an enemy Stealth minion cannot be attacked");
        Resume(g, next => Core2026Proof.Check(((Minion)next.IdEntityDic[theirs.Id]).HasStealth, id + " Stealth survives clone and snapshot"));
        End(g);
        var enemyAttacker = Summon(p.Opponent, "CS2_182"); enemyAttacker.IsExhausted = false;
        var spell = Add(p.Opponent, "CS2_008");
        var theirMoon = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).Select(t => t.Target?.Id ?? 0).ToHashSet();
        Core2026Proof.Check(!theirMoon.Contains(m.Id) && theirMoon.Contains(theirs.Id), id + " the opponent's spells cannot target my Stealth minion");
        Core2026Proof.Check(!AttackTargets(p.Opponent, enemyAttacker).Contains(m.Id) && AttackTargets(p.Opponent, enemyAttacker).Contains(p.Hero.Id),
            id + " the opponent cannot attack my Stealth minion");
        End(g);
        Attack(g, m, p.Opponent.Hero);
        Core2026Proof.Check(!m.HasStealth && p.Opponent.Hero.Damage == 7, id + " attacking breaks Stealth and deals seven");
        var second = (Minion)Play(g, p, id);
        Silence(g, second);
        Core2026Proof.Check(!second.HasStealth && second.IsSilenced, id + " Silence removes Stealth");
        End(g);
        Core2026Proof.Check(AttackTargets(p.Opponent, enemyAttacker).Contains(second.Id) && AttackTargets(p.Opponent, enemyAttacker).Contains(m.Id),
            id + " revealed and Silenced copies can be attacked");
        End(g);
    }

    private static void Windfury(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id, GameTag.WINDFURY);
        var quiet = (Minion)Play(g, p, id);
        Silence(g, quiet);
        var theirs = Summon(p.Opponent, id);
        NextOwnTurn(g);
        var hero = p.Opponent.Hero;
        Attack(g, m, hero);
        Core2026Proof.Check(AttackTargets(p, m).Contains(hero.Id) && hero.Damage == 1, id + " may attack a second time");
        Resume(g, next => Core2026Proof.Check(AttackTargets(next.Player1, (Minion)next.IdEntityDic[m.Id]).Contains(hero.Id),
            id + " second attack survives clone and snapshot"));
        Attack(g, m, hero);
        Core2026Proof.Check(AttackTargets(p, m).Count == 0 && hero.Damage == 2, id + " attacks at most twice");
        Attack(g, quiet, hero);
        Core2026Proof.Check(AttackTargets(p, quiet).Count == 0 && hero.Damage == 3, id + " Silenced copy attacks once");
        End(g);
        Attack(g, theirs, p.Hero);
        Core2026Proof.Check(AttackTargets(p.Opponent, theirs).Contains(p.Hero.Id), id + " opponent copy attacks twice as well");
        Attack(g, theirs, p.Hero);
        Core2026Proof.Check(AttackTargets(p.Opponent, theirs).Count == 0 && p.Hero.Damage == 2, id + " opponent copy attacks at most twice");
        End(g);
    }
}
