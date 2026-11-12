using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14K6Proof
{
    [GroupProof("b14-k6-rogue-warlock-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_093", Mindrot);
        B14K6LostCityProof.Run(); B14K6JailProof.Run();
        B14K6CataclysmProof.Run();
    }
    private static void Mindrot(Game g, Controller p, string id)
    {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_231"); var far = Summon(p, "CS2_200");
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = PlayAt(g, p, id, 1);
        Core2026Proof.Check(left.Card.Id == id && right.Card.Id == id && left.AttackDamage == 4 && left.Health == 5
            && right.AttackDamage == 4 && right.Health == 5, "both adjacent minions transformed to exact source");
        Core2026Proof.Check(far.Card.Id == "CS2_200" && enemy.Card.Id == "CS2_182" && p.BoardZone.SlotCount == 4, "nonadjacent/opponent unchanged");
        left.Silence(); Core2026Proof.Check(left.Card.Id == id && left.AttackDamage == 4, "Silence does not undo a transform");
        ParkBoard(p);
        var blocked = Summon(p, "CS2_182"); var location = LocationProofs.Location(p);
        var second = PlayAt(g, p, id, 2);
        Core2026Proof.Check(blocked.Card.Id == "CS2_182" && location.Card.Type == CardType.LOCATION, "location blocks physical adjacency");
        ParkBoard(p);
        var dormant = Summon(p, "EDR_469"); var awake = Summon(p, "CS2_231");
        PlayAt(g,p,id,1);
        Core2026Proof.Check(dormant.Card.Id == "EDR_469" && dormant.IsDormant && awake.Card.Id == id,
            "Dormant neighbor unchanged; awake neighbor transformed");
        ParkBoard(p);
        var untouchable = Summon(p, "UNG_829t2"); var beyond = Summon(p, "CS2_182");
        PlayAt(g,p,id,0);
        Core2026Proof.Check(untouchable.Card.Id == "UNG_829t2" && beyond.Card.Id == "CS2_182",
            "untouchable neighbor blocks adjacency without transforming");
        ParkBoard(p); Play(g,p,id); Core2026Proof.Check(p.BoardZone.Single().Card.Id == id, "empty neighbors no summons");
        ParkBoard(p);
        var buffLeft=Summon(p,"CS2_182");var buffRight=Summon(p,"CS2_231");var buffed=Add(p,id);
        Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_004e"),p.Hero,buffed);
        p.UsedMana=0;
        Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p,buffed,zonePosition:1)),"buffed Subjugator play");
        Core2026Proof.Check(buffLeft.Health==7 && buffRight.Health==7 && buffLeft.AppliedEnchantments.Any(e=>e.Card.Id=="CS2_004e"),"transform copies present source buffs once");
        buffLeft.Silence();Core2026Proof.Check(buffLeft.Health==5 && buffRight.Health==7,"copied attachment independent under Silence");
        ParkBoard(p);
        for(int i=0;i<6;i++) Summon(p,"CS2_182");
        PlayAt(g,p,id,3); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m=>m.Card.Id==id)==3, "full board transforms in place");
    }
}
