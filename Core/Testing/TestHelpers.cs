#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace RelicStats.Core.Testing;

public static class TestHelpers
{
    private static readonly Lazy<FightConsoleCmd> _fightCmd = new(() => new FightConsoleCmd());
    private static readonly Lazy<WinConsoleCmd> _winCmd = new(() => new WinConsoleCmd());
    private static readonly Lazy<CardConsoleCmd> _cardCmd = new(() => new CardConsoleCmd());
    private static readonly Lazy<EnergyConsoleCmd> _energyCmd = new(() => new EnergyConsoleCmd());
    private static readonly Lazy<GoldConsoleCmd> _goldCmd = new(() => new GoldConsoleCmd());
    private static readonly Lazy<HealConsoleCmd> _healCmd = new(() => new HealConsoleCmd());
    private static readonly Lazy<DamageConsoleCmd> _damageCmd = new(() => new DamageConsoleCmd());
    private static readonly Lazy<BlockConsoleCmd> _blockCmd = new(() => new BlockConsoleCmd());
    private static readonly Lazy<DrawConsoleCmd> _drawCmd = new(() => new DrawConsoleCmd());
    private static readonly Lazy<ApplyPowerConsoleCmd> _powerCmd = new(() => new ApplyPowerConsoleCmd());
    private static readonly Lazy<GodModeConsoleCmd> _godModeCmd = new(() => new GodModeConsoleCmd());
    private static readonly Lazy<RoomConsoleCmd> _roomCmd = new(() => new RoomConsoleCmd());
    private static readonly Lazy<EnchantConsoleCmd> _enchantCmd = new(() => new EnchantConsoleCmd());
    private static readonly Lazy<UpgradeCardConsoleCmd> _upgradeCmd = new(() => new UpgradeCardConsoleCmd());
    private static readonly Lazy<StarsConsoleCmd> _starsCmd = new(() => new StarsConsoleCmd());

    public static Player? Player { get; set; }

    public static void AddRelic(string relicId)
    {
        if (Player == null) return;
        var id = relicId.ToUpperInvariant();
        var relicModel = ModelDb.AllRelics.FirstOrDefault(r => r.Id.Entry == id);
        if (relicModel == null) return;
        Player.AddRelicInternal(relicModel.ToMutable(), silent: true);
    }

    public static void RemoveRelic(string relicId)
    {
        if (Player == null) return;
        var id = relicId.ToUpperInvariant();
        var relic = Player.Relics.FirstOrDefault(r => r.Id.Entry == id);
        if (relic == null) return;
        Player.RemoveRelicInternal(relic, silent: true);
    }

    public static void StartFight(string encounterId = "NIBBITS_WEAK")
    {
        _fightCmd.Value.Process(Player, new[] { encounterId });
    }

    public static void WinCombat()
    {
        Callable.From(() =>
        {
            try
            {
                if (CombatManager.Instance?.IsInProgress == true)
                    _winCmd.Value.Process(Player, Array.Empty<string>());
            }
            catch { /* combat may already be ending */ }
        }).CallDeferred();
    }

    /// <summary>
    /// Spawns a card synchronously into a combat pile (hand by default).
    /// Uses CombatState.CreateCard (registers with _allCards) + pile.AddInternal (subscribes to StateTracker).
    /// Card is immediately available for PlayCard.
    /// </summary>
    public static void SpawnCard(string cardId, string pile = "hand")
    {
        if (Player == null) return;
        var id = cardId.ToUpperInvariant();
        var cardModel = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == id)
            ?? ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry.StartsWith(id))
            ?? ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry.Contains(id));
        if (cardModel == null) return;

        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null) return;

        var card = combatState.CreateCard(cardModel, Player);
        var pileType = Enum.Parse<PileType>(pile, ignoreCase: true);
        pileType.GetPile(Player).AddInternal(card);
        MainFile.Logger.Info($"[SpawnCard] {card.Id.Entry} added to {pileType}, hand count={PileType.Hand.GetPile(Player).Cards.Count}");
    }

    public static void AddEnergy(int amount = 1)
    {
        _energyCmd.Value.Process(Player, new[] { amount.ToString() });
    }

    public static void AddGold(int amount)
    {
        _goldCmd.Value.Process(Player, new[] { amount.ToString() });
    }

    public static void Heal(int amount)
    {
        _healCmd.Value.Process(Player, new[] { amount.ToString() });
    }

    public static void DealDamage(int amount)
    {
        var a = amount;
        Callable.From(() => _damageCmd.Value.Process(Player, new[] { a.ToString() })).CallDeferred();
    }

    public static void DealDamageToPlayer(int amount)
    {
        var a = amount;
        Callable.From(() => _damageCmd.Value.Process(Player, new[] { a.ToString(), "0" })).CallDeferred();
    }

    public static void GiveBlock(int amount, int targetIndex = 0)
    {
        _blockCmd.Value.Process(Player, targetIndex == 0
            ? new[] { amount.ToString() }
            : new[] { amount.ToString(), targetIndex.ToString() });
    }

    /// <summary>
    /// Gives the first enemy massive HP so it survives card plays.
    /// Sets MaxHp and CurrentHp synchronously (no async console command).
    /// </summary>
    public static void ProtectEnemy()
    {
        if (Player == null) return;
        var enemies = Player.Creature.CombatState?.HittableEnemies;
        if (enemies == null || enemies.Count == 0)
        {
            MainFile.Logger.Info("[ProtectEnemy] No enemies found!");
            return;
        }
        foreach (var enemy in enemies)
        {
            enemy.SetMaxHpInternal(999999);
            enemy.SetCurrentHpInternal(999999);
        }
        MainFile.Logger.Info($"[ProtectEnemy] Protected {enemies.Count} enemies");
    }

    public static void DrawCards(int amount)
    {
        _drawCmd.Value.Process(Player, new[] { amount.ToString() });
    }

    public static void ApplyPower(string powerId, int amount = 1, int targetIndex = 0)
    {
        _powerCmd.Value.Process(Player, new[] { powerId, amount.ToString(), targetIndex.ToString() });
    }

    public static void EnableGodMode()
    {
        _godModeCmd.Value.Process(Player, Array.Empty<string>());
    }

    /// <summary>
    /// Disables GodMode if it's currently active (toggle off).
    /// Checks the internal _godModeActive flag to avoid toggling it ON.
    /// </summary>
    public static void DisableGodMode()
    {
        var field = AccessTools.Field(typeof(GodModeConsoleCmd), "_godModeActive");
        if (field != null && (bool)field.GetValue(_godModeCmd.Value)!)
            _godModeCmd.Value.Process(Player, Array.Empty<string>()); // toggles OFF
    }

    private static bool _pendingEndTurn;

    private static int _endTurnRetries;

    /// <summary>
    /// Cancels any pending EndTurn from a previous test to prevent stale timer interference.
    /// Called at the start of each test by TestManager.
    /// </summary>
    public static void CancelPendingEndTurn()
    {
        _pendingEndTurn = false;
        _endTurnRetries = 0;
        _endTurnTimer = null;
    }

    public static void EndTurn()
    {
        if (Player == null) return;
        _pendingEndTurn = true;
        _endTurnRetries = 0;
        TryEndTurn();
        if (_pendingEndTurn)
            ScheduleEndTurnRetry();
    }

    private static SceneTreeTimer? _endTurnTimer;

    private static void ScheduleEndTurnRetry()
    {
        if (!_pendingEndTurn || _endTurnRetries >= 120) return;
        _endTurnRetries++;
        // Use a SceneTreeTimer instead of CallDeferred — deferred calls may not fire
        // frequently enough when the game is waiting for player input
        _endTurnTimer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05);
        _endTurnTimer.Timeout += () =>
        {
            TryEndTurn();
            if (_pendingEndTurn)
                ScheduleEndTurnRetry();
        };
    }

    /// <summary>
    /// Called from event patches to check if a deferred EndTurn should fire.
    /// </summary>
    public static void TryEndTurn()
    {
        try
        {
            if (!_pendingEndTurn || Player == null) return;
            var cm = CombatManager.Instance;
            if (cm == null || !cm.IsInProgress) return;
            if (!cm.IsPlayPhase)
            {
                MainFile.Logger.Info($"[TryEndTurn] retry {_endTurnRetries}: IsPlayPhase=false");
                return;
            }
            // Ensure it's actually the player's turn — IsPlayPhase can be true during side transitions
            var state = cm.DebugOnlyGetState();
            if (state == null || state.CurrentSide != CombatSide.Player)
            {
                MainFile.Logger.Info($"[TryEndTurn] retry {_endTurnRetries}: CurrentSide={state?.CurrentSide}");
                return;
            }
            _pendingEndTurn = false;
            cm.SetReadyToEndTurn(Player, canBackOut: false);
            MainFile.Logger.Info($"[TryEndTurn] ended turn successfully");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[TryEndTurn] exception: {ex.Message}");
        }
    }

    public static void ClearPlayerPowers()
    {
        if (Player == null) return;
        try { Player.Creature.RemoveAllPowersInternalExcept(); }
        catch { /* may not be in combat */ }
    }

    public static void DiscardHand()
    {
        if (Player == null) return;
        var hand = PileType.Hand.GetPile(Player);
        if (hand == null) return;
        var discardPile = PileType.Discard.GetPile(Player);
        foreach (var card in hand.Cards.ToArray())
        {
            hand.RemoveInternal(card, silent: true);
            discardPile.AddInternal(card, -1, silent: true);
        }
    }

    /// <summary>
    /// Finds the first card of the given type in hand, or any card if type is null.
    /// Returns the hand index, or -1 if not found.
    /// </summary>
    public static int FindCardInHand(CardType? type = null)
    {
        if (Player == null) return -1;
        var hand = PileType.Hand.GetPile(Player);
        if (hand == null) return -1;
        for (int i = 0; i < hand.Cards.Count; i++)
        {
            if (type == null || hand.Cards[i].Type == type)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Plays the first attack in hand targeting the first enemy. Falls back to spawning a STRIKE.
    /// </summary>
    public static void PlayAttack()
    {
        var idx = FindCardInHand(CardType.Attack);
        if (idx >= 0)
        {
            PlayCard(idx, 0);
        }
        else
        {
            SpawnCard("STRIKE");
            // Card spawn is async — can't play immediately. Caller must WaitFor an event then play.
        }
    }

    /// <summary>
    /// Plays the first skill in hand. Falls back to spawning a DEFEND.
    /// </summary>
    public static void PlaySkill()
    {
        var idx = FindCardInHand(CardType.Skill);
        if (idx >= 0)
            PlayCard(idx);
        else
            SpawnCard("DEFEND");
    }

    public static void PlayCard(int handIndex = 0, int targetIndex = -1)
    {
        if (Player == null) return;
        try
        {
            var hand = PileType.Hand.GetPile(Player);
            if (hand == null || hand.Cards.Count <= handIndex) return;
            var card = hand.Cards[handIndex];
            Creature? target = null;
            if (targetIndex >= 0)
            {
                var enemies = Player.Creature.CombatState?.HittableEnemies;
                if (enemies != null && enemies.Count > targetIndex)
                    target = enemies[targetIndex];
            }
            var capturedCard = card;
            var capturedTarget = target;
            Callable.From(() => {
                var played = capturedCard.TryManualPlay(capturedTarget);
                MainFile.Logger.Info($"[PlayCard] {capturedCard.Id.Entry} idx={handIndex} target={capturedTarget?.Monster?.Id.Entry ?? "none"} played={played}");
            }).CallDeferred();
        }
        catch { /* combat may have ended */ }
    }

    // ── Event-driven card play queue ──
    // Chains card plays via Hook.AfterCardPlayed: play one card, wait for the event,
    // play the next, etc. When all cards have resolved, runs the completion callback.
    private static Queue<(int idx, int target)>? _cardPlayQueue;
    private static Action? _afterAllPlayed;

    /// <summary>
    /// Called by TestManager.Signal when CardPlayed fires.
    /// Advances the card play queue — plays the next card or runs the completion callback.
    /// </summary>
    public static void OnCardPlayed()
    {
        if (_cardPlayQueue == null) return;
        if (_cardPlayQueue.Count > 0)
        {
            // Delay next play slightly so async relic hooks (AfterCardPlayed) finish processing
            var (idx, target) = _cardPlayQueue.Dequeue();
            var timer = ((Godot.SceneTree)Godot.Engine.GetMainLoop()).CreateTimer(0.15);
            timer.Timeout += () => PlayCard(idx, target);
        }
        else
        {
            var cb = _afterAllPlayed;
            _afterAllPlayed = null;
            _cardPlayQueue = null;
            // Delay completion callback too so the last card's relic hooks settle
            var timer = ((Godot.SceneTree)Godot.Engine.GetMainLoop()).CreateTimer(0.15);
            timer.Timeout += () => cb?.Invoke();
        }
    }

    /// <summary>
    /// Plays card(s) then runs an action after all CardPlayed events have fired.
    /// Event-driven — no timers or sleeps. Cards are played high-to-low index.
    /// </summary>
    public static void PlayThenEndTurn(int cardCount = 1, int targetIndex = -1)
    {
        _cardPlayQueue = new Queue<(int, int)>();
        // Queue cards high-to-low (skip the first — it's played immediately)
        for (int i = cardCount - 2; i >= 0; i--)
            _cardPlayQueue.Enqueue((i, targetIndex));
        _afterAllPlayed = () => EndTurn();
        // Play the first card (highest index) immediately
        PlayCard(cardCount - 1, targetIndex);
    }

    /// <summary>
    /// Exhausts the first card in hand (or at the given index) via CardCmd.Exhaust.
    /// Fires Hook.AfterCardExhausted which triggers GameEvent.CardExhausted.
    /// </summary>
    public static void ExhaustCard(int handIndex = 0)
    {
        if (Player == null) return;
        var hand = PileType.Hand.GetPile(Player);
        if (hand == null || hand.Cards.Count <= handIndex) return;
        var card = hand.Cards[handIndex];
        var ctx = new HookPlayerChoiceContext(Player, LocalContext.NetId!.Value, GameActionType.Combat);
        Task task = CardCmd.Exhaust(ctx, card);
        ctx.AssignTaskAndWaitForPauseOrCompletion(task);
    }

    /// <summary>
    /// Discards the first card in hand (or at the given index) via CardCmd.Discard.
    /// Fires Hook.AfterCardDiscarded which triggers GameEvent.CardDiscarded.
    /// </summary>
    public static void DiscardCard(int handIndex = 0)
    {
        if (Player == null) return;
        var hand = PileType.Hand.GetPile(Player);
        if (hand == null || hand.Cards.Count <= handIndex) return;
        var card = hand.Cards[handIndex];
        var ctx = new HookPlayerChoiceContext(Player, LocalContext.NetId!.Value, GameActionType.Combat);
        Task task = CardCmd.Discard(ctx, card);
        ctx.AssignTaskAndWaitForPauseOrCompletion(task);
    }

    /// <summary>
    /// Triggers a shuffle by moving all draw pile cards to discard, then drawing.
    /// This calls CardPileCmd.ShuffleIfNecessary internally, firing GameEvent.Shuffle.
    /// </summary>
    public static void TriggerShuffle()
    {
        if (Player == null) return;
        var drawPile = PileType.Draw.GetPile(Player);
        var discardPile = PileType.Discard.GetPile(Player);
        // Move all draw pile cards to discard so draw pile is empty
        foreach (var card in drawPile.Cards.ToArray())
        {
            drawPile.RemoveInternal(card, silent: true);
            discardPile.AddInternal(card, -1, silent: true);
        }
        // If discard is also empty, spawn a card there so shuffle has something to work with
        if (!discardPile.Cards.Any())
            SpawnCard("STRIKE", "discard");
        // Drawing will trigger ShuffleIfNecessary since draw pile is empty
        DrawCards(1);
    }

    private static readonly Lazy<PotionConsoleCmd> _potionCmd = new(() => new PotionConsoleCmd());

    /// <summary>
    /// Adds a potion to the player's belt via console command.
    /// </summary>
    public static void AddPotion(string potionId)
    {
        _potionCmd.Value.Process(Player, new[] { potionId });
    }

    /// <summary>
    /// Uses the first potion in the player's belt by calling EnqueueManualUse.
    /// The potion is queued for use through the action system, triggering Hook.AfterPotionUsed.
    /// </summary>
    public static void UsePotion()
    {
        if (Player == null) return;
        var potion = Player.Potions.FirstOrDefault();
        if (potion == null) return;
        Callable.From(() => potion.EnqueueManualUse(Player.Creature)).CallDeferred();
    }

    /// <summary>
    /// Discards all potions from the player's belt.
    /// </summary>
    public static void ClearPotions()
    {
        if (Player == null) return;
        foreach (var potion in Player.Potions.ToArray())
            Player.DiscardPotionInternal(potion, silent: true);
    }

    /// <summary>
    /// Starts an elite encounter. Uses EnterRoomDebug which auto-detects RoomType from the encounter model.
    /// </summary>
    public static void StartEliteFight(string encounterId = "BYGONE_EFFIGY_ELITE")
    {
        _fightCmd.Value.Process(Player, new[] { encounterId });
    }

    /// <summary>
    /// Starts a boss encounter. Uses EnterRoomDebug which auto-detects RoomType from the encounter model.
    /// </summary>
    public static void StartBossFight(string encounterId = "KAISER_CRAB_BOSS")
    {
        _fightCmd.Value.Process(Player, new[] { encounterId });
    }

    // --- Room navigation ---

    public static void EnterRoom(string roomType)
    {
        Callable.From(() => _roomCmd.Value.Process(Player, new[] { roomType })).CallDeferred();
    }

    public static void EnterRestSite() => EnterRoom("RestSite");
    public static void EnterShop() => EnterRoom("Shop");

    // --- Card manipulation ---

    public static void EnchantCard(string enchantmentId, int handIndex = 0)
    {
        _enchantCmd.Value.Process(Player, new[] { enchantmentId, "1", handIndex.ToString() });
    }

    public static void UpgradeCard(int handIndex = 0)
    {
        _upgradeCmd.Value.Process(Player, new[] { handIndex.ToString() });
    }

    // --- Stars ---

    public static void AddStars(int amount)
    {
        _starsCmd.Value.Process(Player, new[] { amount.ToString() });
    }

    // --- Card to permanent deck ---

    public static void AddCardToDeck(string cardId)
    {
        _cardCmd.Value.Process(Player, new[] { cardId, "Deck" });
    }

    /// <summary>
    /// Adds a card to a combat pile via the card console command.
    /// Unlike SpawnCard, this goes through CardPileCmd.Add which fires AfterCardEnteredCombat.
    /// </summary>
    public static void AddCardToCombatPile(string cardId, string pile = "Hand")
    {
        _cardCmd.Value.Process(Player, new[] { cardId, pile });
    }

    // --- Enemy block ---

    public static void GiveEnemyBlock(int amount)
    {
        var enemies = Player?.Creature.CombatState?.HittableEnemies;
        if (enemies == null || enemies.Count == 0) return;
        // Block console command: block <amount> <targetIndex>
        // Enemy is at creature index 1
        _blockCmd.Value.Process(Player, new[] { amount.ToString(), "1" });
    }
}
#endif
