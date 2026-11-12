using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;

internal static class B16StatGainChampionProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_330", Unpinned_ChampionGainClassification);
    private static void Stats(Minion m, int atk, int health, string why) => Core2026Proof.Check(m.AttackDamage == atk && m.BaseHealth == health, why + $": got {m.AttackDamage}/{m.BaseHealth}");
    private static void Unpinned_ChampionGainClassification(Game game, Controller player, string id)
    {
        foreach (bool history in new[] { false, true })
        foreach (bool enemy in new[] { false, true })
        foreach (var zone in new[] { Zone.PLAY, Zone.HAND, Zone.DECK })
        {
            var g = B14H1StatsProof.NewGame(history); var p = enemy ? g.Player2 : g.Player1;
            var champion = zone == Zone.PLAY ? Summon(p, id) : (Minion)Entity.FromCard(p, Cards.FromId(id), zone: zone == Zone.HAND ? p.HandZone : p.DeckZone);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_092e"), p.Hero, champion);
            Stats(champion, 8, 8, "combined +4/+4 grants exactly one bonus");
            new ApplyEffectTask(EntityType.TARGET, Effects.Attack_N(2)).Process(g,p,p.Hero,champion);
            Stats(champion,11,9,"Attack-only gain grants one bonus");
            new ApplyEffectTask(EntityType.TARGET, Effects.Health_N(2)).Process(g,p,p.Hero,champion);
            Stats(champion,12,12,"Health-only gain grants one bonus, including Power Word: Shield category");
            new ApplyEffectTask(EntityType.TARGET, Effects.Attack_N(2), Effects.Health_N(-1)).Process(g,p,p.Hero,champion);
            Stats(champion,15,12,"mixed gain is one operation");
            new Enchant(Effects.SetAttackHealth(1)).ActivateTo(champion); Stats(champion,1,1,"downward setter no bonus");
            new Enchant(Effects.SetAttackHealth(5)).ActivateTo(champion); Stats(champion,6,6,"upward combined setter one bonus");
            new Enchant(Effects.SetAttackHealth(6)).ActivateTo(champion); Stats(champion,6,6,"unchanged setter no bonus");
            new Enchant(Effects.Attack_N(2)).ActivateTo(champion); new Enchant(Effects.Health_N(2)).ActivateTo(champion);
            Stats(champion,10,10,"two separately resolved grants get two bonuses");
            var temp = new Enchant(Effects.Attack_N(2)) { IsOneTurnEffect = true }; temp.ActivateTo(champion);
            Stats(champion,13,11,"temporary Attack grants permanent bonus");
            End(g); Stats(champion,11,11,"temporary expiry keeps only extra +1/+1");
            var aura = Effects.Attack_N(4); aura.ApplyAuraTo(champion); Stats(champion,15,11,"aura adds no bonus");
            new Enchant(Effects.Health_N(1)).ActivateTo(champion); Stats(champion,16,13,"direct gain excludes aura layer");
            aura.RemoveAuraFrom(champion); Stats(champion,12,13,"aura removal no bonus");
            champion.Damage = 1; champion.TakeFullHeal(p.Hero); Stats(champion,12,13,"damage/heal no bonus");
            new SwapAttackHealthTask(EntityType.TARGET,"EX1_059e").Process(g,p,p.Hero,champion);
            Stats(champion,14,13,"swap positive Attack delta earns one bonus");
            new ApplyEffectTask(EntityType.TARGET, Effects.SetCost(0)).Process(g,p,p.Hero,champion);
            Stats(champion,14,13,"cost change no bonus");
            foreach (var future in B16StatGainEventProof.Futures(g))
            {
                var live = (Minion)future.IdEntityDic[champion.Id];
                new Enchant(Effects.AttackHealth_N(1)).ActivateTo(live); Stats(live,16,15,"listener survives clone/snapshot independently");
                var copy = (Minion)Generic.Copy(live.Controller, live.Controller.Hero, live, live.Zone.Type == Zone.PLAY ? Zone.PLAY : Zone.HAND, preserveBoardMinionState: live.Zone.Type == Zone.PLAY);
                Stats(copy,16,15,"copy construction does not trigger bonus");
                new Enchant(Effects.AttackHealth_N(1)).ActivateTo(copy); Stats(copy,18,17,"copy listens independently");
                Stats(live,16,15,"copy gain does not affect original");
                live.Silence(); int attack=live.AttackDamage, healthBefore=live.BaseHealth;
                new Enchant(Effects.AttackHealth_N(1)).ActivateTo(live); Stats(live,attack+1,healthBefore+1,"silenced source no bonus in any zone");
                live.Controller.SetasideZone.Add(live.Zone.Remove(live));
                new Enchant(Effects.AttackHealth_N(1)).ActivateTo(live); Stats(live,attack+2,healthBefore+2,"setaside has no listener");
            }
        }
        // Vanguard draws the only minion and buffs it; rewind must not retain attempt-one bonuses.
        foreach(var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
        var c=(Minion)Entity.FromCard(player,Cards.FromId(id),zone:player.DeckZone);
        Play(game,player,"TIME_003");
        var rewind=game.Rewind;
        Core2026Proof.Check(rewind != null,"Champion draw/buff opens real Rewind");
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(rewind.OfferChoiceId,rewind.ControllerId,new[]{rewind.RewindEntityId}),out _),"Rewind accepted");
        var current=(Minion)game.IdEntityDic[c.Id];
        Stats(current,6,6,"Rewind leaves one +2/+2 and one bonus");
    }
}
