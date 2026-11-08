using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Build-37 Bonus Effects; provisional rules/wire are documented in docs/design/bonus.md.</summary>
    public static class BonusEffects
    {
        internal const GameTag State = (GameTag)38000;
        public const GameTag Elusive = (GameTag)1211;
        public const GameTag Keyword1 = (GameTag)4161;
        public const GameTag Keyword2 = (GameTag)4162;
        public static IReadOnlyList<GameTag> Pool { get; } = Array.AsReadOnly(new[] {
            GameTag.TAUNT, GameTag.WINDFURY, GameTag.DIVINE_SHIELD, GameTag.POISONOUS,
            Elusive, GameTag.RUSH, GameTag.LIFESTEAL, GameTag.REBORN });
        private static readonly string[] Actors = { "TTN_719e1", "TTN_719e3", "TTN_719e", "TTN_719e4",
            "TTN_719e5", "TTN_719e2", "TTN_719e7", "TTN_719e6" };

        internal static bool RetainActor(Card card) => Actors.Contains(card.Id)
            || card.Id == "BE_996e" || card.Id == "JAIL_101e" || card.Id == "TLC_465e";
        internal static bool HasState(Minion minion) => minion[State] != 0 || minion.Card.Id == "CATA_206";

        public static bool Active(Minion minion, GameTag keyword) => keyword == Elusive
            ? minion.CantBeTargetedBySpells && minion.CantBeTargetedByHeroPowers : minion[keyword] > 0;

        public static GameTag[] Eligible(Minion minion) => minion.Untouchable ? Array.Empty<GameTag>() : Pool.Where(k => !Active(minion, k)
            && (k != GameTag.RUSH || minion.Game.CurrentPlayer == minion.Controller
                && minion.NumAttacksThisTurn == 0 && !minion.CanAttack)).ToArray();

        public static int GiveRandom(Minion target, IPlayable source, int count)
        {
            if (target == null || source == null || target.Game != source.Game) throw new ArgumentException("Bonus Effect requires one game.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            int given = 0;
            for (; given < count; given++)
            {
                var eligible = Eligible(target);
                if (eligible.Length == 0) break;
                Give(target, source, eligible[target.Game.Random.Next(eligible.Length)]);
            }
            return given;
        }

        private static void Give(Minion target, IPlayable source, GameTag keyword)
        {
            target[State] = 1;
            Generic.AddEnchantmentBlock(target.Game, Cards.FromId(Actors[Pool.ToList().IndexOf(keyword)]), source, target);
            target.AppliedEnchantments.Last()[State] = 1;
            if (keyword == GameTag.REBORN) target[GameTag.HAS_BEEN_REBORN] = 0;
        }

        public static void ConfigureEnchantment(Card card)
        {
            int index = Array.IndexOf(Actors, card.Id);
            if (index < 0) throw new ArgumentException("Unknown Bonus Effect enchantment.");
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var keyword = Pool[index];
            card.Power = new Power { Enchant = keyword == Elusive
                ? new Enchant(Effects.CantBeTargetedBySpellsAndHeroPowers)
                : new Enchant(keyword, EffectOperator.SET, 1) };
        }

        private static void Set(Minion target, GameTag keyword, bool active)
        {
            if (keyword == Elusive) target.CantBeTargetedBySpells = active;
            else if (keyword == GameTag.RUSH) target.IsRush = active;
            else if (keyword == GameTag.WINDFURY) target.HasWindfury = active;
            else target[keyword] = active ? 1 : 0;
        }

        public static int Steal(Minion thief, Minion victim)
        {
            if (thief == null || victim == null || thief.Game != victim.Game) throw new ArgumentException("Bonus Effect theft requires one game.");
            var stolen = Pool.Where(k => Active(victim, k)).ToArray();
            if (stolen.Length == 0) return 0;
            victim[State] = 1;
            foreach (var keyword in stolen)
            {
                // Native overrides remove only keyword components; external auras retain their own contribution.
                Set(victim, keyword, false);
                Give(thief, thief, keyword);
            }
            return stolen.Length;
        }

        public static void ReplacePair(Minion target)
        {
            if (target.Card.Id != "CATA_206" || target.Zone != target.Controller.HandZone) return;
            var old = new[] { (GameTag)target[Keyword1], (GameTag)target[Keyword2] };
            var pairs = new List<(GameTag First, GameTag Second)>();
            for (int i = 0; i < Pool.Count; i++)
                for (int j = i + 1; j < Pool.Count; j++)
                    if (!(old.Contains(Pool[i]) && old.Contains(Pool[j]))) pairs.Add((Pool[i], Pool[j]));
            var pair = pairs[target.Game.Random.Next(pairs.Count)];
            foreach (var keyword in old)
            {
                var tag = keyword == Elusive ? GameTag.CANT_BE_TARGETED_BY_SPELLS : keyword;
                bool separateGrant = target.AppliedEnchantments?.Any(e => e.Power.Enchant?.Effects.Any(
                    effect => effect.Tag == tag && effect.Value > 0) == true) == true;
                Set(target, keyword, separateGrant);
            }
            // In hand these are intrinsic tags, without Rush readiness changes or accumulating-grant exclusions.
            foreach (var keyword in new[] { pair.First, pair.Second })
                Set(target, keyword, true);
            target[Keyword1] = (int)pair.First;
            target[Keyword2] = (int)pair.Second;
        }

        internal static void CopyState(IPlayable source, IPlayable destination)
        {
            if (!(source is Minion original) || !(destination is Minion copy)
                || !HasState(original)) return;
            foreach (var keyword in Pool)
            {
                bool active = keyword == Elusive
                    ? original._modifiedCantBeTargetedBySpells ?? original.Card.CantBeTargetedBySpells
                    : keyword == GameTag.TAUNT ? original._modifiedTaunt ?? original.Card.Taunt
                    : original.GetNativeGameTag(keyword) > 0;
                Set(copy, keyword, active);
            }
            copy[GameTag.HAS_BEEN_REBORN] = source[GameTag.HAS_BEEN_REBORN];
            copy[State] = source[State];
            if (source.Card.Id == "CATA_206")
            {
                copy[Keyword1] = source[Keyword1]; copy[Keyword2] = source[Keyword2];
            }
        }
    }
}
