using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmeraldDreamPools
    {
        internal static OfflinePoolFilter QonzuSpell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        internal static OfflinePoolFilter MoonMinion() => new OfflinePoolFilter(classMode: PoolClassMode.Exact,
            exactClass: CardClass.PRIEST, type: CardType.MINION);
        internal static OfflinePoolFilter MoonSpell() => new OfflinePoolFilter(classMode: PoolClassMode.Exact,
            exactClass: CardClass.PRIEST, type: CardType.SPELL);
        internal static OfflinePoolFilter Wind() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter Murloc() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.MURLOC);
        internal static OfflinePoolFilter Dragon() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DRAGON);
        internal static OfflinePoolFilter DruidCard() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID);
        internal static OfflinePoolFilter Elemental() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.ELEMENTAL);
        internal static OfflinePoolFilter FiveCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5);
        internal static OfflinePoolFilter TwoCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);
        internal static OfflinePoolFilter NatureSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 4);
        internal static OfflinePoolFilter ThreeCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3);
        internal static OfflinePoolFilter SixCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 6, maxCost: 6);

        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("EDR_493", Alarashi.DbfId, new PoolRequirement("demon", Alarashi.Filter, new[] { "DEMONHUNTER" }, destination: PoolDestination.Transform)),
            new OfflinePoolDeclaration("EDR_811", RiteOfAtrocity.DbfId, new PoolRequirement("undead", RiteOfAtrocity.Filter, new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("EDR_517", Qonzu.DbfId, new PoolRequirement("spell", QonzuSpell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("EDR_465", Ysondre.DbfId, new PoolRequirement("dragon", Dragon(), new[] { "WARRIOR" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("EDR_449p", 114069,
                new PoolRequirement("moon-minion", MoonMinion(), new[] { "PRIEST" }, PoolCostBinding.StackNumberMaxCost, ordinaryCost: 1),
                new PoolRequirement("moon-spell", MoonSpell(), new[] { "PRIEST" }, PoolCostBinding.StackNumberMaxCost, ordinaryCost: 1)),
            new OfflinePoolDeclaration("EDR_448p", 114059, new PoolRequirement("wind-minion", Wind(),
                new[] { "SHAMAN" }, PoolCostBinding.StackNumber, ordinaryCost: 2, destination: PoolDestination.Transform)),
            new OfflinePoolDeclaration("EDR_461", RitualoftheNewMoon.DbfId,
                new PoolRequirement("R3", ThreeCostMinion(), new[] { "PRIEST" }, destination: PoolDestination.Summon),
                new PoolRequirement("R6", SixCostMinion(), new[] { "PRIEST" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("EDR_273", Symbiosis.DbfId, new PoolRequirement("other-class-choose-one", Symbiosis.Filter, new[] { "DRUID" })),
            new OfflinePoolDeclaration("EDR_872", SparkOfLife.DbfId,
                new PoolRequirement("mage-spell", SparkOfLife.Filter(CardClass.MAGE), new[] { "MAGE" }),
                new PoolRequirement("druid-spell", SparkOfLife.Filter(CardClass.DRUID), new[] { "MAGE" })),
            new OfflinePoolDeclaration("FIR_952", Scorchreaver.DbfId, new PoolRequirement("fel-spell", Scorchreaver.Filter, new[] { "DEMONHUNTER" })),
            new OfflinePoolDeclaration("EDR_999", GnawingGreenfin.DbfId, new PoolRequirement("murloc", Murloc(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("EDR_462", SelenicDrake.DbfId, new PoolRequirement("dragon", Dragon(), new[] { "PRIEST" })),
            new OfflinePoolDeclaration("EDR_873", EnvoyoftheGlade.DbfId, new PoolRequirement("druid-card", DruidCard(), new[] { "NEUTRAL" }, destination: PoolDestination.Transform)),
            new OfflinePoolDeclaration("FIR_913", InfernoHerald.DbfId, new PoolRequirement("elemental", Elemental(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("EDR_060", WardofEarth.DbfId, new PoolRequirement("five-cost-minion", FiveCostMinion(), new[] { "DRUID" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("EDR_463", TwilightInfluence.DbfId, new PoolRequirement("two-cost-minion", TwoCostMinion(), new[] { "PRIEST" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("EDR_271", GroveShaper.DbfId, new PoolRequirement("nature-spell", NatureSpell(), new[] { "DRUID" })),
            new OfflinePoolDeclaration("EDR_102", TreacherousTormentor.DbfId, new PoolRequirement("legendary-minion", TreacherousTormentor.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("EDR_105", CreatureofMadness.DbfId, new PoolRequirement("3-cost-minion", CreatureofMadness.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("EDR_456", Darkrider.DbfId, new PoolRequirement("dragon", Darkrider.Filter, new[] { "WARRIOR" })),
            new OfflinePoolDeclaration("EDR_488", AvantGardening.DbfId, new PoolRequirement("deathrattle-minion", AvantGardening.Filter, new[] { "WARLOCK" })),
            new OfflinePoolDeclaration("FIR_939", ShadowflameSuffusion.DbfId, new PoolRequirement("warrior-minion", ShadowflameSuffusion.Filter, new[] { "WARRIOR" })),
            new OfflinePoolDeclaration("FIR_900", Cremate.DbfId, new PoolRequirement("minion", Cremate.Filter, new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("EDR_882", Jumpscare.DbfId, new PoolRequirement("5-plus-cost-demon", Jumpscare.Filter, new[] { "DEMONHUNTER" })),
            new OfflinePoolDeclaration("FIR_924", ShadowflameStalker.DbfId, new PoolRequirement("demon", ShadowflameStalker.Filter, new[] { "WARLOCK" }))
        };
    }
}
