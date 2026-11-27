using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class WildY0aL3AProof
{
    [GroupProof("wild-y0a-l3a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildY0aL3ALegacyProof.Run();
        WildSetsProof.Case("CS2_073", (g, p, id) => {
            var enemy = Summon(p.Opponent, "CS2_182"); var mine = Summon(p, "CS2_182");
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held);
            Core2026Proof.Check(targets.Count == 2 && targets.All(t => t is Minion), "Cold Blood targets any minion, never a hero");
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.Health == 5 && mine.AttackDamage == 4, "Cold Blood without Combo gives +2 Attack");
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.AttackDamage == 8 && enemy.AttackDamage == 6, "Cold Blood after another card gives +4 Attack");
            End(g);
            Core2026Proof.Check(enemy.AttackDamage == 6 && mine.AttackDamage == 8, "Cold Blood is permanent");
            mine.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(mine.AttackDamage == 4, "Silence removes Cold Blood");
            End(g);
            WildY0aL3ALegacyProof.Resume(g, next => {
                var target = (Minion)next.IdEntityDic[enemy.Id];
                Play(next, next.Player1, id, target);
                Core2026Proof.Check(target.AttackDamage == 8, "Cold Blood on a fresh turn gives +2 after clone/snapshot");
            });
            Play(g, p, "CS2_008", p.Opponent.Hero); Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.AttackDamage == 10, "Cold Blood combos off any prior card, including a spell");
            End(g);
            Play(g, p.Opponent, id, enemy);
            Core2026Proof.Check(enemy.AttackDamage == 12, "Opponent's Cold Blood works on their own minion");
        });
    }
}
