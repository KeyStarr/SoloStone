using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;

internal static partial class HistoryShapeProof
{
    internal static void B14Presentation()
    {
        var failures = new List<string>();
        void Proof(string name, Action test)
        {
            try { test(); Console.WriteLine("PASS b14 presentation " + name); }
            catch (InvalidOperationException error) { failures.Add(name + ": " + error.Message); }
        }
        void Clean(Fixture f, string rule)
        {
            f.Flush(); f.Session.History.Finish();
            Check(!f.Session.History.Findings.Any(v => v.Rule == rule), rule + ": "
                + string.Join(';', f.Session.History.Findings.Where(v => v.Rule == rule).Select(v => v.Card + "/" + v.Detail)));
        }
        foreach (string card in new[] { "CATA_488", "CATA_475", "EDR_971" })
            Proof("doubled-end-" + card, () => {
                var f = new Fixture(); var source = f.Summon(f.Player, card);
                var friendly = card == "CATA_488" ? f.Summon(f.Player, "CS2_200") : null;
                var victim = f.Summon(f.Player.Opponent, "CS2_200"); victim.BaseHealth = 30;
                if (friendly != null) friendly.BaseHealth = 30;
                f.Player.Hero.Damage = 10; f.Player.Opponent.Hero.Damage = 10;
                f.Player.ExtraEndTurnEffect = true; f.Ready("b14-end-" + card);
                int nativeEffects = f.Session.Client.EntryCounts.GetValueOrDefault("SUB_SPELL_START");
                // Skip setup history: Vulcanos' appendages enter inside its own TRIGGER.
                int setup = f.Game.PowerHistory.Full.Count;
                f.Act(EndTurnTask.Any(f.Player));
                Check(f.Game.PowerHistory.Full.Skip(setup).OfType<PowerHistoryBlockStart>()
                    .Count(b => b.Source == source.Id && b.BlockType == BlockType.TRIGGER) == 2, "two full end-turn invocations");
                if (card == "CATA_488")
                {
                    Check(source.Damage == 0 && friendly.Damage == 6 && victim.Damage == 6,
                        "Vulcanos damages both boards twice and excludes itself");
                    Check(f.Game.PowerHistory.Full.Skip(setup).OfType<PowerHistorySubSpellStart>()
                        .Count(s => s.Source == source.Id && s.Targets.Count == 0) == 2,
                        "both Vulcanos damage invocations present one public area effect");
                    Check(f.Session.Client.EntryCounts.GetValueOrDefault("SUB_SPELL_START") == nativeEffects + 2,
                        "both Vulcanos area effects survive native wire projection");
                }
                Clean(f, "SPLIT_EFFECT_BLOCK");
            });
        Proof("Vulcanos-no-hit", () => {
            var f = new Fixture(); var source = f.Summon(f.Player, "CATA_488");
            foreach (var plume in ColossalRules.AppendagesOf(source).ToArray()) plume.Destroy();
            f.Game.DeathProcessingAndAuraUpdate(); f.Ready("vulcanos-no-hit");
            int setup = f.Game.PowerHistory.Full.Count;
            f.Act(EndTurnTask.Any(f.Player));
            Check(!f.Game.PowerHistory.Full.Skip(setup).OfType<PowerHistorySubSpellStart>()
                .Any(s => s.Source == source.Id), "empty Vulcanos damage has no area effect cue");
            Clean(f, "SPLIT_EFFECT_BLOCK");
        });
        foreach (string variant in new[] { "immune", "silenced" })
            Proof("Vulcanos-" + variant, () => {
                var f = new Fixture(); var source = f.Summon(f.Player, "CATA_488");
                foreach (var plume in ColossalRules.AppendagesOf(source).ToArray()) plume.Destroy();
                f.Game.DeathProcessingAndAuraUpdate();
                var victim = f.Summon(f.Player.Opponent, "CS2_200");
                if (variant == "immune") victim.IsImmune = true;
                else source.Silence();
                f.Ready("vulcanos-" + variant);
                int setup = f.Game.PowerHistory.Full.Count;
                f.Act(EndTurnTask.Any(f.Player));
                Check(victim.Damage == 0 && !f.Game.PowerHistory.Full.Skip(setup).OfType<PowerHistorySubSpellStart>()
                    .Any(s => s.Source == source.Id), "prevented Vulcanos damage has no area effect cue");
                Clean(f, "SPLIT_EFFECT_BLOCK");
            });
        foreach (string card in new[] { "CORE_CS2_042", "CORE_TRL_240", "CORE_ULD_191", "CORE_UNG_848" })
            Proof("doubled-battlecry-" + card, () => {
                var f = new Fixture(); var victim = f.Summon(card == "CORE_ULD_191" ? f.Player : f.Player.Opponent, "CS2_200");
                victim.BaseHealth = 30; f.Player.Hero.AttackDamage = 2; f.Player.ExtraBattlecry = true;
                var source = Generic.DrawCard(f.Player, Cards.FromId(card)); source[GameTag.COST] = 0;
                f.Ready("b14-battlecry-" + card);
                f.Act(PlayCardTask.MinionTarget(f.Player, source, card == "CORE_UNG_848" ? null : victim));
                Check(f.Game.PowerHistory.Full.OfType<PowerHistoryBlockStart>()
                    .Count(b => b.Source == source.Id && b.BlockType == BlockType.POWER) == 2, "two full Battlecry invocations");
                Clean(f, "SPLIT_EFFECT_BLOCK");
            });
        Proof("restored-end-trigger", () => {
            var original = new Fixture(); original.Summon(original.Player, "CATA_488");
            var victim = original.Summon(original.Player.Opponent, "CS2_200"); victim.BaseHealth = 30;
            original.Player.ExtraEndTurnEffect = true;
            original.Game.DeathProcessingAndAuraUpdate();
            var f = new Fixture(restored: SabberStoneCore.Model.Game.LoadSnapshot(original.Game.SaveSnapshot()));
            f.Ready("b14-restored-end"); f.Act(EndTurnTask.Any(f.Player));
            Check(((Minion)f.Game.IdEntityDic[victim.Id]).Damage == 6, "restored Vulcanos executes both complete end-turn effects");
            Clean(f, "SPLIT_EFFECT_BLOCK");
        });
        foreach (string card in new[] { "CORE_ONY_018", "EDR_490" })
            Proof("forwarded-options-" + card, () => {
                var f = new Fixture(CardClass.DRUID); var victim = f.Summon(f.Player.Opponent, "CS2_200");
                f.Player.ChooseBoth = true; f.Player.Hero.Damage = 10;
                var source = Generic.DrawCard(f.Player, Cards.FromId(card)); source[GameTag.COST] = 0;
                f.Ready("b14-forwarded-options"); f.Act(PlayCardTask.Any(f.Player, source, victim));
                Check(card == "CORE_ONY_018" ? victim.Damage == 4 && f.Player.Hero.Damage == 2
                    : victim.Zone == f.Player.Opponent.GraveyardZone && f.Player.BoardZone.Count(m => m.Card.Id == "EDR_490t") == 2,
                    "both complete Choose One option bodies resolved through parent source");
                Clean(f, "SPLIT_EFFECT_BLOCK");
            });
        Proof("separate-attack-reactions", () => {
            var f = new Fixture(CardClass.DEMONHUNTER); var victim = f.Summon(f.Player.Opponent, "CORE_BT_510"); victim.BaseHealth = 30;
            var source = Generic.DrawCard(f.Player, Cards.FromId("CORE_BT_120")); source[GameTag.COST] = 0;
            f.Ready("b14-reactions"); f.Act(PlayCardTask.MinionTarget(f.Player, source, victim));
            Check(f.Player.Hero.Damage == 3, "three separate Wrathspike reactions resolved");
            Clean(f, "SPLIT_EFFECT_BLOCK");
        });
        Proof("separate-fast-triggers", () => {
            var f = new Fixture(); var source = f.Summon(f.Player, "CATA_475"); f.Ready("b14-fast-triggers");
            for (int i = 0; i < 2; i++)
                f.Game.TaskQueue.Execute(source.Power.Trigger.SingleTask, f.Player, source, null, historyTrigger: true);
            Check(f.Player.Opponent.Hero.Damage == 4, "two fast trigger invocations resolved");
            Clean(f, "SPLIT_EFFECT_BLOCK");
        });
        Proof("doubled-printed-deathrattle", () => {
            var f = new Fixture(); var source = f.Summon(f.Player, "JAIL_007");
            f.Player[GameTag.EXTRA_DEATHRATTLES_BASE] = 1; f.Ready("b14-deathrattle");
            source.Destroy(); f.Game.DeathProcessingAndAuraUpdate();
            Check(f.Player.Opponent.Hero.Damage == 4, "two full Sewer Imp Deathrattles resolved");
            Clean(f, "SPLIT_EFFECT_BLOCK");
        });
        Proof("doubled-granted-deathrattle", () => {
            var f = new Fixture(); var source = f.Summon(f.Player, "CATA_612");
            Generic.AddEnchantmentBlock(f.Game, Cards.FromId("DINO_429e"), source, source);
            f.Player[GameTag.EXTRA_DEATHRATTLES_BASE] = 1; f.Ready("b14-granted");
            new ActivateDeathrattleTask(EntityType.SOURCE).Process(f.Game, f.Player, source, null, new TaskStack());
            f.Game.ProcessTasks(); Clean(f, "SPLIT_EFFECT_BLOCK");
        });
        Proof("mana-hand-stats", () => {
            var f = new Fixture(); f.Player.UsedMana = 8;
            var source = (Minion)Generic.DrawCard(f.Player, Cards.FromId("JAIL_501"));
            Generic.AddEnchantmentBlock(f.Game, Cards.FromId("ULD_191e"), source, source);
            f.Ready("b14-mana-hand-stats");
            int cursor = f.Game.PowerHistory.Full.Count;
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, f.Player.Hero.Id, "", 0, 0));
            f.Player.UsedMana = 6; f.Game.RefreshHistoryDisplays();
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            Check(source.AttackDamage == 4 && source.BaseHealth == 6, "Picklock stats follow remaining mana");
            var animation = f.Game.PowerHistory.Full.Skip(cursor).OfType<PowerHistorySubSpellStart>().Single();
            Check(animation.Source == f.Player.Hero.Id && animation.Targets.SequenceEqual(new[] { source.Id }),
                "mana-driven hand buff explicitly targets Picklock through the owner hero");
            cursor = f.Game.PowerHistory.Full.Count;
            f.Player.UsedMana = 7; f.Game.RefreshHistoryDisplays(); f.Game.RefreshHistoryDisplays();
            Check(source.AttackDamage == 3 && source.BaseHealth == 5
                && !f.Game.PowerHistory.Full.Skip(cursor).OfType<PowerHistorySubSpellStart>().Any(),
                "mana decreases and unchanged refreshes do not animate positive hand buffs");
            Clean(f, "HAND_BUFF_FX");
        });
        Proof("queued-subtasks-still-split", () => {
            var f = new Fixture(); var source = f.Summon(f.Player, "CORE_CS2_042");
            var victim = f.Summon(f.Player.Opponent, "CS2_200"); victim.BaseHealth = 30; f.Ready("b14-queued-split");
            source.ActivateTask(PowerActivation.POWER, victim);
            for (int i = 0; i < 2; i++)
                f.Game.TaskQueue.Enqueue(new DamageTask(1, EntityType.TARGET), f.Player, source, victim);
            f.Game.ProcessTasks(); f.Flush(); f.Session.History.Finish();
            Check(f.Session.History.Findings.Count(v => v.Rule == "SPLIT_EFFECT_BLOCK") == 2,
                "queued per-hit subtasks do not acquire distinct registered invocation identities");
        });
        Proof("converted-hero-lifesteal", () => {
            var f = new Fixture(CardClass.WARLOCK); f.Player.Hero.Damage = 10;
            f.Player.Hero.HasLifeSteal = true; f.Player.RestoreToDamage = true;
            f.Ready("b14-converted-lifesteal");
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, f.Player.Hero.Id, "", 0, 0));
            new DamageTask(2, EntityType.OP_HERO).Process(f.Game, f.Player, f.Player.Hero, null);
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            Check(f.Player.Hero.Damage == 12, "hero Lifesteal converted to real self-damage");
            Clean(f, "FATIGUE_BLOCK");
        });
        Proof("nested-draw-still-fatigue", () => {
            var f = new Fixture(CardClass.WARLOCK); f.Player.Hero.Damage = 10;
            f.Player.Hero.HasLifeSteal = true; f.Player.RestoreToDamage = true;
            foreach (var card in f.Player.DeckZone.ToArray()) f.Player.SetasideZone.Add(f.Player.DeckZone.Remove(card));
            bool drawn = false;
            f.Player.Hero.TakeDamageTrigger += _ => { if (!drawn) { drawn = true; Generic.Draw(f.Player); } };
            f.Ready("b14-nested-fatigue");
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, f.Player.Hero.Id, "", 0, 0));
            new DamageTask(2, EntityType.OP_HERO).Process(f.Game, f.Player, f.Player.Hero, null);
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            Check(drawn, "converted self-damage really caused an empty-deck draw");
            f.Game.PowerHistory.Last.OfType<PowerHistoryBlockStart>().Single(b => b.BlockType == BlockType.FATIGUE).BlockType = BlockType.POWER;
            f.Flush(); f.Session.History.Finish();
            Check(f.Session.History.Findings.Count(v => v.Rule == "FATIGUE_BLOCK") == 2,
                "a nested real draw cannot borrow the outer healing conversion exemption");
        });
        Proof("converted-heal-counter-corruption", () => {
            var f = new Fixture(CardClass.WARLOCK); f.Player.Hero.Damage = 10;
            f.Player.Hero.HasLifeSteal = true; f.Player.RestoreToDamage = true;
            // Recreate the old write inside converted TakeDamage, before resolved-hit provenance is recorded.
            f.Player.Hero.PreDamageTrigger += _ => f.Player.Hero.Fatigue = 2;
            f.Ready("b14-converted-counter-corruption");
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, f.Player.Hero.Id, "", 0, 0));
            new DamageTask(2, EntityType.OP_HERO).Process(f.Game, f.Player, f.Player.Hero, null);
            f.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            f.Flush(); f.Session.History.Finish();
            Check(f.Session.History.Findings.Any(v => v.Rule == "FATIGUE_BLOCK"),
                "converted healing cannot exempt an erroneous FATIGUE mutation");
        });
        Check(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
