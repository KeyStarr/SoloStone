using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VictorNefarius
    {
        // Generated from the pinned enUS export, markup stripped, explicit nonempty lines only.
        private static readonly string[] DragonLines = (
            "AT_008 AT_017 BE_734 BOT_066 BRM_004 BRM_018 BRM_020 BRM_024 BRM_025 BRM_026 BRM_030 BRM_031 BT_341 " +
            "BT_726 CATA_111 CATA_154 CATA_160 CATA_201 CATA_305 CATA_432 CATA_474 CATA_484 CATA_494 CATA_556 CATA_723 " +
            "CATA_898 CATA_999 CORE_AT_008 CORE_DRG_026 CORE_DRG_079 CORE_DRG_090 CORE_DRG_095 CORE_DRG_226 " +
            "CORE_DRG_229 CORE_EX1_043 CORE_EX1_189 CORE_EX1_284 CORE_ICC_027 CORE_ICC_029 CORE_ICC_838 CORE_LOOT_137 " +
            "CORE_NEW1_023 CORE_RLK_919 CORE_UNG_848 CORE_VAN_EX1_561 CORE_VAN_EX1_572 CORE_WON_065 CORE_YOP_034 " +
            "CS3_032 CS3_033 CS3_034 CS3_035 CS3_036 DAL_146 DAL_581 DEEP_006 DINO_435 DMF_528 DMF_532 DRG_019 DRG_026 " +
            "DRG_054 DRG_072 DRG_073 DRG_075 DRG_079 DRG_090 DRG_095 DRG_102 DRG_201 DRG_209 DRG_211 DRG_213 DRG_226 " +
            "DRG_229 DRG_254 DRG_270 DRG_300 DRG_304 DRG_309 DRG_310 DRG_313 EDR_451 EDR_462 EDR_571 EDR_572 EDR_889 " +
            "EDR_890 END_006 END_010 END_021 END_022 END_033 END_034 END_035 ETC_099 EX1_043 EX1_189 EX1_284 EX1_560 " +
            "EX1_561 EX1_562 EX1_563 EX1_572 FIR_901 FIR_927 FIR_960 GDB_321 GDB_860 GIL_128 GIL_190 GIL_681 GIL_683 " +
            "GIL_905 ICC_027 ICC_029 ICC_838 KAR_033 LEG_CS3_032 LEG_CS3_033 LEG_CS3_034 LEG_CS3_036 LOOT_118 LOOT_137 " +
            "LOOT_144 LOOT_410 LOOT_538 MEND_045 NEW1_023 NEW1_030 NX2_022 NX2_037 NX2_050 OG_271 OG_317 OG_320 " +
            "ONY_001 ONY_007 ONY_026 ONY_035 RLK_915 RLK_916 RLK_919 SCH_230 SCH_232 SCH_711 TIME_003 TIME_004 " +
            "TIME_024 TIME_045 TIME_051 TIME_056 TIME_435 TIME_713 TIME_714 TIME_720 TIME_856 TIME_871 TLC_243 TLC_600 " +
            "TLC_888 TOY_380 TOY_386 TOY_896 TRL_323 TRL_526 TRL_569 TSC_069 TTN_718 TTN_907 TTN_924 UNG_848 VAC_527 " +
            "WON_052 WON_065 WON_140 WW_344 WW_434 WW_819 WW_820 WW_824 WW_826 YOD_014 YOD_036 YOD_043 YOP_025 YOP_034").Split(' ');
        private static readonly string[] UndeadLines = (
            "AT_003 AT_030 AT_031 AT_036 AT_054 AT_089 AT_105 AT_119 AT_128 AT_129 AT_130 AT_131 AV_125 AV_139 AV_601 " +
            "BAR_324 BAR_328 BAR_744 BE_781 BOT_433 BOT_509 CAP_800 CATA_304 CFM_063 CFM_120 CFM_308 CFM_341 CFM_636 " +
            "CFM_808 CORE_AT_003 CORE_CFM_120 CORE_CS2_231 CORE_DAL_721 CORE_EX1_002 CORE_EX1_012 CORE_EX1_059 " +
            "CORE_EX1_193 CORE_FP1_030 CORE_FP1_031 CORE_GIL_667 CORE_ICC_018 CORE_ICC_019 CORE_ICC_026 CORE_ICC_027 " +
            "CORE_ICC_028 CORE_ICC_032 CORE_ICC_034 CORE_ICC_047 CORE_ICC_058 CORE_ICC_065 CORE_ICC_069 CORE_ICC_083 " +
            "CORE_ICC_092 CORE_ICC_093 CORE_ICC_094 CORE_ICC_096 CORE_ICC_097 CORE_ICC_099 CORE_ICC_204 CORE_ICC_212 " +
            "CORE_ICC_220 CORE_ICC_240 CORE_ICC_243 CORE_ICC_245 CORE_ICC_252 CORE_ICC_407 CORE_ICC_415 CORE_ICC_419 " +
            "CORE_ICC_450 CORE_ICC_467 CORE_ICC_468 CORE_ICC_700 CORE_ICC_701 CORE_ICC_702 CORE_ICC_705 CORE_ICC_801 " +
            "CORE_ICC_809 CORE_ICC_810 CORE_ICC_811 CORE_ICC_820 CORE_ICC_838 CORE_ICC_851 CORE_ICC_852 CORE_ICC_853 " +
            "CORE_ICC_854 CORE_ICC_856 CORE_ICC_858 CORE_ICC_900 CORE_ICC_902 CORE_ICC_903 CORE_ICC_904 CORE_ICC_911 " +
            "CORE_ICC_913 CORE_LOE_012 CORE_MAW_032 CORE_NEW1_020 CORE_OG_149 CORE_REV_251 CORE_REV_845 CORE_REV_901 " +
            "CORE_REV_957 CORE_RLK_706 CORE_ULD_723 CORE_WON_096 CORE_tt_004 CS2_221 CS2_231 DAL_538 DAL_721 DINO_416 " +
            "DINO_435 DMF_520 DMF_532 DRG_203 DRG_235 EDR_815 END_031 ETC_088 ETC_209 ETC_339 ETC_349 ETC_526 EX1_002 " +
            "EX1_012 EX1_059 EX1_097 EX1_191 EX1_193 EX1_595 FP1_001 FP1_004 FP1_005 FP1_008 FP1_009 FP1_013 FP1_014 " +
            "FP1_015 FP1_016 FP1_017 FP1_024 FP1_026 FP1_027 FP1_028 FP1_030 FP1_031 GDB_321 GIL_117 GIL_198 GIL_207 " +
            "GIL_510 GIL_513 GIL_515 GIL_526 GIL_545 GIL_557 GIL_616 GIL_618 GIL_635 GIL_655 GIL_667 GIL_678 GIL_681 " +
            "GIL_805 GIL_815 GIL_902 ICC_018 ICC_019 ICC_026 ICC_027 ICC_028 ICC_032 ICC_034 ICC_047 ICC_058 ICC_065 " +
            "ICC_069 ICC_083 ICC_092 ICC_093 ICC_094 ICC_096 ICC_097 ICC_099 ICC_204 ICC_212 ICC_220 ICC_240 ICC_243 " +
            "ICC_245 ICC_252 ICC_407 ICC_415 ICC_419 ICC_450 ICC_467 ICC_468 ICC_700 ICC_701 ICC_702 ICC_705 ICC_801 " +
            "ICC_809 ICC_810 ICC_811 ICC_820 ICC_838 ICC_851 ICC_852 ICC_853 ICC_854 ICC_856 ICC_858 ICC_900 ICC_902 " +
            "ICC_903 ICC_904 ICC_911 ICC_913 JAIL_204 JAIL_440 JAIL_942 JAM_005 JAM_007 JAM_021 LEG_RLK_079 " +
            "LEG_RLK_115 LEG_RLK_753 LOE_012 LOE_019 LOE_023 LOE_073 LOE_110 LOOT_534 MAW_032 NEW1_005 NEW1_020 " +
            "NX2_002 NX2_005 NX2_010 NX2_014 NX2_018 NX2_024 NX2_027 NX2_034 NX2_037 NX2_050 NX2_051 OG_138 OG_149 " +
            "OG_248 REV_251 REV_845 REV_901 REV_957 RLK_029 RLK_079 RLK_113 RLK_115 RLK_117 RLK_119 RLK_216 RLK_223 " +
            "RLK_503 RLK_511 RLK_532 RLK_535 RLK_538 RLK_551 RLK_570 RLK_571 RLK_572 RLK_591 RLK_651 RLK_658 RLK_659 " +
            "RLK_706 RLK_708 RLK_713 RLK_753 RLK_816 RLK_821 RLK_824 RLK_831 RLK_833 RLK_845 RLK_900 RLK_912 RLK_914 " +
            "RLK_957 RLK_958 SCH_140 SCH_618 SCH_621 SCH_711 TIME_040 TIME_614 TIME_617 TLC_102 TLC_432 TOY_821 " +
            "TOY_893 TRL_131 TRL_251 TRL_327 TRL_501 TRL_528 TSC_085 TTN_450 ULD_167 ULD_185 ULD_186 ULD_205 ULD_206 " +
            "ULD_207 ULD_214 ULD_274 ULD_275 ULD_288 ULD_721 ULD_723 ULD_727 VAC_429 VAC_436 VAC_437 WON_076 WON_079 " +
            "WON_096 WON_340 WORK_010 WORK_032 WW_322 WW_358 WW_367 YOG_300 YOG_512 YOP_012 tt_004").Split(' ');
        internal static OfflinePoolFilter Components(PoolScope scope, bool dragon) => new OfflinePoolFilter(
            scope: scope, type: CardType.MINION, minCost: 0, maxCost: 10, race: dragon ? Race.DRAGON : Race.UNDEAD,
            cardIds: dragon ? DragonLines : UndeadLines);
        [OfflinePool("CATA_470", 122778)]
        internal static PoolRequirement[] Pools() => new[] { PoolScope.Wild }
            .SelectMany(scope => new[] { true, false }.Select(dragon => new PoolRequirement(
                "creation-" + scope.ToString().ToLowerInvariant() + (dragon ? "-dragon" : "-undead"),
                Components(scope, dragon), new[] { "DEATHKNIGHT" },
                dragon ? PoolCostBinding.None : PoolCostBinding.StackNumberMaxCost, ordinaryCost: dragon ? (int?)null : 5, destination: PoolDestination.Other))).ToArray();

        [StandardCard("CATA_470", 122778)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Entourage = new[] { "CATA_470t1" };
            card.PoweredUpWhen(HoldingDragon);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_470/v1",
                new CustomTask((g,c,s,t,stack) => {
                    stack.Number = 0; stack.Number1 = c.HandZone.Any(h => h.Id != s.Id && h.Card.IsRace(Race.DRAGON)) ? 1 : 0;
                }),
                new DiscoverThenContinueTask(Dragons),
                new CustomTask((g,c,s,t,stack) => stack.Number = stack.Playables.LastOrDefault()?.Id ?? 0),
                new DiscoverThenContinueTask(Undead),
                new CustomTask(Finish)) };
        }
        private static bool HoldingDragon(IPlayable source) => source.Controller.HandZone.Any(h => h.Id != source.Id && h.Card.IsRace(Race.DRAGON));
        internal static Card[] Candidates(Game game, Controller controller, bool dragon, int budget = 10) =>
            new[] { PoolScope.Wild }.SelectMany(scope =>
                OfflineCardPools.Candidates(game, controller.HeroClass, Components(scope, dragon).WithMaxCost(budget), 122778, PoolDestination.Other))
                .GroupBy(c => c.Id, StringComparer.Ordinal).Select(group => group.First()).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        private static Card[] Dragons(Game g, Controller c, IEntity s, TaskStack stack) =>
            AssembleRules.Take(g, Candidates(g,c,true).ToList(), 3);
        private static Card[] Undead(Game g, Controller c, IEntity s, TaskStack stack) => stack.Number == 0 ? Array.Empty<Card>() :
            AssembleRules.Take(g, Candidates(g,c,false,10-g.IdEntityDic[stack.Number].Card.Cost).ToList(), 3);
        private static void Finish(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            var second = stack.Playables.LastOrDefault();
            if (stack.Number == 0 || second == null) return;
            var result = Entity.FromCard(c, GeneratedDefinitionRules.Creation(g.IdEntityDic[stack.Number].Card, second.Card),
                new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id });
            if (stack.Number1 != 0) CostRules.AddModifier((Playable)result, EffectOperator.SUB, 3);
            Generic.AddHandPhase(c, result);
        }
        [StandardCard("CATA_470t1", 127644)]
        internal static void Creation(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.UseGeneratedTargeting(); }
    }
}
