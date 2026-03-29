#if DEBUG
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using RelicStats.Core.Testing;

namespace RelicStats.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
public static class TestCombatStartPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.CombatStart);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
public static class TestPlayerTurnStartPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.PlayerTurnStart);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
public static class TestCombatVictoryPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.CombatVictory);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
public static class TestSideTurnStartPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.SideTurnStart);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
public static class TestCombatEndPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.CombatEnd);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeTurnEnd))]
public static class TestTurnEndPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.TurnEnd);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterTurnEnd))]
public static class TestAfterTurnEndPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.AfterTurnEnd);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
public static class TestCardPlayedPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.CardPlayed);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted))]
public static class TestCardExhaustedPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.CardExhausted);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDiscarded))]
public static class TestCardDiscardedPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.CardDiscarded);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterShuffle))]
public static class TestShufflePatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.Shuffle);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived))]
public static class TestDamageReceivedPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.DamageReceived);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
public static class TestDeathPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.Death);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterGoldGained))]
public static class TestGoldGainedPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.GoldGained);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionUsed))]
public static class TestPotionUsedPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.PotionUsed);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterRoomEntered))]
public static class TestRoomEnteredPatch
{
    public static void Postfix()
    {
        TestManager.CheckTimeouts();
        TestManager.Signal(GameEvent.RoomEntered);
    }
}
#endif
