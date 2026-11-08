using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SmolderingStrength
    {
        internal const int DbfId = 115638;
        // Rule: 1/2/3 over three owner turns in hand; upgrade after owner end, discard at third owner end; reentry resets age.
        internal static int Magnitude(IPlayable source)
        {
            var history = source.Game.HistoryFacts;
            var residence = history.IndexAt(history.Count)?.Entity(source.Id)?.Hand;
            if (residence == null) return 1;
            var end = history.RecordAt(residence.Exit == 0 ? history.Count : residence.Exit);
            if (residence.Entry.Turn == end.Turn) return 1;
            int turns = history.Query.OwnerTurnsStartedWhileHolding(source.Id, source.Controller.Id);
            // Convert indexed starts into completed owner turns, freezing the endpoint after hand exit.
            // Opening draws/mulligans already say turn 1, but its owner start has not happened yet.
            if ((history.IndexAt(residence.Entry.Sequence)?.Prefix(source.Controller.Id)?.Turns ?? 0) > 0
                && residence.Entry.ActiveControllerId == source.Controller.Id) turns++;
            if (end.ActiveControllerId == source.Controller.Id) turns--;
            return Math.Min(3, 1 + turns);
        }
        private static int Left(IPlayable source) => 4 - Magnitude(source);
        internal static void Display(Card card)
        {
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Magnitude);
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_2, Left);
        }
        internal static Trigger Expiry() => new Trigger(TriggerType.TURN_END) {
            TriggerActivation = TriggerActivation.HAND,
            Condition = new SelfCondition(source => Magnitude(source) >= 3),
            SingleTask = new DiscardTask(EntityType.SOURCE)
        };
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            Display(card);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
            card.Power = new Power { Trigger = Expiry(), PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                bool playerCast = ctx.History.Casts().Last(f => f.EntityId == s.Id
                    && f.Outcome == HistoryCastOutcome.Started).CasterKind == HistoryCastKind.Player;
                // Rule: redirected player targets bypass selection criteria (wiki.gg/wiki/Template:Spellbender_notes).
                int? target = t is Minion minion && minion.Zone == minion.Controller.BoardZone
                    && (playerCast || ((Playable)s).IsValidPlayTarget(minion)) ? t.Id : (int?)null;
                if (!target.HasValue && !playerCast)
                    target = ctx.RandomLegalTarget(s.Id);
                if (target.HasValue) Generic.AddEnchantmentBlock(g, Cards.FromId("FIR_914e"), (IPlayable)s,
                    (IPlayable)g.IdEntityDic[target.Value], Magnitude((IPlayable)s));
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
