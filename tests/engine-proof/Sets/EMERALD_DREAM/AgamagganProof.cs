using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AgamagganProof
{
    internal static void Unpinned_OpponentPayment(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var source = p.BoardZone.Single(m => m.Card.Id == id); source.Silence(); Kill(g, source);
        End(g); End(g);
        var next = Add(p, "CS2_182"); p.Opponent.Hero.Armor = 12; p.Opponent.Hero.IsImmune = true;
        Core2026Proof.Check((int)CostRules.PaymentKind(next) == 4 && next[GameTag.CARD_ALTERNATE_COST] == 4, "native opponent Health quote");
        Core2026Proof.Check((int)CostRules.PaymentKind(p.Hero.HeroPower) == 0
            && (int)CostRules.PaymentKind(Add(p.Opponent, "CS2_182")) == 0, "grant survives source/turns; excludes Hero Powers and opponent cards");
        var costly = Add(p, "CS2_182"); costly.Cost = 11;
        Core2026Proof.Check(!CostRules.CanPay(p, CostRules.Quote(p, costly)) && !p.Options().Any(t => t.Source == costly), "PINNED costs over ten prohibited");
        var prepare = Add(p, "CS2_182"); PrepareRules.Grant(prepare); p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p, prepare)) && (int)CostRules.PaymentKind(next) == 4, "PINNED Prepare never consumes grant");
        PoolPilotProof.Resume(g, branch => {
            var owner = branch.Player1; owner.UsedMana = 0;
            int damageFacts = branch.HistoryFacts.Query.Damage().Count();
            Core2026Proof.Check(branch.Process(PlayCardTask.Any(owner, branch.IdEntityDic[next.Id])), "next card accepted without mana");
            Core2026Proof.Check(owner.Opponent.Hero.Health == 26 && owner.Opponent.Hero.Armor == 12 && owner.UsedMana == 0
                && branch.HistoryFacts.Query.Damage().Count() == damageFacts, "Unpinned payment bypasses Armor/Immune without damage facts");
            Core2026Proof.Check((int)CostRules.PaymentKind(branch.IdEntityDic[costly.Id]) == 0, "grant consumed exactly once");
            Core2026Proof.Check(Canonical.State(branch) == Canonical.State(Game.LoadSnapshot(branch.SaveSnapshot())), "opponent payment history snapshot validates");
        });
        foreach (int health in new[] { 4, 5 })
        {
            var branch = DeathKnightC1Proof.NewGame(g.Seed); var owner = branch.Player1;
            Play(branch, owner, id); owner.Opponent.Hero.Health = health;
            var lethal = Add(owner, "CS2_200"); lethal.Cost = 10;
            int board = owner.BoardZone.Count;
            Core2026Proof.Check(branch.Process(PlayCardTask.Any(owner, lethal)) && branch.State == State.COMPLETE
                && owner.Opponent.Hero.Health <= 0 && owner.BoardZone.Count == board, "Unpinned lethal overpayment settles before minion/effect");
            Core2026Proof.Check(Canonical.State(branch) == Canonical.State(branch.CloneExact(history: true)), "terminal payment clone validates");
        }
        var zeroGame = DeathKnightC1Proof.NewGame(g.Seed); var b = zeroGame.Player1;
        Play(zeroGame, b, id); var zero = Add(b, "CS2_231");
        Core2026Proof.Check(zeroGame.Process(PlayCardTask.Any(b, zero)) && b.Opponent.Hero.Health == 30
            && b.AppliedEnchantments.All(e => e.Card.Id != "EDR_489e1"), "zero cost consumes grant without Health loss");
    }
}
