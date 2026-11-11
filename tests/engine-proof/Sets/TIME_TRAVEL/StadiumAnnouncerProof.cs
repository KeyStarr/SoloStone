using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class StadiumAnnouncerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_034", (g, p, id) => {
        EmptyPool(g, p, id);
        var source = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
        WithCards(g, new[] { "CORE_TRL_111" }, () => {
            PlayEntity(g, p, source); RewindTestHelpers.KeepIfOffered(g);
            var own = p.Hero.Weapon; var enemy = p.Opponent.Hero.Weapon;
            Core2026Proof.Check(own.Card.Id == "CORE_TRL_111" && enemy.Card.Id == "CORE_TRL_111"
                && own.AttackDamage == 3 && own.Durability == 3 && enemy.AttackDamage == 2 && enemy.Durability == 2,
                "both equip cross-class weapons; only own gains +1 Attack/+1 Durability; weapon battlecry not played");
            Core2026Proof.Check(own.AppliedEnchantments.Single().Card.Id == "TIME_034e" && enemy.AppliedEnchantments == null, "pinned buff enchantment");
            Resolve(g, p, id);
            Core2026Proof.Check(p.Hero.Weapon.Id != own.Id && p.Opponent.Hero.Weapon.Id != enemy.Id
                && own.Zone.Type == Zone.GRAVEYARD && enemy.Zone.Type == Zone.GRAVEYARD, "equip replaces both existing weapons");
        });
        var previous = p.Hero.Weapon;
        EmptyPool(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon == previous && previous.AttackDamage == 4 && previous.Durability == 4,
            "literal give yours +1/+1 still buffs existing weapon after empty generation pool");
        Resume(g, next => Resolve(next, next.Player1, id));
        Core2026Proof.Check(p.Hero.Weapon != null && p.Opponent.Hero.Weapon != null, "installed weapon pool");
    });
}
