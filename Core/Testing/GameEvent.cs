#if DEBUG
namespace RelicStats.Core.Testing;

public enum GameEvent
{
    CombatStart,
    PlayerTurnStart,
    SideTurnStart,
    CombatVictory,
    CombatEnd,
    CardPlayed,
    TurnEnd,
    AfterTurnEnd,
    CardExhausted,
    CardDiscarded,
    Shuffle,
    DamageReceived,
    Death,
    GoldGained,
    PotionUsed,
    RoomEntered,
}
#endif
