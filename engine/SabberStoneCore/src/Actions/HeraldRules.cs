using System;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Herald progression belongs to the controller; realized Soldier scaling belongs to the entity.</summary>
    public static class HeraldRules
    {
        // Engine-only count. Native 4327 meaning is unpinned; NativeMatch blocks it on every wire path.
        public const GameTag Progress = (GameTag)4327;
        public const GameTag Profile = (GameTag)4534;
        public const GameTag Keyword = (GameTag)4309;
        // Script 2 is an instance-only initialized factor, not another increment counter.
        public const GameTag RealizedFactor = GameTag.TAG_SCRIPT_DATA_NUM_2;
        public static int Count(Controller controller) => controller[Progress];
        public static int Tier(Controller controller) => Math.Min(2, Count(controller) / 2);
        public static string Soldier(CardClass profile) => profile switch {
            CardClass.DEATHKNIGHT => "CATA_780t", (CardClass)14 => "CATA_525t",
            CardClass.ROGUE => "CATA_158t", CardClass.SHAMAN => "CATA_565t",
            CardClass.WARLOCK => "CATA_725t", CardClass.WARRIOR => "CATA_580t", _ => null
        };
        public static bool IsSoldier(Card card) => card != null && Soldier(card.Class) == card.Id;
        /// <summary>Branch (a): the controller's own Soldier class: selected profile, else current hero class.</summary>
        public static CardClass OwnClass(Controller controller)
        {
            var selected = (CardClass)controller[Profile];
            if (Soldier(selected) != null) return selected;
            if (Soldier(controller.HeroClass) != null) return controller.HeroClass;
            return CardClass.INVALID;
        }
        /// <summary>
        /// Soldier class for one Herald resolution (wiki, tier C, ClayByte DM): (a) own class, (b) the source card's class when
        /// it is single-class with a Soldier, (c) the opponent's hero class, (d) INVALID: no Soldier, no progress (UNPINNED).
        /// Fallbacks (b)/(c) are recomputed per resolution and never stored. <paramref name="source"/> null skips (b).
        /// </summary>
        public static CardClass Class(Controller controller, Card source = null)
        {
            var own = OwnClass(controller);
            if (own != CardClass.INVALID) return own;
            if (source != null && source[GameTag.MULTIPLE_CLASSES] == 0 && Soldier(source.Class) != null) return source.Class;
            if (Soldier(controller.Opponent.HeroClass) != null) return controller.Opponent.HeroClass;
            return CardClass.INVALID;
        }
        /// <summary>One value per controller: the native text builder reads only controller tag 4534. Source-independent.</summary>
        public static CardClass WireClass(Controller controller) => Class(controller, null);
        // Herald cards are playable by every class; only the Soldier part depends on the resolved class.
        // Automatic casts ignore mana/hand restrictions, but must validate before removing the spell.
        public static bool CanAutoCast(IPlayable spell)
            => spell.Card[Keyword] == 0 || spell.IsPlayableByCardReq;
        public static bool CanCast(Controller controller, Spell spell, ICharacter target)
            => spell.Card[Keyword] == 0 || (CanAutoCast(spell) && (spell.IsValidPlayTarget(target)
                    // Rite only Combos on hand play; automatic casts may omit its conditional target.
                    || (spell.Card.Id == "CATA_785" && target == null)));
        public static int Resolve(Controller controller, IEntity source)
        {
            var profile = Class(controller, source.Card);
            // (d) UNPINNED: no Soldier class anywhere: the card's other text still resolves, but no Soldier and no progress.
            if (profile == CardClass.INVALID) return 0;
            int tier = Tier(controller);
            string token = Soldier(profile);
            // Only the controller's own class is remembered (hero replacement keeps it); fallbacks never lock in.
            var own = OwnClass(controller);
            if (own != CardClass.INVALID) controller[Profile] = (int)own;
            int id = 0;
            if (token != null && !controller.BoardZone.IsFull)
            {
                var minion = (Minion)Entity.FromCard(controller, Cards.FromId(token), new EntityData {
                    [GameTag.ZONE] = (int)Zone.PLAY, [GameTag.DISPLAYED_CREATOR] = source.Id });
                ConfigureSummon(minion, tier);
                int position = source is BoardObject location
                    ? Math.Min(controller.BoardZone.SlotCount, location.Zone == controller.BoardZone ? location.ZonePosition + 1 : location.LastBoardPosition)
                    : EffectContext.DeathrattlePosition(controller, source);
                // Repeated Heralds append after the source's existing Soldiers in creation order.
                while (position < controller.BoardZone.SlotCount
                    && IsSoldier(controller.BoardZone.GetSlot(position).Card)
                    && controller.BoardZone.GetSlot(position)[GameTag.DISPLAYED_CREATOR] == source.Id)
                    position++;
                Generic.SummonBlock(controller.Game, minion, position, source);
                id = minion.Id;
            }
            // Provisional ordering: summon at the old tier; even a full board advances the count.
            controller[Progress] = Count(controller) + 1;
            return id;
        }
        public static void ConfigureSummon(IPlayable entity, int tier)
        {
            if (!(entity is Minion m) || !IsSoldier(m.Card) || m.IsSilenced || m[RealizedFactor] > 0) return;
            int factor = 1 << Math.Max(0, Math.Min(2, tier));
            m[RealizedFactor] = factor;
            m[GameTag.TAG_SCRIPT_DATA_NUM_1] = m.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] * factor;
            m.AttackDamage += m.Card.ATK * (factor - 1);
            m.BaseHealth += m.Card.Health * (factor - 1);
        }
        public static void ConsumeNeighbor(Minion source)
        {
            var owner = source.Controller;
            if (source.Zone != owner.BoardZone || source.ToBeDestroyed) return;
            bool redirected = owner.BoardZone.Any(m => m.Card.Id == "CATA_726" && !m.IsSilenced
                && !m.IsDormant && !m.ToBeDestroyed);
            if (redirected)
            {
                var candidates = owner.Opponent.DeckZone.Where(c => c is Minion).ToArray();
                if (candidates.Length == 0) return;
                var victim = candidates[source.Game.Random.Next(candidates.Length)];
                source.Game.OnRandomHappened(true);
                if (!DrawReplacementRules.DestroyOffboard(victim)) owner.Opponent.GraveyardZone.Add(owner.Opponent.DeckZone.Remove(victim));
            }
            else
            {
                int right = source.ZonePosition + 1;
                if (right >= owner.BoardZone.SlotCount || !(owner.BoardZone.GetSlot(right) is Minion victim)
                    || victim.Untouchable || victim.ToBeDestroyed) return;
                victim.ToBeDestroyed = true;
                if (!victim.ToBeDestroyed) return;
            }
            int amount = Magnitude(source);
            if (source.Card.Id == "CATA_725t")
            {
                // Preserve the existing Soldier's buff shape when no new aura is present.
                Effects.Attack_N(amount).ApplyTo(source);
                Effects.Health_N(amount).ApplyTo(source);
            }
            else Generic.AddEnchantmentBlock(source.Game, Cards.FromId("CATA_726te"), source, source, amount, amount);
        }

        public static int Magnitude(IPlayable soldier) => soldier[GameTag.TAG_SCRIPT_DATA_NUM_1];
    }
}
