using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HeraldCardProof
{
    internal static void Run(string id) => StandardSetsProof.Case(id, (g, p, _) => {
        p[HeraldRules.Profile] = (int)Cards.FromId(id).Class;
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = Add(p, id);
        if (id == "CATA_725") p.Hero.Damage = 10;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: source is Minion ? 1 : -1)), "Herald card play");
        var soldier = p.BoardZone.Single(m => HeraldRules.IsSoldier(m.Card));
        Core2026Proof.Check(HeraldRules.Count(p) == 1 && soldier.Card.Id == HeraldRules.Soldier(Cards.FromId(id).Class), "consumer resolves own selected profile once");
        Core2026Proof.Check(soldier.ZonePosition == 2, "consumer summon position");
        if (id == "CATA_725")
        {
            Core2026Proof.Check(p.Hero.Damage == 10, "Disciple Battlecry must not heal damaged hero");
            Kill(g, (Minion)source);
            Core2026Proof.Check(p.Hero.Damage == 7 && HeraldRules.Count(p) == 1, "Disciple heals only on death");
            var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
            Core2026Proof.Check(p.Hero.Damage == 7 && HeraldRules.Count(p) == 1, "Silence suppresses heal; summons do not Battlecry");
        }
        if (id == "CATA_530")
        {
            p.Hero.Damage = 10;
            Core2026Proof.Check(p.Hero.HasLifeSteal && p.Hero.AttackDamage == 1, "Fel Infusion grants hero Lifesteal");
            p.Hero.IsExhausted = false;
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Lifesteal hero attack");
            Core2026Proof.Check(p.Hero.Damage == 9, "hero damage heals");
            End(g); Core2026Proof.Check(!p.Hero.HasLifeSteal && p.Hero.AttackDamage == 0, "Lifesteal and attack expire");
            Core2026Proof.Check(p.Hero.AppliedEnchantments.All(e => e.Card.Id != "CATA_530e"), "expired Lifesteal enchantment removed");
            End(g);
        }
        if (id == "CATA_525") Core2026Proof.Check(((Minion)source).IsRush, "printed Rush");
        if (id == "CATA_565") Core2026Proof.Check(((Minion)source).HasTaunt && ((Minion)source).AttackDamage == 1, "printed Taunt and adjacent Soldier buff");
        if (id == "CATA_580") Core2026Proof.Check(p.Hero.Weapon == source && p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 2, "weapon equipped with pinned stats");
        // Retain source for direct full-board repeat; this exercises the shared effect even if normal play needs space.
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int before = HeraldRules.Count(p);
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7 && HeraldRules.Count(p) == before + 1, "full board still Heralds once");
    });
}
