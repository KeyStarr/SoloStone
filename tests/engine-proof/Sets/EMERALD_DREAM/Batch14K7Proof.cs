using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Batch14K7EmeraldDreamProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_421", Omen);

    // PINNED: data/rules-evidence/combat/dh-omen-damage-upgrade-step.yaml.
    // Tier C Omen Notes + independent tier E vS: 1, 2, 3 damage. Tier A 32.0.2/32.4 fixes preserve copy/Dormant progress.
    private static void Omen(Game g, Controller p, string id)
    {
        foreach (int attacks in new[] { 0, 1, 2 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            var source = (Minion)Play(branch, owner, id);
            Core2026Proof.Check(source.IsRush && source.HasWindfury && source.AttackDamage == 6 && source.Health == 12
                && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "exact 6/12 Rush Windfury, initial Deathrattle one");
            for (int i = 0; i < attacks; i++) Attack(branch, source, Summon(owner.Opponent, "CS2_182"));
            Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 + attacks, "PinnedRule_AddOnePerAttack: 1,2,3; not doubling");
            var enemy = Summon(owner.Opponent, "EX1_561");
            var friendly = Summon(owner, "EX1_561"); Summon(owner, "CS2_052");
            int heroDamage = owner.Opponent.Hero.Damage;
            var copy = (Minion)Generic.Copy(owner, owner.Hero, source, Zone.PLAY, preserveBoardMinionState: true);
            branch.ProcessTasks(); branch.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 + attacks, "board copy retains independent displayed damage");
            foreach (var next in new[] { branch, branch.CloneExact(history: true), Game.LoadSnapshot(branch.SaveSnapshot()) })
            {
                var body = (Minion)next.IdEntityDic[source.Id]; var duplicate = (Minion)next.IdEntityDic[copy.Id];
                duplicate.Silence(); Kill(next, duplicate);
                Core2026Proof.Check(next.Player2.Hero.Damage == heroDamage, "Silence suppresses copied Deathrattle");
                Kill(next, body);
                Core2026Proof.Check(next.Player2.Hero.Damage == heroDamage + attacks + 1
                    && ((Minion)next.IdEntityDic[enemy.Id]).Damage == attacks + 1
                    && ((Minion)next.IdEntityDic[friendly.Id]).Damage == 0,
                    "actual death hits every enemy, excludes friendlies and ignores Spell Damage");
            }
        }
        var omen = (Minion)Play(g, p, id);
        Attack(g, omen, Summon(p.Opponent, "CS2_182"));
        var copy2 = (Minion)Generic.Copy(p, p.Hero, omen, Zone.PLAY, preserveBoardMinionState: true);
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Attack(g, copy2, Summon(p.Opponent, "CS2_182"));
        Core2026Proof.Check(copy2[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && omen[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "copy increments do not change original");
        Core2026Proof.Check(DormantRules.Make(copy2, 1, "EDR_979e", p.Hero) && copy2.IsDormant,
            "official Dormant-retention regression fixture");
        Core2026Proof.Check(copy2[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "going Dormant preserves Deathrattle display");
        End(g);
        Attack(g, Summon(p.Opponent, "CS2_182"), omen);
        Core2026Proof.Check(omen[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "being attacked does not improve Deathrattle");
        End(g);
        Core2026Proof.Check(!copy2.IsDormant && copy2[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "32.4 fix: awakening preserves accumulated Omen damage");
        // An attack with lethal retaliation still completes before the normal after-attack/death checkpoint.
        var lethal = (Minion)Play(g, p, id); lethal.Damage = 11;
        int before = p.Opponent.Hero.Damage;
        Attack(g, lethal, Summon(p.Opponent, "CS2_182"));
        Core2026Proof.Check(lethal.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == before + 2, "completed lethal combat improves before Deathrattle");
    }
}
