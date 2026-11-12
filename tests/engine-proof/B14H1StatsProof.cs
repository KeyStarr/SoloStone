using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static B14K3BEProof;

internal static class B14H1StatsProof
{
    internal static readonly string[] Ids = { "CORE_EX1_414", "CORE_OG_218", "CATA_613", "JAIL_459", "END_022", "CATA_487", "END_024", "TLC_482", "CORE_OG_044", "TIME_021" };
    internal static Game NewGame(bool history = true)
    {
        var g = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = history, Logging = false, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var c in new[] { g.Player1, g.Player2 }) { PoolPilotProof.EmptyHand(c); c.BaseMana = 10; }
        return g;
    }
    [GroupProof("b14-h1-stats")]
    internal static void Run()
    {
        var failures = new List<string>();
        void Test(string name, Action<Game, Controller> check)
        {
            foreach (bool history in new[] { false, true })
            try { var g = NewGame(history); check(g, g.Player1); Console.WriteLine("PASS h1 " + name + " history=" + history); }
            catch (Exception e) { failures.Add(name + " history=" + history + ": " + e.Message); }
        }
        Test("EnrageSilence", (g,p) => {
            var m = Summon(p,"EX1_414"); m.Damage=1; g.AuraUpdate();
            Core2026Proof.Check(m.AttackDamage==10,"damaged Grommash gains six"); m.Silence(); g.AuraUpdate();
            Core2026Proof.Check(m.AttackDamage==4,"silenced Grommash stays at four, no late subtraction/reapplication");
        });
        Test("WeaponEnrageHealing", (g,p) => {
            Play(g,p,"CS2_106"); var smith=Summon(p,"CS2_221");
            int attack=p.Hero.Weapon.AttackDamage; smith.Damage=1; g.AuraUpdate();
            Core2026Proof.Check(p.Hero.Weapon.AttackDamage==attack+2,"Smith buffs actual weapon");
            smith.TakeFullHeal(p.Hero); g.AuraUpdate();
            Core2026Proof.Check(p.Hero.Weapon.AttackDamage==attack && smith.AttackDamage==4,"healing Smith removes weapon bonus, not minion Attack");
        });
        Test("WeaponEnrageReplacement", (g,p) => {
            Play(g,p,"CS2_106"); var smith=Summon(p,"CS2_221");
            smith.Damage=1; g.AuraUpdate();
            Play(g,p,"CS2_106");
            int replacementAttack=p.Hero.Weapon.AttackDamage;
            foreach (var future in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var owner=future.Player1;
                ((Minion)future.IdEntityDic[smith.Id]).TakeFullHeal(owner.Hero); future.AuraUpdate();
                Core2026Proof.Check(owner.Hero.Weapon.AttackDamage==3,"healing Smith after weapon replacement preserves base three Attack");
            }
            Core2026Proof.Check(replacementAttack==5,"damaged Smith reapplies bonus to replacement weapon");
        });
        Test("PoisonousAura", (g,p) => {
            var m=Summon(p,"CS2_182"); new Effect(GameTag.POISONOUS,EffectOperator.ADD,1).ApplyAuraTo(m);
            Core2026Proof.Check(m.Poisonous,"aura Poisonous is effective");
        });
        Test("ImmuneAura", (g,p) => {
            var m=Summon(p,"CS2_182"); new Effect(GameTag.IMMUNE,EffectOperator.ADD,1).ApplyAuraTo(m);
            Core2026Proof.Check(m.IsImmune,"aura Immune is effective");
        });
        Test("ChooseBothSources", (g,p) => {
            var a=Summon(p,"OG_044"); var b=Summon(p,"OG_044");
            a.Silence(); g.AuraUpdate(); Core2026Proof.Check(p.ChooseBoth,"second Fandral survives first Silence");
            b.Silence(); g.AuraUpdate(); Core2026Proof.Check(!p.ChooseBoth,"last Fandral removal clears Choose Both");
        });
        Test("HealingConversionSources", (g,p) => {
            var a=Summon(p,"EX1_591"); var b=Summon(p,"EX1_591"); a.Silence(); g.AuraUpdate();
            Core2026Proof.Check(p.RestoreToDamage,"second Auchenai survives first Silence");
            p.Hero.Damage=2; p.Hero.TakeHeal(p.Hero.HeroPower,1);
            Core2026Proof.Check(p.Hero.Damage==3,"remaining conversion deals damage");
            b.Silence(); g.AuraUpdate(); Core2026Proof.Check(!p.RestoreToDamage,"last converter removed");
        });
        Test("SaturatingDamage", (g,p) => {
            var victim=Summon(p.Opponent,"CS2_182"); victim.Damage=1;
            var spell=Add(p,"CS2_029"); Summon(p,"CS2_052");
            Core2026Proof.Check(SpellDamageRules.Amount(spell,int.MaxValue)==int.MaxValue,"spell bonus saturates");
            victim.TakeDamage(spell,int.MaxValue); Core2026Proof.Check(victim.Damage==int.MaxValue && victim.ToBeDestroyed,"existing damage cannot wrap negative");
            p.Opponent.Hero.DamageTakenThisTurn=int.MaxValue-1; p.Opponent.Hero.TakeDamage(p.Hero.HeroPower,2);
            Core2026Proof.Check(p.Opponent.Hero.DamageTakenThisTurn==int.MaxValue,"damage counter saturates");
            p.NumHeroPowerDamageThisGame=int.MaxValue-1; p.Opponent.Hero.TakeDamage(p.Hero.HeroPower,2);
            Core2026Proof.Check(p.NumHeroPowerDamageThisGame==int.MaxValue,"hero-power counter saturates");
            spell.HasLifeSteal=true; p.Hero.Damage=1; p.Hero.TakeDamage(spell,int.MaxValue);
            g.DeathProcessingAndAuraUpdate();
            var restored=Game.LoadSnapshot(g.SaveSnapshot());
            Core2026Proof.Check(restored.Player1.Hero.Damage==0,"saturated self-hit Lifesteal preserves snapshot fact links");
        });
        Core2026Proof.Check(failures.Count==0,string.Join("\n",failures));
    }
}
