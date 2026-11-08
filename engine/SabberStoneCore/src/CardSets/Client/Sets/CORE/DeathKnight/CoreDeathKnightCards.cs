using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set cards without a scripted legacy original (DeathKnight slice).</summary>
    internal static class CoreDeathKnightCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(ArmyOfTheDead.DbfId, "RLK_060", ArmyOfTheDead.Configure);
            register(BattlefieldNecromancer.DbfId, "RLK_061", BattlefieldNecromancer.Configure);
            register(BoneguardCommander.DbfId, "CORE_RLK_506", BoneguardCommander.Configure);
            register(78570, "RLK_061t", BoneguardCommander.ConfigureToken);
            register(79278, "RLK_008t", ArmyOfTheDead.ConfigureToken);
            register(TombGuardians.DbfId, "CORE_RLK_118", TombGuardians.Configure);
            register(91712, "RLK_118t3", TombGuardians.ConfigureToken);
            register(Asphyxiate.DbfId, "CORE_RLK_087", Asphyxiate.Configure);
            register(MalignantHorror.DbfId, "CORE_RLK_745", MalignantHorror.Configure);
            register(PoisonBreath.DbfId, "CORE_EDR_002", PoisonBreath.Configure);
            register(117309, "CORE_EDR_002e", PoisonBreath.ConfigureBuff);
            register(NerubianSwarmguard.DbfId, "CORE_RLK_062", NerubianSwarmguard.Configure);
            register(AcolyteOfDeath.DbfId, "CORE_RLK_121", AcolyteOfDeath.Configure);
            register(ChillfallenBaron.DbfId, "RLK_708", ChillfallenBaron.Configure);
            register(SkeletalSidekick.DbfId, "RLK_958", SkeletalSidekick.Configure);
            register(86191, "RLK_958e", SkeletalSidekick.ConfigureBuff);
            register(DeathsAdvance.DbfId, "CORE_CATA_009", DeathsAdvance.Configure);
            register(Hematurge.DbfId, "CORE_RLK_066", Hematurge.Configure);
            register(MarrowManipulator.DbfId, "CORE_RLK_505", MarrowManipulator.Configure);
            register(BloodTap.DbfId, "CORE_RLK_712", BloodTap.Configure);
            register(BodyBagger.DbfId, "RLK_503", BodyBagger.Configure);
            register(GraveStrength.DbfId, "RLK_707", GraveStrength.Configure);
            register(DeathStrike.DbfId, "RLK_024", DeathStrike.Configure);
            register(FrostStrike.DbfId, "RLK_025", FrostStrike.Configure);
            register(91653, "RLK_712e", BloodTap.ConfigureBuff);
            register(80646, "RLK_707e", GraveStrength.ConfigureBuff);
            register(96197, "RLK_707e2", GraveStrength.ConfigureGreaterBuff);
            // Death Knight slice c2.
            register(FrostwyrmsFury.DbfId, "CORE_RLK_063", FrostwyrmsFury.Configure);
            register(87085, "RLK_063t", FrostwyrmsFury.ConfigureToken);
            register(Deathchiller.DbfId, "CORE_RLK_083", Deathchiller.Configure);
            register(AlexandrosMograine.DbfId, "CORE_RLK_706", AlexandrosMograine.Configure);
            register(87824, "RLK_706e3", AlexandrosMograine.ConfigureEffect);
            register(CorruptedAshbringer.DbfId, "RLK_067", CorruptedAshbringer.Configure);
            register(Thassarian.DbfId, "RLK_223", Thassarian.Configure);
            register(HarbingerOfWinter.DbfId, "RLK_511", HarbingerOfWinter.Configure);
            register(RemorselessWinter.DbfId, "RLK_709", RemorselessWinter.Configure);
            register(GnomeMuncher.DbfId, "RLK_720", GnomeMuncher.Configure);
        }
    }
}
