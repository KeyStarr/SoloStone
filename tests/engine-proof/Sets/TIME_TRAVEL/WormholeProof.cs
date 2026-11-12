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

internal static class WormholeProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_602", (g, p, id) => {
        EmptyPool(g, p, id);
        WithCards(g, new[] { "CORE_EX1_014" }, () => {
            p.Opponent.Hero.Damage = 0;
            Resolve(g, p, id);
            var beast = p.BoardZone.Single();
            Core2026Proof.Check(beast.Card.Id == "CORE_EX1_014" && beast.AttackDamage == 5 && p.Opponent.Hero.Damage == 5
                && p.Opponent.HandZone.IsEmpty, "3-Cost Beast attacks singleton hero immediately; summon does not run Battlecry");
            ParkBoard(p); p.Opponent.Hero.Damage = 0;
            var defender = Summon(p.Opponent, "CS2_186"); defender.HasTaunt = true; defender.CantBeTargetedBySpells = true;
            int before = g.IdEntityDic.Values.Max(entity => entity.Id);
            Resolve(g, p, id);
            beast = p.BoardZone.Concat(p.GraveyardZone.OfType<Minion>()).Single(m => m.Id > before && m.Card.Id == "CORE_EX1_014");
            Core2026Proof.Check((defender.Damage == 5 && beast.Damage == 7) || p.Opponent.Hero.Damage == 5,
                "forced attack chooses enemy character, ignores Taunt/Elusive, applies retaliation");
            ParkBoard(p); ParkBoard(p.Opponent);
            for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            LocationProofs.Location(p); long draws = g.Random.DrawCount;
            Resolve(g, p, id);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.All(m => m.Card.Id == "CS2_182")
                && g.Random.DrawCount == draws, "full mixed board no generation or attack");
            ParkBoard(p);
        });
        Resume(g, next => {
            var player = next.Player1;
            var before = next.IdEntityDic.Values.Select(entity => entity.Id).ToHashSet();
            int damage = player.Opponent.Hero.Damage;
            var source = Play(next, player, id); RewindTestHelpers.KeepIfOffered(next);
            var beasts = player.BoardZone.Concat(player.GraveyardZone.OfType<Minion>()).Where(m => !before.Contains(m.Id)).ToArray();
            Generated(next, source, beasts, 1);
            var beast = beasts.Single();
            Core2026Proof.Check(beast.Card.IsRace(Race.BEAST) && beast.Card.Cost == 3 && beast.NumAttacksThisTurn == 0
                && player.Opponent.Hero.Damage >= damage + beast.AttackDamage,
                "installed pool creates one new eligible Beast attributed to Wormhole and it attacks lone enemy hero without spending normal allowance");
        });
    });
}
