using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-corpse-discover", ProofKind.PoweredUp)]
    private static void RunBatch12CorpseDiscover()
    {
        Scenario("EDR_811", (f, card, met) => {
            f.Player[GameTag.CORPSES] = met ? 5 : 4;
            f.Player[GameTag.CORPSES_SPENT_THIS_GAME] = 3;
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            Check(f.Player.Choice != null, "Rite always offers a Discover");
            // Avoid the gift that immediately plays the chosen card, so delivery stays observable.
            int chosen = f.Player.Choice.Choices.First(id => f.Game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY] == 0
                || f.Game.IdEntityDic[f.Game.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id != "EDR_100t8");
            f.Process(ChooseTask.Pick(f.Player, chosen));
            var awarded = f.Game.IdEntityDic[chosen];
            bool gifted = DarkGifts.Has(awarded);
            Check(awarded.Zone == f.Player.HandZone && awarded.Card.IsRace(Race.UNDEAD)
                && f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == (gifted ? 5 : 3)
                && f.Player.Choice == null, "Rite delivers chosen Undead and spends exactly two only for a Dark Gift");
            return gifted;
        });

        Scenario("JAIL_451", (f, card, met) => {
            f.Player[GameTag.CORPSES] = met ? 8 : 7;
            f.Player[GameTag.CORPSES_SPENT_THIS_GAME] = 3;
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            Check(f.Player.Choice != null, "Blood Clone always offers a Discover");
            int chosen = f.Player.Choice.Choices.First();
            f.Process(ChooseTask.Pick(f.Player, chosen));
            var awarded = f.Game.IdEntityDic[chosen];
            bool summoned = f.Player.BoardZone.Count == 1;
            Check(awarded.Zone == f.Player.HandZone && awarded.Card.Cost == 5
                && f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == (summoned ? 8 : 3)
                && (!summoned || (f.Player.BoardZone.Single().Card.Id == awarded.Card.Id
                    && f.Player.BoardZone.Single().Id != chosen)),
                "Blood Clone delivers discovery and spends exactly five for a separate copy");
            return summoned;
        });
    }
}
