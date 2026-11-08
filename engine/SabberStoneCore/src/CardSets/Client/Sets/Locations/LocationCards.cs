using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LocationCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Keep the shared registry seam unchanged; only collectible pilots enter the Standard corpus.
            void RegisterStandard(int dbfId, string id, Action<Card> configure)
            {
                register(dbfId, id, card => { configure(card); StandardSets.Record(card); });
            }
            RegisterStandard(SanguineDepths.DbfId, "CORE_REV_990", SanguineDepths.Configure);
            register(82315, "REV_990e", SanguineDepths.ConfigureBuff);
            RegisterStandard(UnderbellyNetwork.DbfId, "JAIL_877", UnderbellyNetwork.Configure);
            RegisterStandard(SpireOfSolitude.DbfId, "JAIL_511", SpireOfSolitude.Configure);
            register(126476, "JAIL_511t", card => card.Implemented = true);
            register(127698, "JAIL_877t", UnderbellyNetwork.ConfigureRat);
            RegisterStandard(CosmicSprings.DbfId, "BE_251", CosmicSprings.Configure);
            RegisterStandard(BloodpetalBiome.DbfId, "TLC_449", BloodpetalBiome.Configure);
            RegisterStandard(PastGnomeregan.DbfId, "TIME_044", PastGnomeregan.Configure);
            register(120822, "TIME_044t1", PastGnomeregan.ConfigurePresent);
            register(120823, "TIME_044t2", PastGnomeregan.ConfigureFuture);
            register(120824, "TIME_044e", PastGnomeregan.ConfigureBuff);
            register(120848, "TIME_044t1e", PastGnomeregan.ConfigureDeathrattle);
            RegisterStandard(DemolitionRenovator.DbfId, "CORE_REV_023", DemolitionRenovator.Configure);
            RegisterStandard(TwistingNether.DbfId, "CORE_EX1_312", TwistingNether.Configure);
            RegisterStandard(EruptingVolcano.DbfId, "CATA_584", EruptingVolcano.Configure);
        }
    }
}
