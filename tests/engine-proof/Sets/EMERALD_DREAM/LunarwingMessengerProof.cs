using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class LunarwingMessengerProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_449", (g, p, id) => {
        p.Hero.Card = Cards.FromId("HERO_09");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Cost == 2 && source.AttackDamage == 3 && source.Health == 2
            && source.Card.Class == CardClass.PRIEST && source.Card.IsRace(Race.BEAST) && source.HasLifeSteal,
            "2 mana 3/2 Priest Beast with printed Lifesteal");
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "EDR_449p" && ImbueRules.Count(p) == 1
            && p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "Battlecry installs Moon");
        var power = p.Hero.HeroPower; power.IsExhausted = true;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.HeroPower.Id == power.Id && power.IsExhausted && ImbueRules.Count(p) == 2
            && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "Battlecry upgrades exhausted Moon in place");
        p.Hero.Damage = 10;
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.Hero.Damage == 7, "health damage heals for three");
        source.NumAttacksThisTurn = 0;
        p.Opponent.Hero.Armor = 5;
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 2 && p.Opponent.Hero.Damage == 3 && p.Hero.Damage == 4,
            "armor-only damage heals for three");
        p.Opponent.Hero.Armor = 0;
        source.Silence(); source.NumAttacksThisTurn = 0;
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(!source.HasLifeSteal && p.Opponent.Hero.Damage == 6 && p.Hero.Damage == 4,
            "Silence removes Lifesteal");
        var druid = ImbueProof.Make(CardClass.DRUID, (int)g.Seed);
        Play(druid, druid.Player1, id);
        Core2026Proof.Check(druid.Player1.Hero.HeroPower.Card.Id == "EDR_847p" && ImbueRules.Count(druid.Player1) == 1,
            "copied Priest card installs current hero class Golem");
        while (!druid.Player1.BoardZone.IsFull) Summon(druid.Player1, "CS2_182");
        var full = Add(druid.Player1, id); druid.Player1.UsedMana = 0;
        string fullBefore = Canonical.State(druid);
        Core2026Proof.Check(!druid.Player1.Options().Any(t => t.Source?.Id == full.Id)
            && !Generic.PlayCard(druid.Player1, full)
            && Canonical.State(druid) == fullBefore, "full board blocks play and Imbue without mutation");
        var supported = ImbueProof.Make(CardClass.PALADIN, (int)g.Seed); var owner = supported.Player1;
        Play(supported, owner, id);
        Core2026Proof.Check(owner.UsedMana == 2 && ImbueRules.Count(owner) == 1 && owner.Hero.HeroPower.Card.Id == "EDR_445p",
            "Paladin hand-play pays normally and installs Dragon");
    });
}
