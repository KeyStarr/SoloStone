using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>Lifetime progression belongs to the controller, never the replaced power.</summary>
    public static class ImbueRules
    {
        // Native IMBUE placement on the controller still requires live qualification.
        public static int Count(Controller controller) => controller[GameTag.IMBUE];

        // Engine-only last global turn with an Undead hand-play; cloned, never projected.
        public const GameTag UndeadPlayedTurn = (GameTag)31020;

        /// <summary>The player's real class. A Neutral hero card inherits the previous class on play (PlayCard.PlayHeroCore).</summary>
        public static CardClass PlayerClass(Controller controller) => controller.HeroClass;

        public static bool IsConsumer(Card card) => card != null && card.Collectible
            && (card.Type == CardType.SPELL || card.Type == CardType.MINION || card.Type == CardType.WEAPON)
            && (card[GameTag.IMBUE] > 0 || card.RefTags.TryGetValue(GameTag.IMBUE, out int value) && value > 0);
        // Infinite exports Hide Cost, not the legacy Hide Stats passive marker.
        public static bool IsPassivePower(Card card) => card?.Id == "END_003p";
        // Imbue cards are playable by every class; without a Blessing (DH/Warlock/Warrior) only the Imbue clause is skipped.
        public static bool CanPlay(Controller controller, Card card) => !IsPassivePower(card);
        public static bool CanAutoCast(IPlayable spell) => !IsConsumer(spell.Card) || spell.IsPlayableByCardReq;
        public static bool CanCast(Controller controller, Spell spell, ICharacter target) => !IsConsumer(spell.Card)
            || CanAutoCast(spell) && spell.IsValidPlayTarget(target);

        // Identity includes gated profiles; mapping alone never permits installation.
        public static string Blessing(CardClass heroClass)
        {
            switch (heroClass)
            {
                case CardClass.DRUID: return "EDR_847p";
                case CardClass.HUNTER: return "EDR_850p";
                case CardClass.MAGE: return "EDR_851p";
                case CardClass.PALADIN: return "EDR_445p";
                case CardClass.SHAMAN: return "EDR_448p";
                case CardClass.PRIEST: return "EDR_449p";
                case CardClass.ROGUE: return "END_000p";
                case CardClass.DEATHKNIGHT: return "END_003p";
                default: return null;
            }
        }

        public static void Imbue(Controller controller, IEntity source, int times = 1)
        {
            if (times < 0) throw new ArgumentOutOfRangeException(nameof(times));
            string id = Blessing(PlayerClass(controller));
            // I1: DH/Warlock/Warrior have no Imbue effect, but their other card clauses resolve.
            if (id == null) return;
            for (int i = 0; i < times; i++)
            {
                int count = checked(Count(controller) + 1);
                var power = controller.Hero.HeroPower;
                bool upgrade = count != 1 && power.Card.Id == id;
                // Native upgrade FX: atomic pulse, then tags, so no packet can end on its SUB_SPELL_END.
                if (upgrade) Presentation.ImbueHistory.Pulse(power);
                controller[GameTag.IMBUE] = count;
                if (!upgrade)
                {
                    // Initialize before FULL_ENTITY so native text never shows a stale magnitude.
                    var card = Cards.FromId(id);
                    var tags = new EntityData {
                        [GameTag.CREATOR] = controller.Hero.Id,
                        [GameTag.TAG_SCRIPT_DATA_NUM_1] = count
                    };
                    if (card.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2))
                        tags[GameTag.TAG_SCRIPT_DATA_NUM_2] = count;
                    var replacement = (HeroPower)Entity.FromCard(controller, card, tags);
                    new ReplaceHeroPower().Process(controller.Game, controller, source, null,
                        new TaskStack { Playables = new System.Collections.Generic.List<IPlayable> { replacement } });
                    power = replacement;
                }
                else
                {
                    power[GameTag.TAG_SCRIPT_DATA_NUM_1] = count;
                    if (power.Card.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2))
                        power[GameTag.TAG_SCRIPT_DATA_NUM_2] = count;
                }
            }
        }

        public static void RecordUndeadPlay(Controller controller, Minion minion)
        {
            // Lean_ F2: pre-installation plays do not count; unrelated games retain their exact state.
            var power = controller.Hero.HeroPower;
            if (power.Card.Id != "END_003p" || !minion.Card.IsRace(Race.UNDEAD)) return;
            int turn = controller.Game.Turn;
            bool first = controller[UndeadPlayedTurn] != turn;
            controller[UndeadPlayedTurn] = turn;
            // Lean_ imbue-19 / D3: successful hand-play OnPlay, before Battlecry; summons never call here.
            if (first)
                Generic.AddEnchantmentBlock(controller.Game, Cards.FromId("END_003pe"), power, minion,
                    power[GameTag.TAG_SCRIPT_DATA_NUM_1]);
        }

        public static TaskState TriggerCurrentHeroPower(Controller controller, int? targetId = null)
        {
            var power = controller.Hero.HeroPower;
            // Lean_ imbue-13 / D2: Wisprider has no target/choice policy for these powers.
            if (IsPassivePower(power.Card) || power.IsPassiveHeroPower || power.Card.ChooseOne
                || power.Card.TargetingType != TargetingType.None) return TaskState.STOP;
            IPlayable target = null;
            if (targetId.HasValue)
            {
                if (targetId.Value < 0 || !controller.Game.IdEntityDic.ContainsKey(targetId.Value)) return TaskState.STOP;
                target = controller.Game.IdEntityDic[targetId.Value];
            }
            if (!power.IsValidPlayTarget(target as ICharacter)) return TaskState.STOP;
            return power.Power?.PowerTask?.Process(controller.Game, controller, power, target, new TaskStack())
                ?? TaskState.STOP;
        }
    }
}
