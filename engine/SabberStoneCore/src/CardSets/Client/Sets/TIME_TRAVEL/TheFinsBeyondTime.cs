using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheFinsBeyondTime
    {
        // Direct generation entry points include the collectible parents of exported actors.
        // Extend this list when a new recipe can introduce Fins (including copied Battlecries).
        internal static readonly IReadOnlyCollection<string> OpeningSources = Array.AsReadOnly(new[] {
            "BE_044", "BE_045", "BE_087", "BE_114", "BE_229", "BE_252",
            "BE_257", "BE_262", "BE_553", "BE_571", "BE_702", "BE_740",
            "BE_851", "CATA_153", "CATA_155", "CATA_156", "CATA_158", "CATA_160",
            "CATA_525", "CATA_530", "CATA_561", "CATA_565", "CATA_567", "CATA_569",
            "CATA_580", "CATA_722", "CATA_725", "CATA_780", "CATA_785", "CORE_BOT_256",
            "CORE_CATA_006", "CORE_CFM_781", "CORE_EX1_189", "CORE_GIL_836", "CORE_KAR_069", "CORE_KAR_077",
            "CORE_REV_308", "CORE_WW_374", "Core_LOE_115", "DINO_424", "EDR_102", "EDR_226",
            "EDR_227", "EDR_231", "EDR_264", "EDR_449", "EDR_451", "EDR_463",
            "EDR_518", "EDR_519", "EDR_781", "EDR_800", "EDR_845", "EDR_852",
            "EDR_871", "EDR_970", "EDR_999", "END_000", "END_001", "END_003",
            "FIR_900", "FIR_920", "JAIL_122", "JAIL_200", "JAIL_328", "JAIL_407",
            "JAIL_448", "MEND_502", "MEND_505", "TIME_000", "TIME_014", "TIME_038",
            "TIME_058", "TIME_448", "TIME_613", "TLC_109", "TLC_235", "TLC_442",
            "TLC_461", "TLC_462", "TLC_469", "TLC_514", "TLC_602",
        });
        internal static bool RequiresOpening(Game game)
        {
            var initial = game.Player1.DeckCards.Concat(game.Player2.DeckCards);
            if (initial.Any(card => card.Id == "TIME_706")) return true;
            // Pinned/legacy replay pools cannot introduce an unadmitted identity.
            if (game.LegacyPoolReplay || !game.OfflinePools.Standard.Any(card => card.CardId == "TIME_706")) return false;
            var pending = new Stack<Card>(initial);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count > 0)
            {
                var card = pending.Pop();
                if (!seen.Add(card.Id)) continue;
                if (OpeningSources.Contains(card.Id)) return true;
                var declaration = OfflinePoolDeclarations.Cards.FirstOrDefault(source => source.CardId == card.Id);
                if (declaration == null) continue;
                // Follow admitted intermediate generators too (for example spell Discover into a portal).
                foreach (var pool in declaration.Pools)
                foreach (var owner in new[] { game.Player1, game.Player2 })
                foreach (var candidate in OfflineCardPools.Candidates(game, owner.HeroClass, pool.Filter, card.AssetId, pool.Destination, OfflineCardPools.PrintedClassMask(card)))
                    if (!seen.Contains(candidate.Id)) pending.Push(candidate);
            }
            return false;
        }

        [StandardCard("TIME_706",120774)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>TemporaryRules.SwapStartingHand((IPlayable)s)) };
        }
        [StandardCard("TIME_706e3",123517)]
        internal static void Escrow(Card card)
        { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.Power=new Power(); }
    }
}
