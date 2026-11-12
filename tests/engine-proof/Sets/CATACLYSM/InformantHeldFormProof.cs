using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class InformantHeldFormProof
{
    internal static void Unpinned_Informant(Game g, Controller p, string id)
    {
        // Literal xoroshiro fixtures catch fixed cycling, wrong candidate order and shared copy state.
        var expected = g.Seed switch {
            1 => new[] { CardClass.HUNTER, CardClass.WARRIOR, CardClass.DEATHKNIGHT, CardClass.MAGE, (CardClass)14 },
            2 => new[] { CardClass.HUNTER, CardClass.SHAMAN, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.DRUID },
            3 => new[] { CardClass.SHAMAN, CardClass.DEATHKNIGHT, CardClass.WARLOCK, CardClass.MAGE, CardClass.ROGUE },
            _ => throw new ArgumentException("Informant proof requires seed 1..3")
        };
        g.SetRandomSeed(g.Seed);
        var rng = g.Random.Clone(); rng.Next(11);
        var held = Add(p, id); int selected = held[GameTag.TAG_SCRIPT_DATA_NUM_1];
        Core2026Proof.Check(selected == (int)expected[0] && g.Random.DrawCount == 1 && g.Random.CaptureState().Equals(rng.CaptureState()),
            "Unpinned_InformantClassChoice: exact seeded initialization and one draw, seed=" + g.Seed);
        var copy = Generic.Copy(p, p.Hero, held, Zone.HAND);
        Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == selected && g.Random.DrawCount == 1 && g.Random.CaptureState().Equals(rng.CaptureState()),
            "Unpinned_InformantCopyState: direct copy inherits class with zero draws");
        End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == selected && copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == selected &&
            g.Random.CaptureState().Equals(rng.CaptureState()), "assumed opponent start consumes no class RNG");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var expectedRng = rng.Clone();
            for (int turn = 0; turn < 2; turn++)
            {
                expectedRng.Next(10); expectedRng.Next(10);
                End(next);
                Core2026Proof.Check(next.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == (int)expected[1 + 2 * turn] &&
                    next.IdEntityDic[copy.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == (int)expected[2 + 2 * turn] &&
                    next.Random.DrawCount == 3 + 2 * turn && next.Random.CaptureState().Equals(expectedRng.CaptureState()),
                    "Unpinned_InformantClassChoice: exact independent seeded rerolls and two draws after clone/load, seed=" + g.Seed + ", turn=" + turn);
                if (turn == 0)
                {
                    End(next);
                    Core2026Proof.Check(next.Random.CaptureState().Equals(expectedRng.CaptureState()), "opponent start does not reroll either copy");
                }
            }
            var card = next.IdEntityDic[held.Id]; int cls = card[GameTag.TAG_SCRIPT_DATA_NUM_1];
            PlayEntity(next, next.Player1, card);
            var choice = next.Player1.Choice;
            Core2026Proof.Check(choice != null && choice.Choices.Count == 3 && choice.Choices.Select(i => next.IdEntityDic[i].Card.Id).Distinct().Count() == 3 &&
                choice.Choices.All(i => next.IdEntityDic[i].Card.Class != CardClass.NEUTRAL &&
                    CardClassRules.IsAllowedInDeck(next.IdEntityDic[i].Card, (CardClass)cls) && next.IdEntityDic[i].Card.Type == CardType.SPELL),
                "three distinct spells match selected class: " + cls + ", offers=" +
                (choice == null ? "none" : string.Join(",", choice.Choices.Select(i => next.IdEntityDic[i].Card.Id + ":" + next.IdEntityDic[i].Card.Class))));
            int chosen = choice.Choices[0];
            while (!next.Player1.HandZone.IsFull) Add(next.Player1, "CS2_182");
            PlayerDecisionProof.Answer(next, chosen);
            Core2026Proof.Check(next.Player1.Choice == null && next.Player1.HandZone.Count == 10 &&
                next.IdEntityDic[chosen].Zone != next.Player1.HandZone, "full-hand Discover burns overflow and settles without retry");
        }
    }
}
