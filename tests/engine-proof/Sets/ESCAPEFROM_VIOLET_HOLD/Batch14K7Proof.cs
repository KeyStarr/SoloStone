using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class Batch14K7VioletHoldProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_721", Parasite);

    private static void Parasite(Game g, Controller p, string id)
    {
        var held = Add(p, id);
        Summon(p, "CS2_065");
        Core2026Proof.Check(((Minion)held).AttackDamage == 3 && ((Minion)held).Health == 3, "hand Parasite does not trigger");
        PlayEntity(g, p, held); var source = (Minion)held;
        Core2026Proof.Check(source.IsRush && source.Card[GameTag.PREPARE] == 1, "exact Prepare/Rush keywords");
        Summon(p, "CS2_182"); Summon(p.Opponent, "CS2_065");
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3, "non-Demon and opponent summons excluded");
        var demon = Summon(p, "CS2_065");
        Core2026Proof.Check(demon.AttackDamage == 1 && demon.Health == 3 && source.AttackDamage == 4 && source.Health == 6,
            "after friendly 1/3 Voidwalker gains its stats without removing donor stats");
        var enchant = source.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_721e");
        Core2026Proof.Check(enchant.Card.AssetId == 131257 && enchant.ScriptTag1 == 1 && enchant[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3,
            "exact Stolen Soul enchantment with unequal attack/health");
        var buffed = g.CloneExact(history: true); var buffedOwner = buffed.Player1;
        var donor = (Minion)Add(buffedOwner, "CS2_065");
        Effects.Attack_N(2).ApplyTo(donor); Effects.Health_N(4).ApplyTo(donor);
        PlayEntity(buffed, buffedOwner, donor);
        var recipient = (Minion)buffed.IdEntityDic[source.Id];
        Core2026Proof.Check(donor.AttackDamage == 3 && donor.Health == 7 && recipient.AttackDamage == 7 && recipient.Health == 13,
            "summoned Demon contributes current buffed stats, not printed definition stats");
        source.Damage = 2;
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1; var body = (Minion)next.IdEntityDic[source.Id];
            StandardSetsProof.ParkBoard(owner.Opponent);
            Summon(owner, "CS2_065");
            Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 7 && source.AttackDamage == 4 && source.Health == 4,
                "stateful trigger clone/snapshot independent, existing damage preserved");
            body.Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(body.AttackDamage == 3 && body.Health == 3 && !body.IsRush, "Silence removes accumulated stats, Rush and trigger; Health capped at printed three");
            Summon(owner, "CS2_065");
            Core2026Proof.Check(body.AttackDamage == 3 && body.Health == 3, "silenced board source no longer triggers");
            StandardSetsProof.ParkBoard(owner);
            Summon(owner, "CS2_065");
            Core2026Proof.Check(body.AttackDamage == 3, "silenced offboard source never triggers");
        }
        End(g);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 4, "gained stats persist across turns");
        Summon(p, "CS2_065");
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 7, "friendly Demon summoned on opponent turn still qualifies");
        End(g);
        Kill(g, source);
        StandardSetsProof.ParkBoard(p); Summon(p, "CS2_065");
        Core2026Proof.Check(source.AttackDamage == 5, "dead source never gains more stats");
    }
}
