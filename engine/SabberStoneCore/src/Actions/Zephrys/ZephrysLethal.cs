using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>
    /// Can the player kill the opponent this turn with what is on the board? Zephrys's view: the Basic or Upgraded hero power,
    /// attacks by minions and the hero, Taunts cleared by attacks first. The attacks are played on copies, so Divine Shield,
    /// Poisonous, Lifesteal, Windfury and Deathrattles behave as the rules say.
    /// </summary>
    internal static class ZephrysLethal
    {
        private const int NodeBudget = 64;
        private const int MaxPowerReach = 3;

        private static readonly Lazy<HashSet<string>> BasicPowers = new Lazy<HashSet<string>>(() => {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (CardClass c in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST,
                         CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR, CardClass.DEMONHUNTER })
            {
                try { names.Add(Cards.FromId(BasicHeroPowerRules.PowerId(c)).Name); }
                catch (KeyNotFoundException) { }
            }
            foreach (var card in Cards.All.Where(card => card.Id.StartsWith("AT_132_", StringComparison.Ordinal)
                         && card.Type == CardType.HERO_POWER))
                names.Add(card.Name);
            return names;
        });

        /// <summary>Basic and Upgraded hero powers, matched by name so hero skins count too.</summary>
        internal static bool IsBasicPower(Card card) => BasicPowers.Value.Contains(card.Name);

        /// <summary>True if the opponent dies this turn. The game passed in is consumed.</summary>
        internal static bool Can(Game game, int controllerId)
        {
            var me = game.ControllerById(controllerId);
            if (me.Opponent.Hero.IsImmune) return false;
            if (Dead(me)) return true;
            int budget = NodeBudget;
            var power = me.Hero.HeroPower;
            bool basic = power != null && IsBasicPower(power.Card);
            // No copy is worth making when every attack plus the most a Basic power adds (3: Ballista Shot; a dagger or
            // claws add at most 2 attack) cannot reach the hero.
            if (UpperBound(me) + (basic ? MaxPowerReach : 0) < me.Opponent.Hero.Health + me.Opponent.Hero.Armor) return false;
            if (basic)
            {
                var withPower = game.CloneForSimulation(game.Seed ^ 0x5A5A);
                if (UsePower(withPower, controllerId) && Attack(withPower, controllerId, ref budget)) return true;
            }
            return Attack(game, controllerId, ref budget);
        }

        private static bool Dead(Controller me) => me.Opponent.Hero.IsDead || me.Opponent.PlayState == PlayState.LOST
            || me.Opponent.PlayState == PlayState.CONCEDED;

        private static bool UsePower(Game game, int controllerId)
        {
            var me = game.ControllerById(controllerId);
            var tasks = me.Options().OfType<HeroPowerTask>().ToList();
            if (tasks.Count == 0) return false;
            var task = tasks.FirstOrDefault(t => t.Target?.Id == me.Opponent.Hero.Id) ?? tasks.FirstOrDefault(t => t.Target == null);
            return task != null && game.Process(task);
        }

        private static bool Attack(Game game, int controllerId, ref int budget)
        {
            var me = game.ControllerById(controllerId);
            if (Dead(me)) return true;
            if (--budget < 0 || game.State == State.COMPLETE) return false;
            var attacks = me.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                                                  || t.PlayerTaskType == PlayerTaskType.HERO_ATTACK).ToList();
            if (attacks.Count == 0) return false;
            if (UpperBound(me) < me.Opponent.Hero.Health + me.Opponent.Hero.Armor) return false;

            var face = attacks.Where(t => t.Target?.Id == me.Opponent.Hero.Id).ToList();
            if (face.Count > 0)
            {
                // Nothing in the way: every attack goes face.
                for (int guard = 0; guard < 32 && !Dead(me); guard++)
                {
                    var next = me.Options().FirstOrDefault(t => (t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                        || t.PlayerTaskType == PlayerTaskType.HERO_ATTACK) && t.Target?.Id == me.Opponent.Hero.Id);
                    if (next == null || !game.Process(next)) break;
                }
                return Dead(me);
            }

            // A Taunt blocks the hero: try each attacker on the first Taunt, then search on.
            var taunt = attacks.Select(t => t.Target).OfType<Minion>().FirstOrDefault(m => m.HasTaunt);
            if (taunt == null) return false;
            foreach (var task in attacks.Where(t => t.Target?.Id == taunt.Id))
            {
                int attacker = task.Source?.Id ?? 0; // a hero attack has no source
                var child = game.CloneForSimulation(game.Seed + attacker);
                var copy = child.ControllerById(controllerId);
                var replay = copy.Options().FirstOrDefault(t => t.PlayerTaskType == task.PlayerTaskType
                    && (t.Source?.Id ?? 0) == attacker && t.Target?.Id == taunt.Id);
                if (replay == null || !child.Process(replay)) continue;
                if (Attack(child, controllerId, ref budget)) return true;
                if (budget < 0) return false;
            }
            return false;
        }

        /// <summary>All attack damage that could still be dealt this turn, ignoring Taunts.</summary>
        private static int UpperBound(Controller me)
        {
            int total = 0;
            foreach (var m in WishView.Minions(me))
                if (m.CanAttack || m.AttackableByRush)
                    total += m.AttackDamage * Math.Max(0, (m.HasWindfury ? 2 : 1) - m.NumAttacksThisTurn);
            if (me.Hero.CanAttack)
                total += me.Hero.AttackDamage * Math.Max(0, (me.Hero.Weapon?.IsWindfury == true ? 2 : 1) - me.Hero.NumAttacksThisTurn);
            return total;
        }
    }
}
