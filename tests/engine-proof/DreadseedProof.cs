using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class DreadseedProof
{
    internal const string Leans = "Unpinned_DreadseedPool Unpinned_DormantRecipeOrder Unpinned_DormantReawaken";
    internal static readonly string[] Ids = { "EDR_840t", "EDR_840t1", "EDR_840t2" };
    internal static void Closure()
    {
        foreach (var (id, dbf) in new[] { ("EDR_840t", 114655), ("EDR_840t1", 114658), ("EDR_840t2", 114661),
            ("EDR_840t1e", 114656), ("EDR_840te", 114659), ("EDR_840t1e1", 114660), ("EDR_840t2e1", 114662) })
            EmeraldDreamCardStateProof.Token(id, dbf);
    }
    internal static void Sleeping(Minion m)
    {
        int turns = m.Card.Id == "EDR_840t" ? 2 : m.Card.Id == "EDR_840t1" ? 1 : 3;
        Core2026Proof.Check(Ids.Contains(m.Card.Id) && m.IsDormant && DormantRules.TurnsRemaining(m) == turns
            && m.IsRace(Race.BEAST) && m.IsRace(Race.UNDEAD) && m.Damage == 0, Leans + ": explicit token starts asleep with exported duration and races");
    }
    internal static void Armor(Game g, Controller p, Minion serpent)
    {
        int entity = serpent.Id;
        Resume(g, next => {
            var m = (Minion)next.IdEntityDic[entity];
            for (int turn = 1; turn <= 3; turn++)
            {
                End(next); Core2026Proof.Check(m.IsDormant, "opponent start does not tick Serpent");
                End(next); Core2026Proof.Check(m.IsDormant == (turn < 3), "Serpent wakes on third owner start");
            }
            Core2026Proof.Check(m.HasLifeSteal && m.HasTaunt && m.AttackDamage == 5 && m.BaseHealth == 5, "awake printed 5/5 Taunt Lifesteal");
            m.Controller.Hero.Damage = 12; m.Controller.Opponent.Hero.Armor = 8;
            int damage = m.Controller.Opponent.Hero.Damage;
            Attack(next, m, m.Controller.Opponent.Hero);
            Core2026Proof.Check(m.Controller.Hero.Damage == 7 && m.Controller.Opponent.Hero.Armor == 3
                && m.Controller.Opponent.Hero.Damage == damage, "Lean_LifestealArmorDamage: generated Serpent heals five Armor damage");
        });
    }
    // Real plays must reach each fixed token; direct token summons cannot substitute for parent coverage.
    internal static void Outcomes(Game g, Controller p, Func<Minion[]> summon, int expected)
    {
        var seen = new HashSet<string>(); bool armor = false;
        for (int i = 0; i < 32 && seen.Count < 3; i++)
        {
            ParkBoard(p); EmptyHand(p);
            var children = summon();
            Core2026Proof.Check(children.Length == expected, "exact Dreadseed summon count");
            foreach (var child in children) { Sleeping(child); seen.Add(child.Card.Id); }
            var serpent = children.FirstOrDefault(m => m.Card.Id == "EDR_840t2");
            if (!armor && serpent != null) { Armor(g, p, serpent); armor = true; }
        }
        Core2026Proof.Check(seen.SetEquals(Ids) && armor, Leans + ": all three fixed outcomes reached, including real generated Armor Lifesteal");
        ParkBoard(p); EmptyHand(p);
    }
    internal static void HoundAndCrow(Game g, Controller p)
    {
        var hound = Summon(p, "EDR_840t"); var crow = Summon(p, "EDR_840t1");
        End(g); End(g);
        Core2026Proof.Check(hound.IsDormant && !crow.IsDormant && crow.CantBeTargetedBySpells && crow.CantBeTargetedByHeroPowers,
            "Crow wakes after one owner start with Elusive; Hound waits");
        End(g); End(g);
        Core2026Proof.Check(!hound.IsDormant && p.Hero.AttackDamage == 3 && p.Hero.AppliedEnchantments.Any(e => e.Card.Id == "EDR_840te"),
            "Hound awakening grants exported one-turn +3 Attack");
        Core2026Proof.Check(!DormantRules.Awaken(hound) && p.Hero.AttackDamage == 3, "no duplicate awakening grant");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "Hound's Attack expires at own end"); End(g);
        Core2026Proof.Check(DormantRules.Make(hound, 1, "EDR_840t1e1", hound), "re-dormancy accepted");
        End(g); End(g);
        Core2026Proof.Check(!hound.IsDormant && p.Hero.AttackDamage == 3, Leans + ": later genuine awakening grants again");
        ParkBoard(p); End(g); End(g);
    }
}
