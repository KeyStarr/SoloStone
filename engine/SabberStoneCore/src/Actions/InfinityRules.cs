using System;
using System.Linq;
using System.Numerics;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // Infinity is an ordinary finite attribute; the client formats its numeric value.
    public static class InfinityRules
    {
        public const int Value = int.MaxValue;

        private const GameTag OperationCount = (GameTag)33900, CostBase = (GameTag)33901,
            DefinitionGeneration = (GameTag)33902;
        private const int Operations = 0x30000000, LinkKey = 18001;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 33900 && (int)tag <= 33903
            || (int)tag >= Operations && (int)tag < 0x40000000;
        internal static bool HasCost(IEntity card) => card[OperationCount] != 0;
        private static int Raw(IEntity card, GameTag tag) => card.NativeTags.TryGetValue(tag, out int value) ? value : 0;
        private static GameTag Slot(int index, int field) => (GameTag)checked(Operations + index * 3 + field);
        private static int Residence(IPlayable card) => checked((int)card.Game.HistoryFacts.Query
            .Residences(card.Id, Zone.HAND).LastOrDefault().EntrySequence);

        // Opt-in journal: ordinary cards retain the existing scalar/cache cost path.
        internal static void RecordCost(Playable card, EffectOperator operation, int value, int application = 0)
        {
            if (!HasCost(card)) return;
            int index = card[OperationCount] - 1;
            card[Slot(index, 0)] = (int)operation;
            card[Slot(index, 1)] = value;
            card[Slot(index, 2)] = application;
            card[OperationCount]++;
        }
        internal static int EvaluateCost(Playable card)
        {
            BigInteger value = CostRules.HasManaNumbers(card) ? CostRules.ManaNumbers(card) : Raw(card, CostBase);
            for (int i = 0; i < card[OperationCount] - 1; i++)
            {
                if (Raw(card, Slot(i, 2)) < 0) continue;
                int operand = Raw(card, Slot(i, 1));
                switch ((EffectOperator)card[Slot(i, 0)]) {
                    case EffectOperator.SET: value = operand; break;
                    case EffectOperator.ADD: value += operand; break;
                    case EffectOperator.SUB: value -= operand; break;
                    case EffectOperator.MUL: value *= operand; break;
                    default: throw new InvalidOperationException("Invalid permanent Cost operation.");
                }
            }
            return (int)BigInteger.Max(int.MinValue, BigInteger.Min(Value, value));
        }
        internal static void ResetCost(IPlayable card)
        {
            foreach (var tag in card.NativeTags.Keys.Where(IsInternalTag).ToArray()) card.NativeTags.Remove(tag);
        }
        internal static void ChangingDefinition(IPlayable card)
        {
            if (HasCost(card)) card[DefinitionGeneration]++;
        }
        internal static void CopyCost(IPlayable source, IPlayable copy)
        {
            if (!HasCost(source)) return;
            foreach (var tag in source.NativeTags.Where(pair => IsInternalTag(pair.Key)))
                copy.NativeTags[tag.Key] = tag.Value;
        }
        public static void MarkCost(IPlayable source)
        {
            var c = source.Controller;
            if (c.HandZone.IsEmpty) return;
            var target = (Playable)c.HandZone[c.Game.Random.Next(c.HandZone.Count)];
            c.Game.OnRandomHappened(true);
            if (!HasCost(target)) {
                target[CostBase] = CostRules.CopyableScalar(target) ?? target.Card.Cost;
                target[OperationCount] = 1;
                // Mana-based cards retain a live base; earlier permanent operations must precede the mark.
                if (CostRules.HasManaNumbers(target))
                    foreach (var operation in target._costManager?.PersistentOperations() ?? Array.Empty<(EffectOperator Operator, int Value)>())
                        RecordCost(target, operation.Operator, operation.Value);
            }
            var actor = Enchantment.GetInstance(c, source, target, Cards.FromId("END_018e"));
            RecordCost(target, EffectOperator.SET, Value, actor.Id);
            var link = PayloadRules.StoreValue(source, LinkKey, actor.Id);
            link[PayloadRules.Link] = target.Id;
            link[PayloadRules.Generation] = Residence(target);
            link[PayloadRules.Recipient] = target[DefinitionGeneration];
            PublishCost(target);
        }
        public static void RestoreCost(IEntity source)
        {
            foreach (var link in PayloadRules.Entries(source, LinkKey))
            {
                if (!source.Game.IdEntityDic.TryGetValue(link[PayloadRules.Link], out var target)
                    || target.Zone != target.Controller.HandZone || Residence(target) != link[PayloadRules.Generation]
                    || target[DefinitionGeneration] != link[PayloadRules.Recipient]) continue;
                int application = link[PayloadRules.Value];
                for (int i = 0; i < target[OperationCount] - 1; i++)
                    if (target[Slot(i, 2)] == application) {
                        target[Slot(i, 2)] = -1;
                        if (source.Game.IdEntityDic.TryGetValue(application, out var entity)
                            && entity is Enchantment actor && actor.Target == target
                            && target.AppliedEnchantments?.Contains(actor) == true) actor.Remove();
                        Enchantment.GetInstance(target.Controller, (IPlayable)source, target, Cards.FromId("END_018e2"));
                        PublishCost(target);
                    }
            }
        }
        private static void PublishCost(IPlayable target)
        {
            if (target.Game.History)
                target.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.COST, target.Cost));
        }

        public static void GrantWeapon(Weapon weapon)
        {
            var actor = WeaponSetter(weapon);
            if (actor == null)
                Enchantment.GetInstance(weapon.Controller, weapon, weapon, Cards.FromId("END_012e"), weapon.AttackDamage);
            weapon.NativeTags[GameTag.ATK] = Value;
            PublishAttack(weapon);
        }

        private static Enchantment WeaponSetter(IEntity target) => target is Weapon
            ? target.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "END_012e") : null;

        internal static void WeaponModified(IEntity target, EffectOperator operation, int value)
        {
            var actor = WeaponSetter(target);
            if (actor != null)
                actor.NativeTags[GameTag.TAG_SCRIPT_DATA_NUM_1] = AttributeArithmetic.Apply(actor.ScriptTag1, operation, value);
        }

        internal static void Removing(Enchantment actor)
        {
            if (actor.Card.Id != "END_012e" || !(actor.Target is Weapon weapon)) return;
            weapon.NativeTags[GameTag.ATK] = actor.ScriptTag1;
            PublishAttack(weapon);
        }

        private static void PublishAttack(IPlayable target)
        {
            if (target.Game.History)
                target.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.ATK, target[GameTag.ATK]));
        }
    }
}
