using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class JaggedEdgeOfTimeProof
{
    internal static void Run() => StandardSetsProof.Case("END_001", (game, player, id) => {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.Type == CardType.WEAPON && card.Cost == 3 && card.ATK == 3 && card.Health == 2
            && card[GameTag.BATTLECRY] == 1 && card[GameTag.MULTIPLE_CLASSES] == 65,
            "pinned 3-mana 3/2 Battlecry weapon retains DK/Rogue class mask");
        foreach (CardClass cls in Enum.GetValues<CardClass>().Distinct())
            Core2026Proof.Check(CardClassRules.IsAllowedInDeck(card, cls) == (cls == CardClass.ROGUE || cls == CardClass.DEATHKNIGHT),
                "deck membership exactly DK/Rogue: " + cls);
        foreach (var cls in new[] { CardClass.ROGUE, CardClass.DEATHKNIGHT })
        {
            var g = ImbueProof.Make(cls, (int)game.Seed); var p = g.Player1;
            var oldPower = p.Hero.HeroPower;
            var weapon = (Weapon)Add(p, id);
            Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[weapon.Id]));
            var blessing = p.Hero.HeroPower;
            Core2026Proof.Check(p.Hero.Weapon == weapon && weapon.AttackDamage == 3 && weapon.Durability == 2
                && p.Hero.AttackDamage == 3 && p.UsedMana == 3 && oldPower.Zone == p.SetasideZone
                && blessing.Card.Id == (cls == CardClass.ROGUE ? "END_000p" : "END_003p")
                && ImbueRules.Count(p) == 1 && blessing[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
                "equip and Battlecry install correct blessing for " + cls);
            var replacement = Add(p, id);
            Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[replacement.Id]));
            Core2026Proof.Check(weapon.Zone == p.GraveyardZone && p.Hero.Weapon.Id == replacement.Id
                && p.Hero.Weapon.Durability == 2 && p.Hero.HeroPower.Id == blessing.Id
                && ImbueRules.Count(p) == 2 && blessing[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                "replacement destroys previous weapon, equips fresh durability, upgrades blessing once");
            var attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
            Core2026Proof.Check(g.Process(attack) && p.Opponent.Hero.Damage == 3 && p.Hero.Weapon.Durability == 1
                && ImbueRules.Count(p) == 2, "attack consumes one durability without repeating Battlecry");
        }
        var supported = ImbueProof.Make(CardClass.PALADIN, (int)game.Seed); var owner = supported.Player1;
        var held = Add(owner, id);
        Resume(supported, next => PlayEntity(next, next.Player1, next.IdEntityDic[held.Id]));
        Core2026Proof.Check(owner.Hero.Weapon.Id == held.Id && owner.UsedMana == 3 && ImbueRules.Count(owner) == 1
            && owner.Hero.HeroPower.Card.Id == "EDR_445p", "Paladin pays, equips and Imbues through existing path");
    });
}
