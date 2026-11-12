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

internal static class DruidOfRegrowthProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_033", (g, p, id) => {
        EmptyPool(g, p, id);
        WithCards(g, new[] { "CORE_CS2_009" }, () => {
            var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            enemy.CantBeTargetedBySpells = true;
            int mana = p.UsedMana;
            Resolve(g, p, id);
            Core2026Proof.Check(friendly.AttackDamage == 8 && friendly.Health == 11 && friendly.HasTaunt
                && enemy.AttackDamage == 4 && !enemy.HasTaunt && p.UsedMana == mana, "two Nature effect casts choose only legal random target without payment");
            ParkBoard(p); ParkBoard(p.Opponent);
            long draws = g.Random.DrawCount; Resolve(g, p, id);
            Core2026Proof.Check(g.Random.DrawCount == draws + 2 && p.BoardZone.IsEmpty, "no legal target skips both casts after pool picks");
        });
        WithCards(g, new[] { "CORE_EX1_169" }, () => {
            p.UsedMana = 5; p.TemporaryMana = 0; Resolve(g, p, id);
            Core2026Proof.Check(p.TemporaryMana == 2, "untargeted Nature spells cast twice");
        });
        WithCards(g, new[] { "CORE_EX1_259" }, () => {
            var creeper = Summon(p.Opponent, "FP1_002");
            Resolve(g, p, id);
            var spiders = p.Opponent.GraveyardZone.OfType<Minion>().Where(m => m.Card.Id == "FP1_002t").ToArray();
            Core2026Proof.Check(creeper.Zone.Type == Zone.GRAVEYARD && p.Opponent.BoardZone.IsEmpty && spiders.Length == 2,
                "first Lightning Storm resolves Creeper Deathrattle before second Storm kills both Spiders");
        });
        p.TemporaryMana = 0;
        Resume(g, next => { Play(next, next.Player1, id); RewindTestHelpers.KeepIfOffered(next); });
        ParkBoard(p); ParkBoard(p.Opponent);
    });
}
