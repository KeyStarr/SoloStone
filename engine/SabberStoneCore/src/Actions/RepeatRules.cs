using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Player-play qualification and durable repeat/payload state; completion owns execution.</summary>
    public static class RepeatRules
    {
        internal const GameTag Origin = (GameTag)32109, SchoolMask = (GameTag)32110, TargetWitness = (GameTag)32111;
        internal const GameTag Remaining = (GameTag)32100, PrimaryDone = (GameTag)32101,
            TargetDefinition = (GameTag)32102, ChooseBoth = (GameTag)32108, Payload = (GameTag)32103, Charge = (GameTag)32104;
        public const GameTag OneCost = (GameTag)32105, OtherClass = (GameTag)32106;
        public const GameTag PlayedCost = (GameTag)32107;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 32100 && (int)tag <= 32121;

        private const GameTag PowerSource = (GameTag)32112, PowerRemaining = (GameTag)32113,
            PowerTarget = (GameTag)32114, PowerTargetDefinition = (GameTag)32115, PowerChoice = (GameTag)32116,
            PowerChooseBoth = (GameTag)32117, PowerTargetWitness = (GameTag)32118;

        public static bool DoublesHeroPower(Controller owner) => owner.BoardZone.Any(m => m.Card.Id == "TIME_064"
            && !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed);

        public static bool DoublesEndEffect(IPlayable source) => DoublesHeroPower(source.Controller)
            && (!(source is HeroPower power) || !power.IsPassiveHeroPower && !ImbueRules.IsPassivePower(power.Card));

        internal static readonly Tasks.SimpleTasks.CustomTask EndEffectBoundary = new Tasks.SimpleTasks.CustomTask(
            (game, controller, source, target, stack) => game.DeathProcessingAndAuraUpdate());

        internal static void BeginHeroPower(HeroPower power, ICharacter target, int chooseOne)
        {
            var owner = power.Controller;
            owner[PowerSource] = power.Id; owner[PowerRemaining] = 2;
            owner[PowerTarget] = target?.Id ?? 0; owner[PowerTargetDefinition] = target?.Card.AssetId ?? 0;
            owner[PowerChoice] = chooseOne; owner[PowerChooseBoth] = owner.ChooseBoth ? 1 : 0;
            if (target != null) target[PowerTargetWitness] = power.Id;
            ContinueHeroPower(owner);
        }

        internal static void ContinueHeroPower(Controller owner)
        {
            if (owner[PowerSource] == 0) owner = owner.Opponent;
            if (owner[PowerSource] == 0 || Generic.SpellBodyPending(owner.Game)) return;
            var game = owner.Game;
            var power = (HeroPower)game.IdEntityDic[owner[PowerSource]];
            while (owner[PowerRemaining] > 0 && game.State != State.COMPLETE)
            {
                owner[PowerRemaining]--;
                ICharacter target = null;
                if (game.IdEntityDic.TryGetValue(owner[PowerTarget], out var entity) && entity is ICharacter character
                    && entity.Card.AssetId == owner[PowerTargetDefinition] && entity[PowerTargetWitness] == power.Id
                    && (character == character.Controller.Hero || character.Zone == character.Controller.BoardZone)
                    && power.IsValidPlayTarget(character)) target = character;
                game.TaskQueue.StartEvent();
                if (power.ChooseOne && owner[PowerChooseBoth] != 0)
                    foreach (var option in power.ChooseOnePlayables) option.ActivateTask(PowerActivation.POWER, target, owner[PowerChoice], power);
                else if (power.ChooseOne && owner[PowerChoice] > 0)
                    power.ChooseOnePlayables[owner[PowerChoice]-1].ActivateTask(PowerActivation.POWER, target, owner[PowerChoice], power);
                else power.ActivateTask(PowerActivation.POWER, target, owner[PowerChoice]);
                game.ProcessTasks();
                game.TaskQueue.EndEvent();
                game.DeathProcessingAndAuraUpdate();
                if (Generic.SpellBodyPending(game)) return;
            }
            if (game.IdEntityDic.TryGetValue(owner[PowerTarget], out var witness) && witness[PowerTargetWitness] == power.Id)
                witness.NativeTags.Remove(PowerTargetWitness);
            foreach (var tag in new[] { PowerSource, PowerRemaining, PowerTarget, PowerTargetDefinition, PowerChoice, PowerChooseBoth }) owner.NativeTags.Remove(tag);
            power.IsExhausted = true;
            owner.HeroPowerActivationsThisTurn++;
            owner.NumTimesHeroPowerUsedThisGame++;
            game.TaskQueue.StartEvent();
            game.TriggerManager.OnInspireTrigger(power);
            game.ProcessTasks();
            game.TaskQueue.EndEvent();
            game.DeathProcessingAndAuraUpdate();
            BasicHeroPowerRules.AfterUse(power);
            game.CurrentEventData = null;
        }

        // Definition-only metadata, like ColossalRules; never contains games or entity instances.
        private static readonly Dictionary<int, GameTag> Sources = new Dictionary<int, GameTag>();
        public static void Declare(Card card, GameTag kind) => Sources[card.AssetId] = kind;
        private static bool HasSource(Controller controller, GameTag kind) => controller.BoardZone.Any(m =>
            !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed && Sources.TryGetValue(m.Card.AssetId,out var declared) && declared == kind);

        public static int QualifiedSource(IPlayable card, int cost)
        {
            if (!(card is Spell)) return 0;
            bool other = card.Card.Class != CardClass.NEUTRAL && !CardClassRules.IsAllowedInDeck(card.Card,card.Controller.HeroClass);
            return card.Controller.BoardZone.FirstOrDefault(m => !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed &&
                (m.Card.Id == "TIME_852t1" && card.Card[GameTag.SPELL_SCHOOL] == 1
                    && card.Controller.BoardZone.Any(dragon => dragon != m && dragon.Card.IsRace(Race.DRAGON) && !dragon.IsDormant && !dragon.ToBeDestroyed)
                || Sources.TryGetValue(m.Card.AssetId,out var kind) && (kind == OneCost && cost == 1 || kind == OtherClass && other)))?.Id ?? 0;
        }

        public static void Grant(Controller controller, IPlayable source, string enchantment, int count = 1, int schools = 0)
        {
            var entry = Enchantment.GetInstance(controller, source, controller, Cards.FromId(enchantment), count);
            entry[SchoolMask] = schools;
            entry[Charge] = 1;
        }

        public static void Mark(IPlayable spell, IPlayable source, string enchantment)
        {
            var entry = Enchantment.GetInstance(spell.Controller, source, spell, Cards.FromId(enchantment));
            entry[Charge] = 1;
        }

        internal static void RevealMarks(Spell spell)
        {
            if (!spell.Game.History || spell.IsSecret) return;
            foreach (var entry in spell.AppliedEnchantments?.Where(e => e[Charge] != 0 || e[Payload] != 0) ?? Enumerable.Empty<Enchantment>())
                spell.Game.PowerHistory.Add(new PowerHistoryShowEntity { Entity = PowerHistoryBuilder.FullEntity(entry).Entity });
        }

        public static void GrantPayload(Controller controller, IPlayable source, string enchantment)
        {
            var entry = Enchantment.GetInstance(controller, source, controller, Cards.FromId(enchantment));
            entry[Payload] = 1;
        }

        internal static void Capture(IPlayable source, int cost, int origin)
        {
            if (source is Minion && HasSource(source.Controller,OneCost)) source[PlayedCost] = cost;
            if (!(source is Spell spell)) return;
            var marked = spell.AppliedEnchantments?.FirstOrDefault(e => e[Charge] != 0);
            if (origin != 0 || marked != null)
            {
                spell[Remaining] = 1;
                spell[Origin] = marked?.Id ?? origin;
            }
            int school = spell.Card[GameTag.SPELL_SCHOOL];
            foreach (var entry in source.Controller.AppliedEnchantments?.OrderBy(e => e.OrderOfPlay).ToArray() ?? new Enchantment[0])
            {
                if (entry[Charge] != 0)
                {
                    int schools = entry[SchoolMask];
                    if (schools != 0 && (school <= 0 || (schools & (1 << school)) == 0)) continue;
                    spell[Remaining] = 1;
                    spell[Origin] = entry.Id;
                    if (schools != 0)
                    {
                        entry[SchoolMask] = schools & ~(1 << school);
                        if (entry[SchoolMask] == 0) entry.Remove();
                    }
                    else if (--entry[GameTag.TAG_SCRIPT_DATA_NUM_1] <= 0) entry.Remove();
                }
                else if (entry[Payload] != 0 && school == 6)
                {
                    var captured = Enchantment.GetInstance(source.Controller, entry.Creator, spell, entry.Card);
                    captured[Payload] = 1;
                    entry.Remove();
                }
            }
        }

        internal static bool IsRepeating(IEntity source) => source.Game.SpellCompletion?.SourceId == source.Id && source[PrimaryDone] != 0;

        internal static ICharacter Target(Spell spell, int id, int chooseOne)
        {
            if (id == 0 || !spell.Game.IdEntityDic.TryGetValue(id, out var entity) || !(entity is ICharacter target) ||
                entity.Card.AssetId != spell[TargetDefinition] || entity[TargetWitness] != spell.Id ||
                !(target == target.Controller.Hero || target.Zone == target.Controller.BoardZone)) return null;
            var branch = chooseOne > 0 ? spell.ChooseOnePlayables[chooseOne - 1] : spell;
            return branch.IsValidPlayTarget(target) ? target : null;
        }

        internal static void RunPayloads(Spell spell)
        {
            foreach (var entry in spell.AppliedEnchantments?.Where(e => e[Payload] != 0).OrderBy(e => e.OrderOfPlay).ToArray() ?? new Enchantment[0])
            {
                spell.Game.DeathProcessingAndAuraUpdate();
                if (spell.Game.State == State.COMPLETE) break;
                entry.Remove();
                if (spell.Game.History) spell.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER,entry.Id,"",0,0));
                spell.Game.TaskQueue.StartEvent();
                entry.Card.Power.PowerTask.Process(spell.Game, spell.Controller, spell, null);
                spell.Game.ProcessTasks();
                spell.Game.TaskQueue.EndEvent();
                if (spell.Game.History) spell.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                spell.Game.DeathProcessingAndAuraUpdate();
            }
        }

        internal static void Clear(Spell spell)
        {
            int targetId = spell.Game.SpellCompletion?.SourceId == spell.Id ? spell.Game.SpellCompletion.TargetId : 0;
            if (targetId != 0 && spell.Game.IdEntityDic.TryGetValue(targetId,out var target) && target[TargetWitness] == spell.Id)
                target[TargetWitness] = 0;
            spell[Remaining] = spell[PrimaryDone] = spell[TargetDefinition] = spell[ChooseBoth] = spell[Origin] = 0;
            foreach (var entry in spell.AppliedEnchantments?.Where(e => e[Payload] != 0).ToArray() ?? new Enchantment[0]) entry.Remove();
        }
    }
}
