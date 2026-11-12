using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Model;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class CopyInitializationProof
{
    private static Game Fixture(bool history)
    {
        var game = new Game(new GameConfig { RandomSeed = 71, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = history, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
            player.BaseMana = 10;
        }
        return game;
    }

    private static void EnchantedCopies()
    {
        foreach (bool history in new[] { true, false })
        foreach (bool generic in new[] { false, true })
        {
            var game = Fixture(history);
            var player = game.Player1;
            var original = Summon(player, "CS2_182");
            Generic.AddEnchantmentBlock(game, Cards.FromId("CS2_009e"), player.Hero, original); // +2/+2, Taunt
            Generic.AddEnchantmentBlock(game, Cards.FromId("CS2_011o"), player.Hero, original); // +2 Attack this turn
            Core2026Proof.Check(original.AttackDamage == 8 && original.BaseHealth == 7 && original.HasTaunt,
                "enchanted source is 8/7 with Taunt");
            Play(game, player, "EX1_366");
            bool observed = false;
            void Observe(IEntity summoned)
            {
                observed = true;
                if (history) Core2026Proof.Check(new[] { "CS2_009e", "CS2_011o" }.All(id =>
                    ((Minion)summoned).AppliedEnchantments?.Count(e => e.Card.Id == id && e.Target == summoned) == 1),
                    "source enchantments attached independently before AFTER_SUMMON");
            }
            game.TriggerManager.AfterSummonTrigger += Observe;
            Minion copy;
            if (generic) copy = (Minion)Generic.Copy(player, player.Hero, original, Zone.PLAY);
            else
            {
                var stack = new TaskStack();
                new SummonCopyTask(EntityType.TARGET, addToStack: true).Process(game, player, player.Hero, original, stack);
                copy = (Minion)stack.Playables.Single();
            }
            game.TriggerManager.AfterSummonTrigger -= Observe;
            Core2026Proof.Check(observed, "copy passes through AFTER_SUMMON observer");
            game.DeathProcessingAndAuraUpdate();
            string path = generic ? "Generic.Copy" : "SummonCopyTask";
            Core2026Proof.Check(copy.AttackDamage == 9 && copy.BaseHealth == 8 && copy.HasTaunt,
                path + " enchanted copy retains subsequent Sword buff exactly once");
            if (history)
            {
                foreach (string id in new[] { "CS2_009e", "CS2_011o" })
                {
                    var sourceEnchant = original.AppliedEnchantments.Single(e => e.Card.Id == id);
                    var copyEnchant = copy.AppliedEnchantments.Single(e => e.Card.Id == id);
                    Core2026Proof.Check(sourceEnchant.Id != copyEnchant.Id && sourceEnchant.Target == original && copyEnchant.Target == copy,
                        path + " copies independent attached enchantment " + id);
                }
                Core2026Proof.Check(copy.AppliedEnchantments.Count(e => e.Card.Id == "EX1_366e") == 1,
                    path + " retains one new summon enchantment");
            }
            byte[] boundary = game.SaveSnapshot();
            foreach (bool silenceSource in new[] { false, true })
            {
                var branches = new[] { game.CloneExact(history: history), Game.LoadSnapshot(boundary) };
                var histories = new List<string>();
                foreach (var branch in branches)
                {
                    int historyStart = branch.PowerHistory?.Full.Count ?? 0;
                    var source = (Minion)branch.IdEntityDic[original.Id];
                    var copied = (Minion)branch.IdEntityDic[copy.Id];
                    (silenceSource ? source : copied).Silence();
                    branch.DeathProcessingAndAuraUpdate();
                    Core2026Proof.Check(source.AttackDamage == (silenceSource ? 4 : 8) && copied.AttackDamage == (silenceSource ? 9 : 4),
                        path + " Silence removes only selected entity's buffs");
                    End(branch);
                    Core2026Proof.Check(source.AttackDamage == (silenceSource ? 4 : 6) && source.BaseHealth == (silenceSource ? 5 : 7)
                        && copied.AttackDamage == (silenceSource ? 7 : 4) && copied.BaseHealth == (silenceSource ? 8 : 5),
                        path + " temporary enchantment expires independently after Silence and resume; got "
                        + source.AttackDamage + "/" + source.BaseHealth + ", " + copied.AttackDamage + "/" + copied.BaseHealth);
                    Core2026Proof.Check(source.HasTaunt == !silenceSource && copied.HasTaunt == silenceSource,
                        path + " expiry retains unsilenced permanent enchantment");
                    if (history) Core2026Proof.Check(!source.AppliedEnchantments.Concat(copied.AppliedEnchantments).Any(e => e.Card.Id == "CS2_011o"),
                        path + " expired enchantments detached from both entities");
                    if (history) histories.Add(string.Concat(branch.PowerHistory.Full.Skip(historyStart).Select(entry => Canonical.Value(entry))));
                }
                Core2026Proof.Check(Canonical.State(branches[0]) == Canonical.State(branches[1]), path + " clone/snapshot continuation parity");
                if (history) Core2026Proof.Check(histories[0] == histories[1],
                    path + " clone/snapshot history parity");
                Core2026Proof.Check(boundary.SequenceEqual(game.SaveSnapshot()), path + " continuation leaves original game untouched");
            }
        }
        Console.WriteLine("PASS enchanted copy initialization: both paths, independent Silence/expiry, clone/snapshot continuation, history on/off");
    }

    internal static void Run()
    {
        EnchantedCopies();
        foreach (bool history in new[] { false, true })
        foreach (bool generic in new[] { false, true })
        {
            var game = Fixture(history);
            var player = game.Player1;
            var original = Summon(player, "CS2_182"); // 4/5 Yeti
            original.AttackDamage = 7;
            original.BaseHealth = 8;
            original.Damage = 2;
            Play(game, player, "EX1_366"); // Sword of Justice
            var stack = new TaskStack();
            Minion copy;
            if (generic) copy = (Minion)Generic.Copy(player, player.Hero, original, Zone.PLAY);
            else
            {
                new SummonCopyTask(EntityType.TARGET, addToStack: true).Process(game, player, player.Hero, original, stack);
                copy = (Minion)stack.Playables.Single();
            }
            Core2026Proof.Check(copy.AttackDamage == 8 && copy.BaseHealth == 9 && copy.Damage == 2,
                $"{(generic ? "Generic.Copy" : "SummonCopyTask")} initializes copied 7/8 before Sword +1/+1, retaining damage; got {copy.AttackDamage}/{copy.BaseHealth}/{copy.Damage}");
            Core2026Proof.Check(original.AttackDamage == 7 && original.BaseHealth == 8, "summon buff must not mutate source");
            Core2026Proof.Check(player.Hero.Weapon.Durability == 4, "after-summon trigger fires exactly once");
            if (history) Core2026Proof.Check(copy.AppliedEnchantments.Count(e => e.Card.Id == "EX1_366e") == 1,
                "new summon enchantment remains attached exactly once");
        }
        foreach (bool history in new[] { false, true })
        {
            var game = Fixture(history);
            var player = game.Player1;
            var original = (Minion)Add(player, "CS2_182");
            Play(game, player, "EX1_366");
            var copy = (Minion)Generic.Copy(player, player.Hero, original, Zone.PLAY, initialize: entity => {
                var minion = (Minion)entity;
                minion.AttackDamage = 8;
                minion.BaseHealth = 8;
                minion.HasDivineShield = true;
            });
            Core2026Proof.Check(copy.AttackDamage == 9 && copy.BaseHealth == 9 && copy.HasDivineShield,
                "explicit 8/8 shielded copy initializes before Sword, as required by Searing Reflection");
            Core2026Proof.Check(original.AttackDamage == 4 && original.BaseHealth == 5 && !original.HasDivineShield,
                "copy initializer does not alter drawn source");
            while (!player.BoardZone.IsFull) Summon(player, "CS2_231");
            int entities = game.IdEntityDic.Count;
            var result = new SummonCopyTask(EntityType.TARGET).Process(game, player, player.Hero, copy);
            Core2026Proof.Check(result == TaskState.STOP && game.IdEntityDic.Count == entities,
                "full board rejects copy task before allocation or summon triggers");
        }
        Console.WriteLine("PASS copy initialization: both copy paths retain Sword of Justice summon buff and copied damage, source isolation, history on/off");
    }
}
