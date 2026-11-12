using System.Reflection;

namespace TransportInvariants
{
    // LocalLadder binds one save per process (1df07ebe); proofs that drive real ladder settlement each bind their own
    // disposable profile, so clear the previous proof's binding first instead of leaking it into the next proof.
    internal static class LadderBinding
    {
        internal static void Bind(LocalProfile profile)
        {
            foreach (string name in new[] { "profile", "collectionUi" })
                typeof(LocalLadder).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            LocalLadder.Initialize(profile);
        }

        // The state of one finished match (result, terminal facts, flags). A proof that settles a match through the real
        // LocalLadder clears it afterwards so that the next proof does not see a match that has already been settled.
        internal static void ClearMatch()
        {
            foreach (string name in new[] { "active", "result", "terminalOutcome", "error", "terminalFacts", "terminalPetXp",
                         "terminalAchievementEvidence", "terminalTurns", "conceded", "nativeFinished" })
            {
                var field = typeof(LocalLadder).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
        }
    }
}
