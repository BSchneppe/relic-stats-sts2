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
    /// <summary>Hook.ModifyRewards ran: room-end rewards (gold, relics, card rewards) exist now. Fires well after CombatVictory.</summary>
    RewardsGenerated,
    /// <summary>Hook.AfterCardChangedPiles ran, e.g. a card finished entering the permanent deck.</summary>
    CardChangedPiles,
}
#endif
