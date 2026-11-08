using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CataclysmCards
    {
        // Batch 6 blocked on core primitives; leave these collectibles unregistered:
        // CATA_496: temporary CANT_ATTACK survives Silence (Minion.Silence drops OneTurnEffects
        // without undoing this tag); Enchantment.Remove cannot separately expire restriction/control.
        // CATA_550: core change: no bounded remainder summon API; OnBodyEntered replays
        // the entire +99 declaration, so a refill cannot cap the final batch to the remainder.
        // CATA_139: core change: no positive stat-gain event; aura polling loses intermediate gains.
        // CATA_151: core change: Hero.HasWindfury only reads/writes the weapon, ignoring hero auras.
        // CATA_154: core change: other-class spell repeat scheduler missing.
        // CATA_155: core change: own-turn Health-loss replacement (including Health payment) missing.
        // CATA_726: unpinned rule: which enemy-deck minion Arms/Soldiers destroy, and empty-deck gain.
        // CATA_613: Character.IsImmune ignores AuraEffects.Immune; AdaptiveEffect and expiry
        // overwrite the same flag, so independent Immune sources require core composition.
        // CATA_616: PlayHistory has no paid Cost snapshot. LastCardPlayed points at a mutable
        // entity: opponent Sap clears TAG_LAST_KNOWN_COST_IN_HAND via EntityData.Reset (4 becomes 0).
        // CATA_898: Aura has no opposing BOARD type; an opponent-owned helper lacks source-side
        // control-change and silence lifecycle support (Aura.AddToGame/Minion.Silence).
        // MEND_805: HistoryDefinition.Materialize cannot serialize CapturedArchmage.ProgressTrigger;
        // strict transport hits this when Charity copies its death definition. Needs core codec support.
        // CATA_557: coordinator skip; automatic targeting bypasses Elusive protection.
        // Batch 8: CATA_697 needs a filtered hand selector; SelectEntityThenContinueTask offers all cards.
        // CATA_497 also requires pinned Deathwing discount magnitude, scope and ordering.
        // CATA_464: Generic.Copy drops spell script-data payload; no verified payload enchantment.
        // Dynamic Dragon Breath damage cannot survive hand copies without a core change.
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(TwistedMonstrosity.DbfId, "CATA_206", TwistedMonstrosity.Configure);
            register(GronnGiant.DbfId, "CATA_616", GronnGiant.Configure);
            register(Vyranoth.DbfId, "CATA_213", Vyranoth.Configure);
            register(123155, "CATA_213e", Vyranoth.ConfigureAttack);
            register(127871, "CATA_213e2", Vyranoth.ConfigureHealth);
            register(BashanaRunetotem.DbfId, "MEND_046", BashanaRunetotem.Configure);
            register(130722, "MEND_046t", BashanaRunetotem.ConfigureTreant);
            register(123332, "CATA_481", Isorath.Configure);
            register(126120, "CATA_481e", Isorath.ConfigureSleep);
            register(122961, "CATA_566", TolvirCarver.Configure);
            register(129251, "CATA_566e", TolvirCarver.ConfigureCarving);
            register(CommanderGeddon.DbfId, "CATA_591", CommanderGeddon.Configure);
            register(123322, "CATA_591e", CommanderGeddon.ConfigureReplacement);
            register(125804, "CATA_591e2", CommanderGeddon.ConfigureDiscount);
            register(125451,"CATA_154t",Sinestra.ConfigureWing);
            register(125503,"CATA_154t1",Sinestra.ConfigureWing);
            register(126501,"CATA_154e",card => { card.Implemented=true; card.Power=new SabberStoneCore.Enchants.Power(); });
            register(Sinestra.DbfId,"CATA_154",Sinestra.Configure);
            register(TamePet.DbfId, "MEND_300", TamePet.Configure);
            register(MigratingElekk.DbfId, "MEND_303", MigratingElekk.Configure);
            register(RoamFree.DbfId, "MEND_307", RoamFree.Configure);
            register(Morchok.DbfId, "CATA_570", Morchok.Configure);
            register(ChowDown.DbfId, "CATA_465", ChowDown.Configure);
            register(125516, "CATA_465t", ChowDown.ConfigureToken);
            register(SindragosasTriumph.DbfId, "CATA_978", SindragosasTriumph.Configure);
            register(127165, "CATA_978e", SindragosasTriumph.ConfigureBuff);
            register(Decimation.DbfId, "CATA_581", Decimation.Configure);
            register(RagnarosTheGreatFire.DbfId, "CATA_150", RagnarosTheGreatFire.Configure);
            register(124113, "CATA_150t", RagnarosTheGreatFire.ConfigureHand);
            register(125412, "CATA_150t1", RagnarosTheGreatFire.ConfigureHand);
            register(AlAkirLordOfStorms.DbfId, "CATA_153", AlAkirLordOfStorms.Configure);
            register(125443, "CATA_153t", AlAkirLordOfStorms.ConfigureHand);
            register(127053, "CATA_153t1", AlAkirLordOfStorms.ConfigureHand);
            register(TheBlackBlood.DbfId, "CATA_300", TheBlackBlood.Configure);
            register(122756, "CATA_300t1", TheBlackBlood.ConfigureBody);
            register(128030, "CATA_300t2", TheBlackBlood.ConfigureBody);
            register(128031, "CATA_300t3", TheBlackBlood.ConfigureBody);
            register(Vulcanos.DbfId, "CATA_488", Vulcanos.Configure);
            register(123666, "CATA_488t", Vulcanos.ConfigurePlume);
            register(128032, "CATA_488t2", Vulcanos.ConfigurePlume);
            register(Chromatus.DbfId, "CATA_432", Chromatus.Configure);
            register(122402, "CATA_432t1", Chromatus.ConfigureHead);
            register(122405, "CATA_432t2", Chromatus.ConfigureHead);
            register(122407, "CATA_432t3", Chromatus.ConfigureHead);
            register(122410, "CATA_432t4", Chromatus.ConfigureHead);
            register(ArcaneFlow.DbfId, "CATA_489", ArcaneFlow.Configure);
            register(123743, "CATA_489t", ArcaneFlow.ConfigureFirst);
            register(123744, "CATA_489t2", ArcaneFlow.ConfigureSecond);
            register(WildwoodCircle.DbfId, "CATA_134", WildwoodCircle.Configure);
            register(122972, "CATA_134t", WildwoodCircle.ConfigureFirst);
            register(122973, "CATA_134t2", WildwoodCircle.ConfigureSecond);
            register(126886, "CATA_134t3", card => card.Implemented = true);
            register(127980, "CATA_134e", WildwoodCircle.ConfigureDeathrattle);
            register(Schism.DbfId, "CATA_306", Schism.Configure);
            register(122876, "CATA_306t1", Schism.ConfigureFirst);
            register(122877, "CATA_306t2", Schism.ConfigureSecond);
            register(122875, "CATA_306e", Schism.ConfigureBuff);
            register(FlightManeuvers.DbfId, "CATA_479", FlightManeuvers.Configure);
            register(123141, "CATA_479t", FlightManeuvers.ConfigureFirst);
            register(123142, "CATA_479t2", FlightManeuvers.ConfigureSecond);
            register(123140, "CATA_479t3", card => card.Implemented = true);
            register(127188, "CATA_479e", FlightManeuvers.ConfigureBuff);
            register(SupplyRun.DbfId, "CATA_820", SupplyRun.Configure);
            register(121758, "CATA_820t", SupplyRun.ConfigureFirst);
            register(121760, "CATA_820t2", SupplyRun.ConfigureSecond);
            register(121761, "CATA_820e", SupplyRun.ConfigureBuff);
            register(Spiritspeaker.DbfId, "MEND_301", Spiritspeaker.Configure);
            register(GemstoneHoarder.DbfId, "CATA_897", GemstoneHoarder.Configure);
            register(130826, "CATA_897e3", GemstoneHoarder.ConfigureCapture);
            register(DisposableAcolytes.DbfId, "CATA_499", DisposableAcolytes.Configure);
            register(CeremonialClash.DbfId, "CATA_569", CeremonialClash.Configure);
            register(Lifebloom.DbfId, "MEND_042", Lifebloom.Configure);
            register(SpearheartSentry.DbfId, "CATA_474", SpearheartSentry.Configure);
            register(EarthenRoar.DbfId, "CATA_554", EarthenRoar.Configure);
            register(127130, "CATA_554e", EarthenRoar.ConfigureHealth);
            register(MalevolentMutant.DbfId, "CATA_697", MalevolentMutant.Configure);
            register(StickybombSaboteur.DbfId, "CATA_186", StickybombSaboteur.Configure);
            register(123782, "CATA_186t", StickybombSaboteur.ConfigureSabotage);
            register(123781, "CATA_186te", card => { card.Implemented = true; card.Power = new SabberStoneCore.Enchants.Power(); });
            register(Warloc.DbfId, "CATA_180", Warloc.Configure);
            register(131143, "CATA_180e", card => { card.Implemented = true; card.Power = new SabberStoneCore.Enchants.Power(); });

            register(AgentOfTheOldOnes.DbfId, "CATA_200", AgentOfTheOldOnes.Configure);
            register(OcularOccultist.DbfId, "CATA_490", OcularOccultist.Configure);
            register(ShelteredSurvivor.DbfId, "CATA_721", ShelteredSurvivor.Configure);
            register(ShadowswornDisciple.DbfId, "CATA_725", ShadowswornDisciple.Configure);
            register(ArmoredBloodletter.DbfId, "CATA_525", ArmoredBloodletter.Configure);
            register(FelInfusion.DbfId, "CATA_530", FelInfusion.Configure);
            register(SkywallSentinel.DbfId, "CATA_565", SkywallSentinel.Configure);
            register(CataclysmicWarAxe.DbfId, "CATA_580", CataclysmicWarAxe.Configure);
            register(RitualOfPower.DbfId, "CATA_561", RitualOfPower.Configure);
            register(EnvoyOfTheEnd.DbfId, "CATA_722", EnvoyOfTheEnd.Configure);
            register(ExperimentalAnimation.DbfId, "CATA_156", ExperimentalAnimation.Configure);
            register(ObsessiveTechnician.DbfId, "CATA_780", ObsessiveTechnician.Configure);
            register(ManiacalFollower.DbfId, "CATA_158", ManiacalFollower.Configure);
            register(RiteOfTwilight.DbfId, "CATA_785", RiteOfTwilight.Configure);
            register(127584, "CATA_561t", Breezling.Configure);
            register(126983, "CATA_780t", SoldierOfOnyxia.Configure);
            register(127003, "CATA_158t", SoldierOfSinestra.Configure);
            register(126995, "CATA_525t", HeraldSoldiers.ConfigureAzshara);
            register(127071, "CATA_565t", HeraldSoldiers.ConfigureAlAkir);
            register(127045, "CATA_725t", HeraldSoldiers.ConfigureChoGall);
            register(127082, "CATA_580t", HeraldSoldiers.ConfigureRagnaros);
            register(124142, "CATA_530e", FelInfusion.ConfigureBuff);
            register(EbonscaleScout.DbfId, "CATA_552", EbonscaleScout.Configure);
            register(122870, "CATA_552t", EbonscaleScout.ConfigureBattlecry);
            register(Maloriak.DbfId, "CATA_494", Maloriak.Configure);
            register(ConfrontTheTolvir.DbfId, "CATA_560", ConfrontTheTolvir.Configure);
            register(Torch.DbfId, "CATA_585", Torch.Configure);
            register(SearingFissure.DbfId, "CATA_582", SearingFissure.Configure);
            register(NurturingNature.DbfId, "MEND_305", NurturingNature.Configure);
            register(Stormbinder.DbfId, "CATA_724", Stormbinder.Configure);
            register(EarthenDrake.DbfId, "CATA_999", EarthenDrake.Configure);
            register(RafaamsLastStand.DbfId, "CATA_498", RafaamsLastStand.Configure);
            register(BroxigarsLastStand.DbfId, "CATA_526", BroxigarsLastStand.Configure);
            register(127691, "CATA_582e", SearingFissure.ConfigureBuff);
            register(123512, "MEND_305e", NurturingNature.ConfigureBuff);
            register(Mossbinding.DbfId, "CATA_135", Mossbinding.Configure);
            register(ForestsGift.DbfId, "CATA_138", ForestsGift.Configure);
            register(GaronasLastStand.DbfId, "CATA_203", GaronasLastStand.Configure);
            register(Mend.DbfId, "CATA_302", Mend.Configure);
            register(PurifyingBreath.DbfId, "CATA_303", PurifyingBreath.Configure);
            register(SleetStorm.DbfId, "CATA_485", SleetStorm.Configure);
            register(IncensedMatriarch.DbfId, "CATA_305", IncensedMatriarch.Configure);
            register(ScalebreakerBulwark.DbfId, "CATA_475", ScalebreakerBulwark.Configure);
            register(BronzeRedeemer.DbfId, "CATA_478", BronzeRedeemer.Configure);
            register(CommandClaw.DbfId, "CATA_467", CommandClaw.Configure);
            register(ChromaticBroodmother.DbfId, "CATA_469", ChromaticBroodmother.Configure);
            register(MedivhsTriumph.DbfId, "CATA_308", MedivhsTriumph.Configure);
            register(122974, "CATA_135t", card => card.Implemented = true);
            register(122984, "CATA_135e", Mossbinding.ConfigureBuff);
            register(127198, "CATA_138e", ForestsGift.ConfigureBuff);
            register(130303, "CATA_305e", IncensedMatriarch.ConfigureBuff);
            register(126052, "CATA_478t", card => card.Implemented = true);
            register(122768, "CATA_467e", CommandClaw.ConfigureBuff);
            register(BronzeKeeper.DbfId, "CATA_476", BronzeKeeper.Configure);
            register(123116, "CATA_476t", card => card.Implemented = true);
            register(EmboldeningBlade.DbfId, "MEND_803", EmboldeningBlade.Configure);
            register(130741, "MEND_803e", EmboldeningBlade.ConfigureGameBuff);
            register(130742, "MEND_803e2", EmboldeningBlade.ConfigureBuff);
            register(IridescentFlitterwing.DbfId, "CATA_133", IridescentFlitterwing.Configure);
            register(SelflessProtector.DbfId, "CATA_208", SelflessProtector.Configure);
            register(TwilightEgg.DbfId, "CATA_210", TwilightEgg.Configure);
            register(123127, "CATA_210t", card => card.Implemented = true);
            register(NozdormuBronzeAspect.DbfId, "CATA_473", NozdormuBronzeAspect.Configure);
            register(SigilOfTheSeas.DbfId, "CATA_528", SigilOfTheSeas.Configure);
            register(StonetalonStriker.DbfId, "CATA_551", StonetalonStriker.Configure);
            register(LoGoshsLastStand.DbfId, "CATA_610", LoGoshsLastStand.Configure);
            register(BrashBattlemaster.DbfId, "MEND_800", BrashBattlemaster.Configure);
            register(122981, "CATA_133e", IridescentFlitterwing.ConfigureBuff);
            register(130282, "CATA_473e", NozdormuBronzeAspect.ConfigureBuff);
            register(127933, "CATA_528t", card => card.Implemented = true);
            register(122868, "CATA_551t", card => card.Implemented = true);
            register(125848, "CATA_610e", LoGoshsLastStand.ConfigureDeathrattle);
            register(123627, "MEND_800e", BrashBattlemaster.ConfigureGameBuff);
            register(123629, "MEND_800e2", BrashBattlemaster.ConfigureBuff);
            register(FrostbittenImp.DbfId, "CATA_612", FrostbittenImp.Configure);
            register(InjuredAttendant.DbfId, "CATA_304", InjuredAttendant.Configure);
            register(DarkscaleBroodmother.DbfId, "CATA_111", DarkscaleBroodmother.Configure);
            register(TwilightMistress.DbfId, "CATA_201", TwilightMistress.Configure);
            register(ReinforcementRallier.DbfId, "CATA_558", ReinforcementRallier.Configure);
            register(WarmasterBlackhorn.DbfId, "CATA_720", WarmasterBlackhorn.Configure);
            register(WastelandVanguard.DbfId, "MEND_302", WastelandVanguard.Configure);
            register(AratortheRedeemer.DbfId, "MEND_804", AratortheRedeemer.Configure);
            register(123631, "MEND_804e", AratortheRedeemer.ConfigureBuff);
            register(WinterspringWhelp.DbfId, "CATA_484", WinterspringWhelp.Configure);
            register(DrakeadonMongrel.DbfId, "CATA_723", DrakeadonMongrel.Configure);
            register(CarrierWhelp.DbfId, "CATA_556", CarrierWhelp.Configure);
            register(SeedingDragon.DbfId, "MEND_045", SeedingDragon.Configure);
            register(AzsharasTriumph.DbfId, "CATA_136", AzsharasTriumph.Configure);
            register(122985, "CATA_136e", AzsharasTriumph.ConfigureBuff);
            register(TalanjisLastStand.DbfId, "CATA_471", TalanjisLastStand.Configure);
            register(122880, "CATA_471e", TalanjisLastStand.ConfigureDeathrattle);
            register(UnstableSpellcaster.DbfId, "CATA_483", UnstableSpellcaster.Configure);
            register(WizenedWildspeaker.DbfId, "MEND_041", WizenedWildspeaker.Configure);
            register(MuradinsLastStand.DbfId, "CATA_568", MuradinsLastStand.Configure);
            register(RavenousFelfisher.DbfId, "CATA_529", RavenousFelfisher.Configure);
            register(SpellweaversBrilliance.DbfId, "CATA_452", SpellweaversBrilliance.Configure);
            register(122455, "CATA_452t", card => card.Implemented = true);
            register(Broodwatcher.DbfId, "CATA_132", Broodwatcher.Configure);
            register(122969, "CATA_132t", card => card.Implemented = true);
            register(Charity.DbfId, "MEND_805", Charity.Configure);
            register(130679, "MEND_805e", Charity.ConfigureBuff);
            register(SylvanassTriumph.DbfId, "CATA_557", SylvanassTriumph.Configure);
            register(BurstingLeyline.DbfId, "MEND_500", BurstingLeyline.Configure);
            register(LeyWalker.DbfId, "MEND_501", LeyWalker.Configure);
            register(CrystallizedLeyline.DbfId, "MEND_502", CrystallizedLeyline.Configure);
            register(SurgeNeedle.DbfId, "MEND_503", SurgeNeedle.Configure);
            register(LeylineNexus.DbfId, "MEND_504", LeylineNexus.Configure);
            register(123509, "MEND_504e", LeylineNexus.ConfigureDiscount);
            register(TheArcanomicon.DbfId, "MEND_505", TheArcanomicon.Configure);
            register(123497, "MEND_505t", TheArcanomicon.ConfigureOption);
            register(123499, "MEND_505t2", TheArcanomicon.ConfigureOption);
            register(123501, "MEND_505t3", TheArcanomicon.ConfigureOption);
            register(MysticRunesaber.DbfId, "MEND_506", MysticRunesaber.Configure);
            register(HeartrootStones.DbfId, "MEND_043", HeartrootStones.Configure);
            register(ScorchingRavager.DbfId, "CATA_160", ScorchingRavager.Configure);
            register(FlashFlood.DbfId, "CATA_533", FlashFlood.Configure);
            register(DreadLeviathan.DbfId, "CATA_699", DreadLeviathan.Configure);
            register(125572, "CATA_699e", DreadLeviathan.ConfigureDrain);
            register(125573, "CATA_699e2", DreadLeviathan.ConfigureGain);
            register(BattlefieldBlaster.DbfId, "CATA_209", BattlefieldBlaster.Configure);
            register(CleansingCleric.DbfId, "CATA_216", CleansingCleric.Configure);
            register(RubySanctum.DbfId, "CATA_301", RubySanctum.Configure);
            register(EldritchTentacles.DbfId, "CATA_491", EldritchTentacles.Configure);
            register(123125, "CATA_209e", BattlefieldBlaster.ConfigureBuff);
            register(123694, "CATA_216e", CleansingCleric.ConfigureBuff);
            register(126032, "CATA_301e", RubySanctum.ConfigureBuff);
        }
    }
}
