using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class LegendaryEliteProof
{
    [GroupProof("legendary-elite")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var scenario in new Action[] { Garona, Medivh, Release, Kingslayers, Hogger, CurrentTags, PetLikes })
        {
            try { scenario(); Console.WriteLine("PASS Legendary ELITE " + scenario.Method.Name); }
            catch (Exception e) { failures.Add(scenario.Method.Name + ": " + e.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static Game Fresh()
    {
        var game = new Game(new GameConfig { StartPlayer = 1, RandomSeed = 17, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, Player1HeroClass = CardClass.ROGUE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            player.BaseMana = 10;
        }
        return game;
    }

    private static void Garona()
    {
        var game = Fresh(); var player = game.Player1;
        var khelos = Summon(player.Opponent, "DINO_410t");
        var ordinary = Summon(player.Opponent, "CS2_101t");
        var spell = Add(player, "CATA_203");
        Core2026Proof.Check(khelos.Card.Rarity == Rarity.INVALID && khelos[GameTag.ELITE] == 1 && khelos.AttackDamage == 20 && khelos.Health == 20,
            "real pinned Khelos has ELITE but no rarity");
        var options = player.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToList();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == khelos.Id), "Garona lists Khelos as legal target");
        Core2026Proof.Check(options.All(t => t.Target?.Id != ordinary.Id), "non-Legendary token remains invalid");
        PegasusGame.AllOptions packet = null;
        var match = new NativeMatch((_, message) => { if (message is PegasusGame.AllOptions all) packet = all; });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        typeof(NativeMatch).GetMethod("SendHumanOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(match, null);
        Core2026Proof.Check(packet.Options.Any(o => o.MainOption?.Id == spell.Id && o.MainOption.Targets.Any(t => t.Id == khelos.Id)),
            "native options packet includes Khelos target");
        PlayEntity(game, player, spell, khelos);
        Core2026Proof.Check(khelos.Zone.Type == Zone.GRAVEYARD && ordinary.Zone.Type == Zone.PLAY,
            "Garona destroys Khelos and preserves ordinary token");
    }

    private static void Medivh()
    {
        var game = Fresh(); var player = game.Player1;
        var spell = Add(player, "CATA_308");
        var khelos = Summon(player, "DINO_410t");
        Core2026Proof.Check(spell.Cost == 1 && spell[GameTag.COST] == 1 && PoweredUp.IsMet(spell),
            "Medivh discounts and highlights for controlled elite token");
        khelos[GameTag.ELITE] = 0; game.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 5 && !PoweredUp.IsMet(spell), "Medivh observes current ELITE override");
        khelos.NativeTags.Remove(GameTag.ELITE); game.AuraUpdate();
        khelos.Silence(); game.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(spell.Cost == 1, "silence retains printed elite identity");
        new EffectContext(game, player.Id, spell.Id).Transform(khelos.Id, "CS2_101t"); game.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 5, "transform to ordinary token removes Legendary condition");
    }

    private static void Release()
    {
        var game = Fresh(); var player = game.Player1;
        var khelos = (SabberStoneCore.Model.Entities.Minion)Add(player, "DINO_410t");
        var ordinary = (SabberStoneCore.Model.Entities.Minion)Add(player, "CS2_101t");
        Play(game, player, "JAIL_387");
        Core2026Proof.Check(khelos.AttackDamage == 23 && khelos.Health == 22 && ordinary.AttackDamage == 2 && ordinary.Health == 2,
            "Release gives elite token +3/+2 and ordinary token +1/+1");
    }

    private static void Kingslayers()
    {
        var game = Fresh(); var player = game.Player1;
        foreach (var owner in new[] { player, player.Opponent })
        {
            foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
            SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_101t"), zone: owner.DeckZone);
            SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("DINO_410t"), zone: owner.DeckZone);
        }
        Play(game, player, "TIME_875t1");
        Core2026Proof.Check(game.Process(HeroAttackTask.Any(player, player.Opponent.Hero)), "Kingslayers attack accepted");
        Core2026Proof.Check(player.HandZone.Any(c => c.Card.Id == "DINO_410t") && player.Opponent.HandZone.Any(c => c.Card.Id == "DINO_410t")
            && player.DeckZone.Single().Card.Id == "CS2_101t" && player.Opponent.DeckZone.Single().Card.Id == "CS2_101t",
            "Kingslayers draws both elite tokens, skipping ordinary tokens");
    }

    private static void Hogger()
    {
        var game = B15GameSetupProof.Resolved(new[] { "JAIL_384", "DINO_410t", "CS2_101t", "CS2_182", "CS2_182", "CS2_182", "CS2_182" });
        Core2026Proof.Check(game.Player1.HandZone.Concat(game.Player1.DeckZone).Count(c => c.Card.Id == "DINO_410t") == 2,
            "Hogger copies existing elite token in opening hand or deck");
        var player = game.Player1;
        var sampled = OwnDeckLedger.Observe(game, player, null).Sample(game, player, 17);
        Core2026Proof.Check(sampled.GetValueOrDefault("DINO_410t") == player.DeckZone.Count(c => c.Card.Id == "DINO_410t"),
            "NPC deck ledger reconstructs Hogger's elite token copy from declared deck");
    }

    private static void CurrentTags()
    {
        var game = Fresh(); var player = game.Player1;
        var legendary = Summon(player.Opponent, "EX1_563");
        var ordinary = Summon(player.Opponent, "CS2_101t");
        var spell = Add(player, "CATA_203");
        legendary[GameTag.ELITE] = 0; ordinary[GameTag.ELITE] = 1;
        foreach (var state in new[] { game, game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            var options = state.Player1.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
            Core2026Proof.Check(options.Length == 1 && options[0].Target?.Id == ordinary.Id,
                "current entity ELITE overrides printed rarity in original, clone and snapshot");
        }
        Core2026Proof.Check(legendary.Card.Rarity == Rarity.LEGENDARY && ordinary.Card.Rarity != Rarity.LEGENDARY,
            "entity overrides leave collection rarity untouched");
    }

    private static void PetLikes()
    {
        foreach (var (id, elite) in new[] { ("DINO_410t", 1), ("EX1_563", 0), ("CS2_101t", 0) })
        {
            var game = Fresh(); var player = game.Player1; var card = Add(player, id);
            card[GameTag.ELITE] = elite;
            PlayEntity(game, player, card);
            var likes = new NativePetLikes(8, PetXpPolicy.PresetB);
            likes.Project(game.PowerHistory.Full.ToList(), player.PlayerId, 9999);
            Core2026Proof.Check(likes.Fired.Contains("legendary-minion") == (elite == 1), "pet like observes current ELITE for " + id);
        }
    }
}
