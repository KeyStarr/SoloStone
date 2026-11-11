using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B14K1TimeTravelProof
{
    internal static void Run() => StandardSetsProof.Case("END_034", Crumblecrusher);
    private static void Crumblecrusher(Game g, Controller p, string id)
    {
        bool laterMinion = false, laterLocation = false;
        foreach (bool reverse in new[] { false, true })
        foreach (int seed in new[] { 1, 2, 3 })
        {
            ForcedCardProof.Clean(g);
            var friendly = Summon(p, "CS2_182"); var ownLocation = LocationProofs.Location(p);
            Play(g, p, "CS2_106"); var ownWeapon = p.Hero.Weapon;
            var first = Summon(p.Opponent, "CS2_182");
            // Duplicate definitions remain distinct random candidates; physical order differs from age.
            var second = (Minion)Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
            SabberStoneCore.Actions.Generic.SummonBlock(g, second, reverse ? 0 : -1, null);
            var dormant = Summon(p.Opponent, "TIME_046");
            var a = LocationProofs.Location(p.Opponent);
            var b = LocationProofs.Location(p.Opponent, position: reverse ? 0 : -1);
            new SabberStoneCore.Tasks.SimpleTasks.WeaponTask("CS2_106", opponent: true).Process(g, p, p.Hero, null);
            var weapon = p.Opponent.Hero.Weapon;
            g.SetRandomSeed(seed); var oracle = g.CloneExact(history: true);
            int[] minions = p.Opponent.BoardZone.Where(m => !m.Untouchable).Select(m => m.Id).ToArray();
            int[] locations = p.Opponent.BoardZone.GetLocations().Select(m => m.Id).ToArray();
            int expectedMinion = minions[oracle.Random.Next(minions.Length)];
            int expectedLocation = locations[oracle.Random.Next(locations.Length)];
            laterMinion |= expectedMinion != minions[0]; laterLocation |= expectedLocation != locations[0];
            PoolPilotProof.Resume(g, next => {
                Play(next, next.Player1, id);
                Core2026Proof.Check(next.IdEntityDic[expectedMinion].Zone == next.Player2.GraveyardZone
                    && next.IdEntityDic[expectedLocation].Zone == next.Player2.GraveyardZone
                    && next.Player2.BoardZone.Count == 2 && next.Player2.BoardZone.LocationCount == 1
                    && next.Player2.Hero.Weapon == null && next.IdEntityDic[weapon.Id].Zone == next.Player2.GraveyardZone,
                    "seeded separate minion/location picks follow physical order, then enemy weapon destroyed");
                Core2026Proof.Check(next.IdEntityDic[dormant.Id].Zone == next.Player2.BoardZone
                    && next.IdEntityDic[friendly.Id].Zone == next.Player1.BoardZone
                    && next.IdEntityDic[ownLocation.Id].Zone == next.Player1.BoardZone
                    && next.Player1.Hero.Weapon.Id == ownWeapon.Id, "Dormant and every friendly category unaffected");
            });
        }
        Core2026Proof.Check(laterMinion && laterLocation, "random tests reject always-first choices for both categories");
        ForcedCardProof.Clean(g);
        if (p.Opponent.Hero.Weapon != null) p.Opponent.Hero.Weapon.Destroy();
        g.DeathProcessingAndAuraUpdate();
        var loc = LocationProofs.Location(p.Opponent); Play(g, p, id);
        Core2026Proof.Check(loc.Zone == p.Opponent.GraveyardZone, "missing minion and weapon do not suppress location destruction");
        ForcedCardProof.Clean(g);
        new SabberStoneCore.Tasks.SimpleTasks.WeaponTask("CS2_106", opponent: true).Process(g, p, p.Hero, null);
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Weapon == null, "weapon-only board still destroys weapon");
        ForcedCardProof.Clean(g);
        var alone = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(alone.Zone == p.Opponent.GraveyardZone, "minion-only board still destroys minion");
        StandardSetsProof.ParkBoard(p);
        var enemy = Summon(p.Opponent, "CS2_182"); Summon(p, id);
        Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone, "summon does not run Battlecry");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == id, "no enemies resolves safely");
    }
}
