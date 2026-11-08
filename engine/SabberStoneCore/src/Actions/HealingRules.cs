using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;

namespace SabberStoneCore.Actions
{
    public static class HealingRules
    {
        private const GameTag LifestealDepth = (GameTag)31280;
        private const GameTag LifestealTotal = (GameTag)31281;
        private const GameTag ConvertedHeal = (GameTag)31282;
        private const GameTag MinionHealingTurn = (GameTag)31283, MinionHealingAmount = (GameTag)31284, RestorationTurn = (GameTag)31285, RestorationAmount = (GameTag)31286;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31280 && (int)tag <= 31289;
        public static int MinionHealingThisTurn(Controller owner) => owner[MinionHealingTurn]==owner.Game.Turn+1 ? owner[MinionHealingAmount] : 0;
        internal static void RecordMinionHealing(Controller owner,int amount)
        {
            int previous=MinionHealingThisTurn(owner);
            owner[MinionHealingTurn]=owner.Game.Turn+1;
            owner[MinionHealingAmount]=DamageArithmetic.Add(previous,amount);
        }
        public static bool RestoredHealthThisTurn(Controller owner) => owner[RestorationTurn]==owner.Game.Turn+1 && owner[RestorationAmount]>0;
        private static void RecordHealing(IPlayable source,ICharacter target,int amount)
        {
            if(amount<=0) return;
            var owner=source.Controller;
            if(target is Minion) RecordMinionHealing(owner,amount);
            int previous=RestoredHealthThisTurn(owner)?owner[RestorationAmount]:0;
            owner[RestorationTurn]=owner.Game.Turn+1;
            owner[RestorationAmount]=DamageArithmetic.Add(previous,amount);
        }
        public static bool ReplacesHealthLoss(Controller owner) => owner.Game.CurrentPlayer == owner
            && owner.BoardZone.Any(m => m.Card.Id == "CATA_155" && !m.IsSilenced && !m.Untouchable && !m.ToBeDestroyed);

        internal static bool ReplaceHealthLoss(Hero hero, int amount)
        {
            if (hero == null || amount <= 0 || !ReplacesHealthLoss(hero.Controller)) return false;
            var source = hero.Controller.BoardZone.First(m => m.Card.Id == "CATA_155" && !m.IsSilenced && !m.Untouchable && !m.ToBeDestroyed);
            Generic.AddEnchantmentBlock(hero.Game, Cards.FromId("CATA_155e"), source, hero, amount);
            return true;
        }

        public static int Bonus(Controller owner) => owner[GameTag.OUTGOING_HEALING_ADJUSTMENT]
            + 2 * (owner.AppliedEnchantments?.Count(e => e.Card.Id == "CATA_216e") ?? 0);

        public static void Heal(IPlayable source, IEnumerable<ICharacter> targets, int amount, bool full = false,
            Controller modifiers = null, bool alreadyScaled = false)
        {
            var recipients = targets.Where(t => t != null).ToArray();
            if (recipients.Length == 0) return;
            var owner = modifiers ?? source.Controller;
            var grants = owner.AppliedEnchantments?.Where(e => e.Card.Id == "CATA_301e").ToArray()
                ?? Array.Empty<Enchantment>();
            bool converted = owner.RestoreToDamage || grants.Length > 0;
            // Snapshot and consume before any recipient reactions; prevented/full-health attempts still consume.
            foreach (var grant in grants) grant.Remove();
            foreach (var target in recipients)
            {
                int heal = alreadyScaled ? amount : Amount(source, full ? target.Damage : amount, owner);
                if (converted)
                {
                    if (source[ConvertedHeal] != 0) continue;
                    source[ConvertedHeal] = 1;
                    try { target.TakeDamage(source, heal); }
                    finally { source.NativeTags.Remove(ConvertedHeal); }
                    continue;
                }
                var game = source.Game;
                if (game.TriggerManager.Events.HasSubscribers<HealIntentEvent>())
                {
                    game.TaskQueue.StartEvent();
                    var previous = game.CurrentEventData;
                    try
                    {
                        game.CurrentEventData = new EventMetaData(source, target, heal);
                        game.TriggerManager.Events.Raise(new HealIntentEvent(target));
                        game.ProcessTasks();
                    }
                    finally { game.CurrentEventData = previous; game.TaskQueue.EndEvent(); }
                    if (game.State == State.COMPLETE || target is Hero h && h.ToBeDestroyed
                        || target is Minion m && m.Zone.Type != Zone.PLAY) continue;
                }
                if (SpellDamageRules.PreventsHealing(target)) continue;
                RecordHealing(source,target,Math.Min(target.Damage,heal));
                ((Character)target).ApplyHeal(source, heal);
            }
        }

        public static int Amount(IPlayable source, int amount, Controller modifiers = null)
        {
            var owner = modifiers ?? source.Controller;
            int doubles = owner.ControllerAuraEffects[GameTag.SPELL_HEALING_DOUBLE];
            if (source is HeroPower && owner.Hero.Weapon?.Card.Id == "TIME_890t") doubles--;
            int value = DamageArithmetic.Add(amount, Bonus(owner));
            if ((source is Spell || source is HeroPower) && doubles > 0) value = DamageArithmetic.Double(value, doubles);
            if (owner.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE] > 0)
                value = DamageArithmetic.Double(value, owner.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE]);
            return Math.Max(0, value);
        }

        public static void Split(IPlayable source, int amount)
        {
            int budget = Amount(source, amount);
            source.Game.TaskQueue.StartEvent();
            try
            {
                for (int i = 0; i < budget && source.Game.State != State.COMPLETE; i++)
                {
                    var owner = source.Controller;
                    var targets = new ICharacter[] { owner.Hero }.Concat(owner.BoardZone.GetAll())
                        .Where(c => !c.IsDead && c.Damage > 0).ToArray();
                    if (targets.Length == 0) break;
                    var target = targets[targets.Length == 1 ? 0 : source.Game.Random.Next(targets.Length)];
                    if (targets.Length > 1) source.Game.OnRandomHappened(true);
                    Heal(source, new[] { target }, 1, alreadyScaled: true);
                    source.Game.ProcessTasks();
                }
            }
            finally { source.Game.TaskQueue.EndEvent(); }
        }

        internal static bool IsConvertedHeal(IPlayable source) => source[ConvertedHeal] != 0;
        internal static bool BeginDamage(IPlayable source)
        {
            if (source?.HasLifeSteal != true || IsConvertedHeal(source)) return false;
            source[LifestealDepth]++; return true;
        }
        internal static void EndDamage(IPlayable source)
        {
            if (source == null || source[LifestealDepth] == 0 || --source[LifestealDepth] != 0) return;
            int amount = source[LifestealTotal];
            source.NativeTags.Remove(LifestealDepth); source.NativeTags.Remove(LifestealTotal);
            if (amount > 0) Lifesteal(source, amount);
        }
        internal static void Lifesteal(IPlayable source, int amount, bool both = false)
        {
            if (amount <= 0 || IsConvertedHeal(source)) return;
            if (source[LifestealDepth] > 0) { source[LifestealTotal] = DamageArithmetic.Add(source[LifestealTotal], amount); return; }
            var game = source.Game;
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER,
                source.Id, source.Card.Id, -1, 0, triggerKeyword: (int)GameTag.LIFESTEAL));
            try
            {
                Heal(source, new[] { source.Controller.Hero }, amount);
                if (both) Heal(source, new[] { source.Controller.Opponent.Hero }, amount,
                    modifiers: source.Controller.Opponent);
                foreach (var hero in both ? new[] { source.Controller.Hero, source.Controller.Opponent.Hero }
                    : new[] { source.Controller.Hero })
                    if (hero.ToBeDestroyed && hero.Health > 0) hero.ToBeDestroyed = false;
            }
            finally { if (game.History) game.PowerHistory.Add(new PowerHistoryBlockEnd()); }
        }
    }
}
