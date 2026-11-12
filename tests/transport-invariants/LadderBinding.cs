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
    }
}
