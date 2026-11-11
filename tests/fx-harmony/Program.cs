using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static class Program
{
    private static int Main()
    {
        try
        {
            NativeFxTelemetry.Run();
            // The sub-spell pin reads the client's own library and the plugin built against it (run.py builds the plugin when a client is set up).
            if (File.Exists(PinnedSubSpellHooks.ClientLibrary) && File.Exists(PinnedSubSpellHooks.BuiltPlugin)) PinnedSubSpellHooks.Run();
            else Console.WriteLine("SKIP pinned sub-spell hooks: needs the client's Assembly-CSharp.dll (run.py --client) and the plugin built against it");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

// Native fixture: cancellation is a ref output, including when native execution throws.
internal sealed class InputManager
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DropHeldSpellLikeCard(object card, object entity, ref bool cancelDrop)
    {
        cancelDrop = true;
        if (entity is Exception error) throw error;
    }
}

// Native fixture: the diagnostics-only state hook reads its enum through int storage.
internal sealed class Spell
{
    private FixtureState m_activeStateType;
    internal FixtureState State => m_activeStateType;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ChangeStateImpl(FixtureState state) { m_activeStateType = state; }
}

internal static class NativeTestControl { internal static bool DiagnosticsEnabled; }

internal partial class NativeFxTelemetry
{
    private static readonly List<string> installed = new List<string>(), failed = new List<string>();
    private static readonly FieldInfo spellState = typeof(Spell).GetField("m_activeStateType", BindingFlags.Instance | BindingFlags.NonPublic);
    private static Func<bool> configured;
    private static bool recording = true;
    private static object[] observedArgs;
    private static object observedResult;
    private static Exception observedException;
    private static void Warn(string message) { Console.Error.WriteLine(message); }
    private static void Observe(MethodBase method, object instance, object[] args, object result, Exception exception)
    { if (recording) { observedArgs = args; observedResult = result; observedException = exception; } }
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }

    internal static void Run()
    {
        // A finalizer requesting the magic argument array can overwrite native ref outputs in HarmonyX 2.9.0.
        foreach (var method in typeof(NativeFxTelemetry).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name.Contains("Finalizer")))
            Check(method.ReturnType == typeof(void) && method.GetParameters().All(parameter =>
                parameter.Name != "__args" && !parameter.ParameterType.IsByRef), "passive signature: " + method.Name);
        Check(typeof(NativeFxTelemetry).GetMethod("DropFinalizer", BindingFlags.Static | BindingFlags.NonPublic)
            .GetParameters().All(parameter => parameter.Name != "__2" && parameter.Name != "cancelDrop"),
            "drop never binds cancellation output");
        Check(typeof(Harmony).Assembly.GetName().Version == new Version(2, 9, 0, 0), "shipped HarmonyX 2.9.0");
        var harmony = new Harmony("test.native-fx.ref-cancellation");
        try
        {
            Hook(harmony, typeof(InputManager), "DropHeldSpellLikeCard", typeof(void), typeof(object), typeof(object), typeof(bool).MakeByRefType());
            Check(failed.Count == 0 && installed.Count == 1, "production registration installs drop finalizer");
            foreach (var method in typeof(FxFixture).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var arguments = method.GetParameters().Select(parameter => parameter.ParameterType == typeof(FixtureState)
                    ? (object)FixtureState.Power : parameter.ParameterType == typeof(FixtureAssetReference)
                        ? new FixtureAssetReference { Guid = "prefab-guid" } : new object()).ToArray();
                Hook(harmony, typeof(FxFixture), method.Name, method.ReturnType,
                    method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
                Check(failed.Count == 0, "production registration: " + method.Name);
                object result = method.Invoke(null, arguments);
                Check(observedArgs.SequenceEqual(arguments) && Equals(observedResult, result) && observedException == null,
                    "by-value arguments and result: " + method.Name);
            }
            var input = new InputManager();
            object card = new object(), entity = new object();
            bool cancelDrop = false;
            input.DropHeldSpellLikeCard(card, entity, ref cancelDrop);
            Check(cancelDrop, "caller observes native cancelDrop=true");
            Check(observedArgs.SequenceEqual(new[] { card, entity }) && observedResult == null,
                "drop source observed without cancellation output or invented success result");
            recording = false;
            cancelDrop = false;
            input.DropHeldSpellLikeCard(card, entity, ref cancelDrop);
            Check(cancelDrop, "caller observes cancellation with observer disabled");
            recording = true;
            var failure = new InvalidOperationException("native failure");
            cancelDrop = false;
            Exception caught = null;
            try { input.DropHeldSpellLikeCard(card, failure, ref cancelDrop); }
            catch (Exception error) { caught = error; }
            Check(cancelDrop && ReferenceEquals(caught, failure) && ReferenceEquals(observedException, failure),
                "native exception identity and ref output survive finalizer");
            var spell = new Spell();
            Hook(harmony, typeof(Spell), "ChangeStateImpl", typeof(void), typeof(FixtureState));
            Check(failed.Count == 0, "production registration: ChangeStateImpl");
            observedArgs = null;
            spell.ChangeStateImpl(FixtureState.Power);
            Check(spell.State == FixtureState.Power && observedArgs == null, "state hook observes nothing without diagnostics");
            configured = () => true;
            spell.ChangeStateImpl(FixtureState.Power);
            Check(observedArgs != null && observedArgs.SequenceEqual(new object[] { FixtureState.Power }), "configured diagnostics observe the boxed native state");
            configured = null;
            observedArgs = null;
            NativeTestControl.DiagnosticsEnabled = true;
            spell.ChangeStateImpl(FixtureState.Power);
            Check(observedArgs != null && observedArgs.SequenceEqual(new object[] { FixtureState.Power }), "test-control diagnostics observe the boxed native state");
        }
        finally { harmony.UnpatchSelf(); }
    }
}

internal enum FixtureState { Power = 3 }
internal struct FixtureAssetReference { internal string Guid; }
internal static class FxFixture
{
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Void0() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Void1(FixtureState state) { }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Void2(object effect, object card) { }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void Void3(FixtureAssetReference reference, object prefab, object callbackContext) { }
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool Bool0() => true;
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool Bool1(object task) => false;
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool Bool2(object effect, object card) => true;
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool Bool3(object state, object task, object entity) => false;
    [MethodImpl(MethodImplOptions.NoInlining)] public static object Object1(object path) => path;
    [MethodImpl(MethodImplOptions.NoInlining)] public static object Object2(object card, object task) => card;
}
