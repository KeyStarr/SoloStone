using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class FlamespitterProof
{
    private static void HeroAttack(Game g, Controller p)
    {
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(task != null && g.Process(task), "hero attack accepted");
    }
    private static IPlayable[] Overdrives(Controller p) => p.HandZone.Where(c => c.Card.Id == "BE_553t").ToArray();
    private static bool CanAttack(Controller p, Minion m, ICharacter target) =>
        p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target?.Id == target.Id);

    internal static void Run() => StandardSetsProof.Case("BE_552", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId("BE_553t").Type == CardType.SPELL && Cards.FromId("BE_553t").Cost == 0
            && Cards.FromId("BE_553e").Type == CardType.ENCHANTMENT, "pinned Overdrive token and Burning Out enchantment");
        var weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon == weapon && weapon.AttackDamage == 2 && weapon.Durability == 2, "2/2 weapon equipped");
        var clone = g.CloneExact(history: true);
        HeroAttack(clone, clone.Player1);
        Core2026Proof.Check(Overdrives(clone.Player1).Length == 1 && Overdrives(p).Length == 0, "clone trigger isolated");
        HeroAttack(g, p);
        var overdrive = Overdrives(p).Single();
        Core2026Proof.Check(overdrive[GameTag.DISPLAYED_CREATOR] == weapon.Id && overdrive.Cost == 0, "after hero attack: one Overdrive");

        // Minion attacks and opponent hero attacks do not trigger.
        var attacker = Summon(p, "CS2_182"); Attack(g, attacker, p.Opponent.Hero);
        Core2026Proof.Check(Overdrives(p).Length == 1, "minion attack does not trigger");

        // Overdrive targets friendly Mechs only.
        var mech = Summon(p, "GVG_044"); var dragonMech = Summon(p, "EX1_025t"); var enemyMech = Summon(p.Opponent, "GVG_044");
        var plain = Summon(p, "CS2_182");
        p.BaseMana = 10; p.UsedMana = 0;
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == overdrive.Id).Select(t => t.Target?.Id).ToArray();
        Core2026Proof.Check(targets.Contains(mech.Id) && targets.Contains(dragonMech.Id) && !targets.Contains(enemyMech.Id) && !targets.Contains(plain.Id)
            && !targets.Contains(null) && !targets.Contains(p.Hero.Id), "friendly Mech targets only (multi-race included)");

        // Fresh Mech: +2 Attack and Rush (minions only), dies at end of turn.
        Core2026Proof.Check(!CanAttack(p, mech, enemyMech), "fresh Mech cannot attack before Overdrive");
        PlayEntity(g, p, overdrive, mech);
        Core2026Proof.Check(mech.AttackDamage == 5 && mech.Health == 4 && mech.IsRush && mech.AppliedEnchantments.Any(e => e.Card.Id == "BE_553e"), "+2 Attack and Rush");
        Core2026Proof.Check(CanAttack(p, mech, enemyMech) && !CanAttack(p, mech, p.Opponent.Hero), "granted Rush: minions only");

        // Silenced fresh Mech: loses Rush (exhausted again) and survives the turn end.
        var silenced = Summon(p, "GVG_044");
        PlayEntity(g, p, Add(p, "BE_553t"), silenced);
        silenced.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!silenced.IsRush && silenced.AttackDamage == 3 && !CanAttack(p, silenced, enemyMech), "silence removes Rush/Attack; fresh minion exhausted");
        End(g);
        Core2026Proof.Check(mech.Zone == p.GraveyardZone && silenced.Zone == p.BoardZone, "Overdriven Mech dies at end of turn; silenced one survives");
        Core2026Proof.Check(enemyMech.Zone == p.Opponent.BoardZone, "enemy Mech untouched");

        // Opponent hero attack does not trigger our weapon.
        Play(g, p.Opponent, "CS2_106");
        var enemyAttack = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Hero);
        Core2026Proof.Check(g.Process(enemyAttack) && Overdrives(p).Length == 0, "opponent hero attack does not trigger");
        End(g);

        // Older Mech can attack heroes as usual; it still dies at end of this turn.
        PlayEntity(g, p, Add(p, "BE_553t"), dragonMech);
        Core2026Proof.Check(dragonMech.AttackDamage == 4 && CanAttack(p, dragonMech, p.Opponent.Hero), "older Mech keeps normal readiness");

        // Last durability: trigger still resolves before the weapon breaks; full hand burns the Overdrive.
        FillHand(p);
        HeroAttack(g, p);
        Core2026Proof.Check(p.Hero.Weapon == null && weapon.Zone == p.GraveyardZone && p.HandZone.Count == 10 && Overdrives(p).Length == 0
            && p.GraveyardZone.Any(c => c.Card.Id == "BE_553t"), "final attack: weapon breaks; full hand burns the Overdrive");
        End(g);
        Core2026Proof.Check(dragonMech.Zone == p.GraveyardZone, "older Overdriven Mech dies at end of turn");
        End(g);

        // A destroyed weapon's trigger is gone: a replacement weapon attack gives nothing.
        EmptyHand(p);
        Play(g, p, "CS2_106"); HeroAttack(g, p);
        Core2026Proof.Check(p.HandZone.Count == 0, "no Overdrive without Flamespitter");

        // A bounce must remove Burning Out, not leave its end-turn death subscribed in hand.
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var returned = Summon(p, "BE_550");
        Play(g, p, "BE_553t", returned);
        Core2026Proof.Check(Generic.ReturnToHandBlock(p, returned), "Overdriven Mech returned to hand");
        Core2026Proof.Check(returned.Zone == p.HandZone && returned.AppliedEnchantments.Count == 0
            && returned.AttackDamage == returned.Card.ATK && !returned.IsRush, "bounce removes Burning Out and its granted stats/keyword");
        Resume(g, next => {
            var copy = (Minion)next.IdEntityDic[returned.Id];
            End(next);
            Core2026Proof.Check(copy.Zone == next.Player1.HandZone && !copy.ToBeDestroyed
                && !next.HistoryFacts.Query.Deaths().Any(f => f.EntityId == copy.Id), "bounced Mech survives turn end without a false death");
        });
    });
}
