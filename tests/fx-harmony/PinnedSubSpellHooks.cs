using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class PinnedSubSpellHooks
{
    internal static void Run()
    {
        void Check(bool value, string name) { if (!value) throw new Exception(name); }
        using (var hash = System.Security.Cryptography.SHA256.Create())
            Check(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("references/game/Assembly-CSharp.dll"))).Replace("-", "").ToLowerInvariant()
                == "786376bddad544709639d4b3ab60ce8799d4b1c6c7d7fc4c225ce79fe07228c6", "pinned client assembly hash");
        using (var native = ModuleDefinition.ReadModule("references/game/Assembly-CSharp.dll"))
        using (var plugin = ModuleDefinition.ReadModule("plugin/bin/Release/net48/HSNativeUIProbe.dll"))
        {
            var sub = native.Types.Single(t => t.Name == "SubSpellController");
            var instance = sub.NestedTypes.Single(t => t.Name == "SubSpellInstance");
            foreach (string field in new[] { "SubSpellStartTask", "SpellInstance", "SpellLoaded" })
                Check(instance.Fields.Any(f => f.Name == field), "pinned sub-spell field " + field);
            foreach (var row in new[] {
                (Name: "GetSubSpellInstanceForTasklist", Result: "SubSpellController/SubSpellInstance", Args: new[] { "PowerTaskList" }),
                (Name: "OnSubSpellLoadAttempted", Result: "System.Void", Args: new[] { "AssetReference", "UnityEngine.GameObject", "System.Object" }),
                (Name: "AttachTasklistToSubSpell", Result: "System.Boolean", Args: new[] { "PowerTaskList", "SubSpellController/SubSpellInstance" }),
                (Name: "OnSubSpellFinished", Result: "System.Void", Args: new[] { "Spell", "System.Object" }),
                (Name: "OnSubSpellStateFinished", Result: "System.Void", Args: new[] { "Spell", "SpellStateType", "System.Object" }) })
                Check(sub.Methods.Any(m => m.Name == row.Name && m.ReturnType.FullName == row.Result
                    && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(row.Args)), "pinned hook " + row.Name);
            var telemetry = plugin.Types.Single(t => t.Name == "NativeFxTelemetry");
            var install = telemetry.Methods.Single(m => m.Name == "InstallHooks");
            foreach (string name in new[] { "GetSubSpellInstanceForTasklist", "OnSubSpellLoadAttempted", "AttachTasklistToSubSpell", "OnSubSpellFinished", "OnSubSpellStateFinished" })
                Check(install.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == name), "production installs " + name);
            foreach (var method in telemetry.Methods.Where(m => m.Name.Contains("Finalizer")))
                Check(method.ReturnType.FullName == "System.Void" && method.Parameters.All(p => p.Name != "__args" && !p.ParameterType.IsByReference),
                    "compiled passive finalizer " + method.Name);
            foreach (var method in telemetry.Methods.Where(m => m.HasBody))
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Stsfld)
                    Check(((FieldReference)instruction.Operand).DeclaringType.Scope.Name != "Assembly-CSharp", "no native field writes");
                if (instruction.Operand is MethodReference called && called.DeclaringType.Scope.Name == "Assembly-CSharp")
                    Check(!new[] { "GetSpell", "CreateSpell", "InstantiatePrefab", "ActivateState", "AttachTasklistToSubSpell", "AttachPowerTaskList" }.Contains(called.Name),
                        "telemetry never drives native spell lifecycle " + called.FullName);
            }
        }
        Console.WriteLine("PASS pinned sub-spell declarations, installed hooks and compiled passive IL");
    }
}
