using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ImbueBlessings
    {
        internal static void Golem(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                int n = s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                // shortcut: base visual only; pt3/pt4 thresholds need a pinned client oracle.
                var golem = (Minion)Entity.FromCard(c, Cards.FromId("EDR_847pt2"),
                    new EntityData { [GameTag.ATK] = n, [GameTag.HEALTH] = n, [GameTag.CREATOR] = s.Id });
                golem.AttackDamage = n;
                golem.BaseHealth = n;
                Generic.SummonBlock(g, golem, -1, (IPlayable)s);
            }) };
        }

        internal static void Wolf(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var beasts = c.HandZone.Where(p => p.Card.IsRace(Race.BEAST)).ToArray();
                if (beasts.Length == 0) return;
                var target = beasts.Choose(g.Random);
                int n = s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_850pe1"), (IPlayable)s, target, n);
                Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_850pe5"), (IPlayable)s, target, n);
                g.OnRandomHappened(true);
            }) };
        }

        internal static void Wisp(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int n = s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < n && !c.BoardZone.IsFull; i++) ctx.Summon("EDR_851t");
                int damage = (n + c.Hero.HeroPowerDamage) * (int)Math.Pow(2, c.ControllerAuraEffects[GameTag.HERO_POWER_DOUBLE]);
                for (int i = 0; i < damage; i++)
                {
                    var enemies = IncludeTask.GetEntities(EntityType.ENEMIES, c, s, null, null)
                        .OfType<ICharacter>().Where(enemy => !enemy.ToBeDestroyed).ToArray();
                    if (enemies.Length == 0) break;
                    // Bonuses scale the total, not each one-damage missile.
                    var target = enemies.Choose(g.Random);
                    if (g.History) g.PowerHistory.SelectResolvedTarget(target);
                    target.TakeDamage((IPlayable)s, 1);
                    g.OnRandomHappened(true);
                }
            }) };
        }

        internal static void Wind(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion) || minion.Zone != c.BoardZone
                    || !((HeroPower)s).IsValidPlayTarget(minion)) return;
                // Lean_ imbue-10: printed Cost + current magnitude, no nearest-cost fallback.
                stack ??= new TaskStack();
                stack.Number = minion.Card.Cost + s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                var ctx = new EffectContext(g, c.Id, s.Id, minion.Id, stack);
                var chosen = ctx.PickRandomCard(EmeraldDreamPools.Wind(), PoolCostBinding.StackNumber, 114059);
                if (chosen == null) return;
                ctx.RunLegacy(new TransformTask(chosen.Id, EntityType.TARGET, clearNativeKeywords: true));
                // Wind creates a new body: old life/turn/ability state must not survive the transform.
                foreach (var tag in new[] { GameTag.REBORN, GameTag.HAS_BEEN_REBORN, GameTag.SILENCED,
                    GameTag.DEATHRATTLE, GameTag.BATTLECRY, GameTag.INSPIRE, GameTag.SPELLPOWER, GameTag.ENRAGED,
                    GameTag.IMMUNE, GameTag.FROZEN, GameTag.NUM_ATTACKS_THIS_TURN, GameTag.NUM_TURNS_IN_PLAY,
                    GameTag.CONTROLLER_CHANGED_THIS_TURN, GameTag.DORMANT_AWAKENED_THIS_TURN,
                    GameTag.CANT_ATTACK, GameTag.CANNOT_ATTACK_HEROES, GameTag.TAG_SCRIPT_DATA_NUM_1,
                    GameTag.TAG_SCRIPT_DATA_NUM_2, GameTag.LAST_AFFECTED_BY, GameTag.PREDAMAGE })
                {
                    int before = minion.GetNativeGameTag(tag);
                    if (minion._data.Remove(tag) && g.History && minion.GetNativeGameTag(tag) != before)
                        g.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(
                            minion.Id, tag, minion.GetNativeGameTag(tag)));
                }
                minion.IsImmune = false;
                // Transform readiness belongs to the new card, never the previous Rush body.
                minion.AttackableByRush = false;
                g.RushMinions.RemoveAll(id => id == minion.Id);
                minion.IsExhausted = c != g.CurrentPlayer || !minion.HasCharge && !minion.IsRush;
                if (c == g.CurrentPlayer && minion.IsRush && !minion.HasCharge)
                {
                    minion.AttackableByRush = true;
                    g.RushMinions.Add(minion.Id);
                }
                g.OnRandomHappened(true);
            }) };
        }

        internal static void AttackBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } };
        }

        internal static void Discount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(0)) { UseScriptTag = true } };
        }
    }
}
