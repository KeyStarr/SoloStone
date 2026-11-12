using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class HeroDivineShieldProof
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("HeroDivineShield: " + message);
    }

    internal static Game NewGame(bool history)
    {
        var game = new Game(new GameConfig { RandomSeed = 31, StartPlayer = 1,
            SkipMulligan = true, Shuffle = false, History = history, Logging = false,
            FormatType = FormatType.FT_WILD, Player1HeroClass = CardClass.MAGE,
            Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        game.Player1.BaseMana = 10;
        return game;
    }

    internal static Minion Summon(Game game, string id)
    {
        var minion = (Minion)Entity.FromCard(game.Player1, Cards.FromId(id));
        Generic.SummonBlock(game, minion, -1, null);
        game.DeathProcessingAndAuraUpdate();
        return minion;
    }

    // Use the existing card-task API so these proofs compile against the old engine too.
    private static void Grant(Game game) => new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.HERO)
        .Process(game, game.Player1, game.Player1.Hero, null);

    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            foreach (int armor in new[] { 0, 5 })
            {
                var game = NewGame(history);
                var hero = game.Player1.Hero;
                hero.Armor = armor;
                Grant(game); Grant(game);
                int lost = 0, damaged = 0, dealt = 0;
                game.TriggerManager.LoseDivineShield += sender => { if (sender == hero) lost++; };
                hero.TakeDamageTrigger += _ => damaged++;
                game.TriggerManager.DealDamageTrigger += _ => dealt++;
                Check(hero.TakeDamage(game.Player2.Hero, 8) == 0 && hero.Damage == 0 && hero.Armor == armor,
                    "shield consumes whole hit before armor/health");
                Check(hero[GameTag.DIVINE_SHIELD] == 0 && lost == 1 && damaged == 0 && dealt == 0,
                    "one loss event, no damage events");
                hero.TakeDamage(game.Player2.Hero, 8);
                Check(hero.Damage == 8 - armor && hero.Armor == 0 && lost == 1,
                    "repeated grants do not stack; second hit deals damage");
            }

            foreach (bool minionTarget in new[] { false, true })
            {
                var game = NewGame(history);
                Character target = minionTarget ? Summon(game, "EX1_008") : game.Player1.Hero;
                target[GameTag.DIVINE_SHIELD] = 1;
                foreach (int amount in new[] { 0, -1 })
                {
                    target.TakeDamage(game.Player2.Hero, amount);
                    Check(target[GameTag.DIVINE_SHIELD] == 1 && target.Damage == 0,
                        "nonpositive damage retains shield on every character");
                }
                target.IsImmune = true;
                target.TakeDamage(game.Player2.Hero, 10);
                Check(target[GameTag.DIVINE_SHIELD] == 1 && target.Damage == 0, "Immune precedes shield");
                target.IsImmune = false;
                target.TakeDamage(game.Player2.Hero, 10);
                Check(target[GameTag.DIVINE_SHIELD] == 0 && target.Damage == 0, "shield works after immunity ends");
            }

            FatigueAndLifesteal(history);
            Payment(history);
            PaymentBoundaries(history);
            CombatAndListeners(history);
            Resume(history);
            Bypass(history);
            IceBlock(history);
            SpellComponents(history);
        }
        Console.WriteLine("PASS hero Divine Shield: damage, immunity, armor, fatigue, combat, listeners, costs, Ice Block, clone/snapshot, history on/off");
    }

    private static void FatigueAndLifesteal(bool history)
    {
        var game = NewGame(history);
        var hero = game.Player1.Hero;
        foreach (var card in game.Player1.DeckZone.GetAll()) game.Player1.DeckZone.Remove(card);
        Grant(game);
        Generic.Draw(game.Player1);
        Check(hero.Fatigue == 1 && hero.Damage == 0 && hero[GameTag.DIVINE_SHIELD] == 0,
            "fatigue advances despite shield");
        Generic.Draw(game.Player1);
        Check(hero.Fatigue == 2 && hero.Damage == 2, "next fatigue hit uses advanced counter");
        var source = Summon(game, "CS2_231");
        source.HasLifeSteal = true;
        hero.Damage = 10;
        game.Player2.Hero[GameTag.DIVINE_SHIELD] = 1;
        game.Player2.Hero.TakeDamage(source, 3);
        Check(hero.Damage == 10, "prevented hit cannot Lifesteal");
        game.Player2.Hero.TakeDamage(source, 3);
        Check(hero.Damage == 7, "unshielded Lifesteal remains active");
    }

    private static void Payment(bool history)
    {
        foreach (bool cardAura in new[] { false, true })
        {
            var game = NewGame(history);
            var hero = game.Player1.Hero;
            var spell = Generic.DrawCard(game.Player1, Cards.FromId("CS2_029"));
            if (cardAura)
            {
                spell.AuraEffects = new SabberStoneCore.Enchants.AuraEffects(CardType.SPELL);
                spell.AuraEffects.CardCostHealth = true;
            }
            else game.Player1.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 1;
            hero.Armor = 7; hero.IsImmune = true; Grant(game);
            int damageEvents = 0;
            hero.TakeDamageTrigger += _ => damageEvents++;
            Check(Generic.PayPhase(game.Player1, spell), "health payment accepted");
            Check(hero.Damage == 4 && hero.Armor == 7 && hero[GameTag.DIVINE_SHIELD] == 1
                && hero.Fatigue == 0 && damageEvents == 0 && game.Player1.UsedMana == 0,
                "health payment bypasses shield, immunity, armor and damage/fatigue events");
        }
    }

    private static void PaymentBoundaries(bool history)
    {
        foreach (bool cardAura in new[] { false, true })
        foreach (int health in new[] { 3, 4, 5 })
        foreach (string path in new[] { "payment", "validation", "options", "play" })
        {
            var game = NewGame(history);
            var player = game.Player1;
            var hero = player.Hero;
            var spell = Generic.DrawCard(player, Cards.FromId("CS2_029"));
            if (cardAura)
            {
                spell.AuraEffects = new SabberStoneCore.Enchants.AuraEffects(CardType.SPELL);
                spell.AuraEffects.CardCostHealth = true;
            }
            else player.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 1;
            hero.Damage = 30 - health;
            hero.Armor = 7;
            hero.IsImmune = true;
            Grant(game);
            player.BaseMana = 0;
            Check(spell.Cost == 4, "health affordability fixture costs four");
            bool affordable = health == 5;
            int lastKnownCost = spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND];
            int historyCount = game.PowerHistory?.Last.Count ?? 0;
            bool accepted = path switch
            {
                "payment" => Generic.PayPhase(player, spell),
                "validation" => spell.IsPlayableByPlayer,
                "options" => player.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == spell),
                _ => Generic.PlayCard(player, spell, game.Player2.Hero)
            };
            Check(accepted == affordable,
                $"{path} requires cost strictly below Health: cardAura={cardAura}, health={health}, history={history}");
            Check(hero.Armor == 7 && hero.IsImmune && hero.HasDivineShield && !hero.ToBeDestroyed
                && player.UsedMana == 0, "health affordability ignores armor, immunity, shield and mana");
            if (!affordable)
            {
                Check(hero.Health == health && spell.Zone.Type == Zone.HAND && player.NumCardsPlayedThisTurn == 0
                    && spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == lastKnownCost
                    && (game.PowerHistory?.Last.Count ?? 0) == historyCount && game.CurrentEventData == null,
                    "rejected payment/play leaves health, hand, counters, cost tag and history unchanged");
            }
            else if (path is "payment" or "play")
                Check(hero.Health == 1, "affordable Health cost can leave exactly one Health");
        }
    }

    private static void CombatAndListeners(bool history)
    {
        var game = NewGame(history);
        var bolvar = Summon(game, "ICC_858");
        var weapon = Generic.DrawCard(game.Player1, Cards.FromId("ICC_071"));
        Check(game.Process(PlayCardTask.Any(game.Player1, weapon)), "equip Light's Sorrow");
        Grant(game);
        game.Player2.Hero.AttackDamage = 3;
        Check(game.Process(HeroAttackTask.Any(game.Player1, game.Player2.Hero)), "shielded weapon attack accepted");
        Check(game.Player1.Hero.Damage == 0 && game.Player1.Hero[GameTag.DIVINE_SHIELD] == 0,
            "weapon combat consumes hero shield on retaliation");
        Check(bolvar.AttackDamage == 1 && ((Weapon)weapon).AttackDamage == 1,
            "minion-only shield-loss listeners ignore hero");
        bolvar.TakeDamage(game.Player2.Hero, 1);
        game.ProcessTasks();
        Check(bolvar.AttackDamage == 3 && ((Weapon)weapon).AttackDamage == 2,
            "minion shield loss still triggers both legacy listeners");
    }

    private static void Resume(bool history)
    {
        var game = NewGame(history);
        Grant(game);
        foreach (var resumed in new[] { game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var hero = resumed.Player1.Hero;
            Check(PowerHistoryBuilder.FullEntity(hero).Entity.Tags[GameTag.DIVINE_SHIELD] == 1
                && PowerHistoryBuilder.ShowEntity(hero).Entity.Tags[GameTag.DIVINE_SHIELD] == 1,
                "full/show snapshots retain hero shield");
            hero.TakeDamage(resumed.Player2.Hero, 5);
            Check(hero.Damage == 0 && hero[GameTag.DIVINE_SHIELD] == 0, "resumed shield prevents damage");
            if (history)
                Check(resumed.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t =>
                    t.EntityId == hero.Id && t.Tag == GameTag.DIVINE_SHIELD && t.Value == 0),
                    "resumed history emits shield removal");
        }
        Check(game.Player1.Hero[GameTag.DIVINE_SHIELD] == 1, "resumed games do not mutate original");
    }

    private static void Bypass(bool history)
    {
        var game = NewGame(history);
        Grant(game);
        var hero = game.Player1.Hero;
        new SilenceTask(EntityType.TARGET).Process(game, game.Player1, hero, hero);
        Check(!hero.IsSilenced && hero[GameTag.DIVINE_SHIELD] == 1, "Silence ignores hero");
        hero.Health = 10;
        Check(hero.Health == 10 && hero[GameTag.DIVINE_SHIELD] == 1, "set health bypasses shield");
        hero.Destroy();
        Check(hero.ToBeDestroyed && hero[GameTag.DIVINE_SHIELD] == 1, "destroy bypasses shield");
    }

    private static void IceBlock(bool history)
    {
        var game = NewGame(history);
        var secret = Generic.DrawCard(game.Player1, Cards.FromId("EX1_295"));
        Check(game.Process(PlayCardTask.Any(game.Player1, secret)), "Ice Block played");
        Grant(game);
        Check(game.Process(EndTurnTask.Any(game.Player1)), "opponent turn for secret");
        var hero = game.Player1.Hero;
        hero.TakeDamage(game.Player2.Hero, 100);
        Check(hero.Damage == 0 && !hero.IsImmune && game.Player1.SecretZone.Count == 1,
            "shield prevents lethal before Ice Block");
        hero.TakeDamage(game.Player2.Hero, 100);
        Check(hero.Damage == 0 && hero.IsImmune && game.Player1.SecretZone.Count == 0,
            "post-predamage immunity still prevents lethal");
    }

    private static void SpellComponents(bool history)
    {
        var game = NewGame(history);
        var hero = game.Player2.Hero;
        hero.HasDivineShield = true;
        var frostbolt = Generic.DrawCard(game.Player1, Cards.FromId("CS2_024"));
        Check(game.Process(PlayCardTask.Any(game.Player1, frostbolt, hero)), "Frostbolt accepted");
        Check(hero.Damage == 0 && !hero.HasDivineShield && hero.IsFrozen,
            "shield prevents damage while independent spell Freeze still resolves");
    }
}
