using System;
using Assets;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static partial class NativeArena
{
    private static void InstallRetailGuards(Harmony harmony)
    {
        // Replace instance calls at their use sites: a null retail singleton must never be dereferenced offline.
        foreach (var method in new[] {
            Required(typeof(DraftManager), "OnBegin"), Required(typeof(DraftManager), "OnAckRewards"),
            Required(typeof(DraftManager), "OnArenaSessionResponsePacket", new[] { typeof(PegasusUtil.ArenaSessionResponse), typeof(bool) }),
            Required(typeof(DraftDisplay), "HandleGameStartupFailure"),
            AccessTools.EnumeratorMoveNext(Required(typeof(DraftDisplay), "InitializeDraftScreen")),
            AccessTools.EnumeratorMoveNext(Required(typeof(DraftDisplay), "NotifySceneLoadedWhenReady")) })
            harmony.Patch(method ?? throw new MissingMethodException("Arena coroutine MoveNext"),
                transpiler: new HarmonyMethod(typeof(NativeArena), nameof(RetailCalls)));
    }
    private static IEnumerable<CodeInstruction> RetailCalls(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var code in instructions)
        {
            if (code.operand is MethodInfo method)
            {
                string replacement = method.DeclaringType == typeof(BnetPresenceMgr) && method.Name == "SetGameFieldBlob" ? nameof(PresenceBlob)
                    : method.DeclaringType == typeof(PresenceMgr) && method.Name == "SetStatus" ? nameof(PresenceStatus)
                    : method.DeclaringType == typeof(PresenceMgr) && method.Name == "SetPrevStatus" ? nameof(PreviousPresence)
                    : method.DeclaringType == typeof(PresenceMgr) && method.Name == "get_CurrentStatus" ? nameof(CurrentPresence)
                    : method.DeclaringType == typeof(AchieveManager) && method.Name == "IsReady" ? nameof(AchievementReadiness) : null;
                if (replacement != null)
                {
                    code.opcode = OpCodes.Call;
                    code.operand = Required(typeof(NativeArena), replacement);
                }
            }
            yield return code;
        }
    }
    private static bool PresenceBlob(BnetPresenceMgr manager, uint id, IProtoBuf blob) => !Offline && manager.SetGameFieldBlob(id, blob);
    private static bool PresenceStatus(PresenceMgr manager, Enum[] status) => !Offline && manager.SetStatus(status);
    private static bool PreviousPresence(PresenceMgr manager) => !Offline && manager.SetPrevStatus();
    private static Global.PresenceStatus CurrentPresence(PresenceMgr manager) => Offline ? Global.PresenceStatus.ARENA_IDLE : manager.CurrentStatus;
    private static bool AchievementReadiness(AchieveManager manager) => Offline || manager.IsReady();
}
