using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    /// <summary>Legacy play conditions shared by powers and their hand glow.</summary>
    internal static class LegacyPoweredUpRules
    {
        internal static readonly SelfCondition LowHealth = SelfCondition.IsHealth(12, RelaSign.LEQ);
        internal static readonly SelfCondition Beast = SelfCondition.IsControllingRace(Race.BEAST);
        internal static readonly SelfCondition ShadowformTwo = SelfCondition.IsHeroPowerCard("EX1_625t");
        internal static readonly SelfCondition ShadowformVoid = SelfCondition.IsHeroPowerCard("ICC_830p");

        internal static bool HoldingDragon(IPlayable source) => source.Controller.HandZone
            .Any(p => p != source && p is ICharacter character && character.IsRace(Race.DRAGON));
        internal static bool AnotherMech(IPlayable source) => source.Controller.BoardZone
            .Any(p => p != source && p.IsRace(Race.MECHANICAL));
        internal static bool ControlsBeast(IPlayable source) => Beast.Eval(source);
        internal static bool EmptyHand(IPlayable source) => !source.Controller.HandZone.Any(p => p != source);
        internal static bool FourEnemyMinions(IPlayable source) => source.Controller.Opponent.BoardZone.CountExceptUntouchables >= 4;
        internal static bool MortalStrike(IPlayable source) => LowHealth.Eval(source.Controller.Hero);
        internal static bool WeaponEquipped(IPlayable source) => SelfCondition.IsAnyWeaponEquiped.Eval(source.Controller.Hero);
        internal static bool InShadowform(IPlayable source) => ShadowformTwo.Eval(source) || ShadowformVoid.Eval(source);

        internal static void Apply(Dictionary<string, Card> cards)
        {
            cards["CORE_KAR_062"].PoweredUpWhen(HoldingDragon);
            cards["CORE_LOE_039"].PoweredUpWhen(AnotherMech);
            cards["CORE_TRL_111"].PoweredUpWhen(ControlsBeast);
            cards["CORE_BRM_013"].PoweredUpWhen(EmptyHand);
            cards["EX1_085"].PoweredUpWhen(FourEnemyMinions);
            cards["EX1_408"].PoweredUpWhen(MortalStrike);
            cards["EX1_409"].PoweredUpWhen(WeaponEquipped);
            cards["EX1_625"].PoweredUpWhen(InShadowform);
        }
    }
}
