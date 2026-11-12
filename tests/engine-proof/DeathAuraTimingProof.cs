using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DeathAuraTimingProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        void Verify(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        foreach (var row in new[] { (sources: 1, deaths: 1), (sources: 2, deaths: 2), (sources: 2, deaths: 1) })
        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            var owner = game.Player1;
            foreach (var card in owner.DeckZone.ToArray())
            {
                owner.DeckZone.Remove(card);
                owner.SetasideZone.Add(card);
            }
            owner.Hero.Damage = 0;
            owner.Hero.Fatigue = 0;
            var sources = Enumerable.Range(0, row.sources).Select(_ => Summon(owner, "CORE_CATA_001")).ToArray();
            var undead = Summon(owner, "RLK_958");
            var acolyte = Summon(owner, "CORE_RLK_121");
            Core2026Proof.Check(owner.Hero.IsImmune && owner.Hero[GameTag.IMMUNE] == 1, "death aura Immune fixture");

            foreach (var (mode, branch) in Branches(game))
            {
                var hero = branch.Player1.Hero;
                bool immune = row.sources != row.deaths;
                string label = $"{mode}, Immune sources={row.sources}, deaths={row.deaths}";
                int damageEvents = 0, immuneAtDamage = -1;
                hero.TakeDamageTrigger += _ => { damageEvents++; immuneAtDamage = hero[GameTag.IMMUNE]; };
                foreach (var source in sources.Take(row.deaths)) branch.IdEntityDic[source.Id].ToBeDestroyed = true;
                branch.IdEntityDic[undead.Id].ToBeDestroyed = true;
                branch.DeathProcessingAndAuraUpdate();
                Verify(hero.Fatigue == 1 && hero.Damage == (immune ? 0 : 1),
                    $"{label}: fatigue={hero.Fatigue}, damage={hero.Damage}, expected damage={(immune ? 0 : 1)}");
                Verify(hero.IsImmune == immune && hero[GameTag.IMMUNE] == (immune ? 1 : 0),
                    $"{label}: final IMMUNE={hero[GameTag.IMMUNE]}");
                Verify(damageEvents == (immune ? 0 : 1) && (immune || immuneAtDamage == 0),
                    $"{label}: damage events={damageEvents}, IMMUNE at fatigue damage={immuneAtDamage}");
                Verify(branch.IdEntityDic[acolyte.Id].Zone.Type == Zone.PLAY
                    && branch.IdEntityDic[undead.Id].Zone.Type == Zone.GRAVEYARD
                    && branch.Player1.BoardZone.Count == (immune ? 2 : 1), $"{label}: death batch and surviving listener");
            }
        }

        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            var champion = Summon(game.Player1, "CS2_222");
            var recruit = Summon(game.Player1, "CS2_101t");
            var ghoul = Summon(game.Player1, "FP1_024");
            Core2026Proof.Check(recruit.AttackDamage == 2 && recruit.Health == 2, "death aura stat fixture is buffed 2/2 Recruit");
            foreach (var (mode, branch) in Branches(game))
            {
                var target = (Minion)branch.IdEntityDic[recruit.Id];
                int attackAtDamage = -1, healthAtDamage = -1;
                target.TakeDamageTrigger += _ => { attackAtDamage = target.AttackDamage; healthAtDamage = target.BaseHealth; };
                branch.IdEntityDic[champion.Id].ToBeDestroyed = true;
                branch.IdEntityDic[ghoul.Id].ToBeDestroyed = true;
                branch.DeathProcessingAndAuraUpdate();
                Verify(attackAtDamage == 1 && healthAtDamage == 1,
                    $"{mode}, Champion/Ghoul: Recruit stats at deathrattle damage={attackAtDamage}/{healthAtDamage}, expected 1/1");
                Verify(target.Zone.Type == Zone.GRAVEYARD && branch.Player1.BoardZone.Count == 0,
                    $"{mode}, Champion/Ghoul: Recruit zone={target.Zone.Type}, expected GRAVEYARD");
            }
        }

        foreach (bool destroyWeapon in new[] { true, false })
        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            var spellPower = Summon(game.Player1, "CS2_142");
            var ghoul = Summon(game.Player1, "FP1_024");
            var weapon = (Weapon)Add(game.Player1, "KAR_063");
            Core2026Proof.Check(game.Process(PlayCardTask.Any(game.Player1, weapon)), "equip Spirit Claws");
            Core2026Proof.Check(weapon.Zone == null && game.Player1.Hero.AttackDamage == 3,
                "death weapon aura fixture: equipped without Zone, hero attack=3");
            foreach (var (mode, branch) in Branches(game))
            {
                var hero = branch.Player1.Hero;
                int attackAtDamage = -1;
                ((Minion)branch.IdEntityDic[spellPower.Id]).TakeDamageTrigger += _ => attackAtDamage = hero.AttackDamage;
                if (destroyWeapon) branch.IdEntityDic[weapon.Id].ToBeDestroyed = true;
                branch.IdEntityDic[ghoul.Id].ToBeDestroyed = true;
                branch.DeathProcessingAndAuraUpdate();
                int expected = destroyWeapon ? 0 : 3;
                Verify(attackAtDamage == expected && hero.AttackDamage == expected,
                    $"{mode}, Spirit Claws/Ghoul destroyed={destroyWeapon}: hero attack at deathrattle={attackAtDamage}, final={hero.AttackDamage}, expected {expected}");
                Verify(destroyWeapon
                    ? hero.Weapon == null && branch.IdEntityDic[weapon.Id].Zone?.Type == Zone.GRAVEYARD
                    : hero.Weapon == branch.IdEntityDic[weapon.Id], $"{mode}, Spirit Claws: equipped/graveyard state");
            }
        }

        foreach (bool death in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            var minion = Summon(game.Player1, "CS2_101t");
            var costCard = Add(game.Player1, "LOE_113");
            foreach (var (mode, branch) in Branches(game))
            {
                if (death) branch.IdEntityDic[minion.Id].ToBeDestroyed = true;
                int before = branch.PowerHistory.Full.Count;
                branch.DeathProcessingAndAuraUpdate();
                int costPackets = branch.PowerHistory.Full.Skip(before).OfType<PowerHistoryTagChange>()
                    .Count(change => change.EntityId == costCard.Id && change.Tag == GameTag.COST);
                Verify(costPackets == 2, $"{mode}, unrelated death={death}: expected two normal COST updates, got {costPackets}");
            }
        }

        Core2026Proof.Check(failures.Count == 0, "DeathAuraTiming:\n" + string.Join("\n", failures));
        Console.WriteLine("PASS death aura timing: single/stacked/surviving Immune, fatigue, Champion/Ghoul stats, destroyed/surviving Spirit Claws, original/clone/snapshot");
    }

    private static (string mode, Game game)[] Branches(Game game) => new[] {
        ("original", game), ("clone", game.CloneExact(history: true)), ("snapshot", Game.LoadSnapshot(game.SaveSnapshot()))
    };
}
