#if DEBUG
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Pooling;

namespace RelicStats.Patches;

// The tests start each fight with the debug fight command while the previous fight is still in
// progress. That frees the old combat room, and with it any pooled card nodes that were still
// children of it, but they stay in the pool's free list. The next card play that takes one of them
// crashes with ObjectDisposedException inside NCard.Create, the play aborts, and the test times out
// waiting for CardPlayed. Drop disposed nodes from the free list before the pool hands one out.
[HarmonyPatch(typeof(NodePool<NCard>), nameof(NodePool<NCard>.Get))]
public static class TestNodePoolPatch
{
    private static readonly System.Reflection.FieldInfo FreeObjectsField =
        AccessTools.Field(typeof(NodePool<NCard>), "_freeObjects");

    public static void Prefix(NodePool<NCard> __instance)
    {
        if (FreeObjectsField?.GetValue(__instance) is not List<NCard> free) return;
        int removed = free.RemoveAll(n => !GodotObject.IsInstanceValid(n));
        if (removed > 0)
            MainFile.Logger.Info($"[NodePool] dropped {removed} disposed NCard node(s) from the pool");
    }
}
#endif
