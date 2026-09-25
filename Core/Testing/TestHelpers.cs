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
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

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
    private static readonly Lazy<AncientConsoleCmd> _ancientCmd = new(() => new AncientConsoleCmd());

    public static Player? Player { get; set; }

    /// <summary>
    /// Retry budget for actions that must wait for the play phase, at 0.05s per attempt.
    /// </summary>
    /// <remarks>
    /// Must stay comfortably under TestRunner.WaitFor's 5s default, which most tests use: a step
    /// that is still retrying when its WaitFor expires fails the test even though the action was
    /// about to succeed.
    /// </remarks>
    private const int MaxActionAttempts = 60;

    public static void AddRelic(string relicId)
    {
        if (Player == null) { MainFile.Logger.Warn($"[AddRelic] '{relicId}' skipped: no player"); return; }
        var id = relicId.ToUpperInvariant();
        var relicModel = ModelDb.AllRelics.FirstOrDefault(r => r.Id.Entry == id);
        // Silently doing nothing here makes the relic's whole test assert against a relic the
        // player never had, which reads as a tracking failure rather than a setup failure.
        if (relicModel == null) { MainFile.Logger.Warn($"[AddRelic] '{id}' not found in ModelDb"); return; }
        Player.AddRelicInternal(relicModel.ToMutable(), silent: true);
        var present = Player.Relics.Any(r => r.Id.Entry == id);
        MainFile.Logger.Info($"[AddRelic] {id} added, present={present}, relicCount={Player.Relics.Count}");
    }

    public static void RemoveRelic(string relicId)
    {
        if (Player == null) return;
        var id = relicId.ToUpperInvariant();
        var relic = Player.Relics.FirstOrDefault(r => r.Id.Entry == id);
        if (relic == null) return;
        Player.RemoveRelicInternal(relic, silent: true);
    }

    /// <summary>
    /// Gives the relic the way the game does on pickup, so its AfterObtained runs. <see cref="AddRelic"/>
    /// uses the silent internal add and never fires it, which is right for most tests but makes every
    /// on-pickup relic (Hefty Tablet, Neow's Bones, Pumpkin Candle's kindle…) untestable.
    /// </summary>
    public static void ObtainRelic(string relicId)
    {
        if (Player == null) { MainFile.Logger.Warn($"[ObtainRelic] '{relicId}' skipped: no player"); return; }
        var id = relicId.ToUpperInvariant();
        var relicModel = ModelDb.AllRelics.FirstOrDefault(r => r.Id.Entry == id);
        if (relicModel == null) { MainFile.Logger.Warn($"[ObtainRelic] '{id}' not found in ModelDb"); return; }
        TaskHelper.RunSafely(RelicCmd.Obtain(relicModel.ToMutable(), Player));
        MainFile.Logger.Info($"[ObtainRelic] {id} obtained, present={Player.Relics.Any(r => r.Id.Entry == id)}");
    }

    /// <summary>The player's live instance of a relic, for reading its public counters (TimesLifted, CombatsSeen…).</summary>
    public static T? GetRelic<T>() where T : RelicModel => Player?.GetRelic<T>();

    // --- Room transitions ---
    //
    // Every room change the tests make (fight, room, event, ancient, map travel) goes through
    // RunManager.EnterRoomDebug / EnterRoom. Those raise AfterRoomEntered (and CombatStart,
    // PlayerTurnStart) from inside the room entry but then keep going: EnterRoomDebug still awaits a
    // fade-in after the hooks have fired. A test that waits for RoomEntered and starts the next fight
    // straight away therefore starts a second transition on top of an unfinished one. The second
    // one's ExitCurrentRooms/EnterRoom interleave with the first, the fight is never created, and the
    // game is left in a half-built room (no background, no UI) that every later test then fails in.
    // The console commands also hand back the transition task, which was being dropped, so the
    // resulting exception was never even logged.
    //
    // So transitions are serialized here: one runs at a time, a new one waits until the previous
    // task has completed, and each task's exceptions are logged.

    private static Task? _roomTransition;
    private const int MaxTransitionWaitAttempts = 400; // 20s at 0.05s

    // Transitions waiting for the running one to finish. They count as pending too: between the
    // running task completing and the next retry tick starting the queued one there is no combat and
    // no running task, and the runner must not read that gap as "combat ended".
    private static int _queuedTransitions;

    // Bumped by CancelQueuedRoomTransitions; a queued transition from an older generation (a test
    // that has since failed or finished) is dropped instead of starting inside the next test.
    private static int _transitionGeneration;

    /// <summary>True while a harness room transition is running or queued.</summary>
    public static bool IsRoomTransitionPending => _queuedTransitions > 0 || _roomTransition is { IsCompleted: false };

    /// <summary>Drops transitions still queued by a previous test. Called by TestManager before each test.</summary>
    public static void CancelQueuedRoomTransitions()
    {
        _transitionGeneration++;
        if (_queuedTransitions > 0)
            MainFile.Logger.Info($"[RoomTransition] dropped {_queuedTransitions} queued transition(s) from the previous test");
        _queuedTransitions = 0;
    }

    private static void RunRoomTransition(string label, Func<Task?> start) =>
        RunRoomTransition(label, start, 0, _transitionGeneration);

    private static void RunRoomTransition(string label, Func<Task?> start, int attempt, int generation)
    {
        if (generation != _transitionGeneration) return; // cancelled; the counter was already reset
        if (attempt > 0) _queuedTransitions--;
        if (_roomTransition is { IsCompleted: false })
        {
            if (attempt == 0)
                MainFile.Logger.Info($"[RoomTransition] {label}: waiting for the previous transition to finish");
            if (attempt >= MaxTransitionWaitAttempts)
            {
                MainFile.Logger.Warn($"[RoomTransition] {label}: previous transition still running after {attempt} attempts; starting anyway");
            }
            else
            {
                _queuedTransitions++;
                var timer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05);
                timer.Timeout += () => RunRoomTransition(label, start, attempt + 1, generation);
                return;
            }
        }

        Task? task;
        try { task = start(); }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[RoomTransition] {label} threw: {e.Message}");
            return;
        }
        if (task == null) return;
        _roomTransition = task;
        TaskHelper.RunSafely(task);
        if (attempt > 0) MainFile.Logger.Info($"[RoomTransition] {label}: started after {attempt} waits");
    }

    /// <summary>Runs a console command whose work is a room transition, through the serializer.</summary>
    private static void RunRoomCmd(string label, AbstractConsoleCmd cmd, params string[] args) =>
        RunRoomTransition(label, () =>
        {
            var result = cmd.Process(Player, args);
            if (!result.success)
            {
                MainFile.Logger.Warn($"[Cmd] {cmd.CmdName} {string.Join(" ", args)} failed: {result.msg}");
                return null;
            }
            return result.task;
        });

    /// <summary>
    /// Enters a room directly with an explicit map point type (e.g. Unknown for Planisphere), through
    /// the transition serializer.
    /// </summary>
    public static void EnterDebugRoom(RoomType roomType, MapPointType pointType) =>
        RunRoomTransition($"room {roomType}/{pointType}",
            () => RunManager.Instance.EnterRoomDebug(roomType, pointType, null, showTransition: false));

    public static void StartFight(string encounterId = "NIBBITS_WEAK") =>
        RunRoomCmd($"fight {encounterId}", _fightCmd.Value, encounterId);

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

    /// <summary>Sets the player's gold to an exact amount (AddGold only adds).</summary>
    public static void SetGold(int amount)
    {
        if (Player == null) return;
        TaskHelper.RunSafely(PlayerCmd.SetGold(amount, Player));
    }

    /// <summary>
    /// Sets the player's current HP directly, without the heal/damage pipeline. Use it to put the
    /// player under a heal relic's threshold (Meat on the Bone, Regal Pillow) before the trigger.
    /// </summary>
    public static void SetPlayerHp(int hp)
    {
        Player?.Creature.SetCurrentHpInternal(hp);
    }

    public static void Heal(int amount)
    {
        _healCmd.Value.Process(Player, new[] { amount.ToString() });
    }

    public static void DealDamage(int amount)
    {
        var a = amount;
        Callable.From(() => RunCmd(_damageCmd.Value, a.ToString())).CallDeferred();
    }

    public static void DealDamageToPlayer(int amount)
    {
        var a = amount;
        Callable.From(() => RunCmd(_damageCmd.Value, a.ToString(), "0")).CallDeferred();
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
        var ctx = new ThrowingPlayerChoiceContext();
        foreach (var enemy in enemies)
        {
            enemy.SetMaxHpInternal(999999);
            enemy.SetCurrentHpInternal(999999);
            // GodMode gives the player ~1e9 Strength, so raw HP isn't enough to survive an attack.
            // BufferPower prevents HP loss outright (same mechanism GodMode uses to protect the player).
            TaskHelper.RunSafely(PowerCmd.Apply<BufferPower>(ctx, enemy, 999999999m, enemy, null));
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
            if (Player.PlayerCombatState?.Phase != PlayerTurnPhase.Play)
            {
                MainFile.Logger.Info($"[TryEndTurn] retry {_endTurnRetries}: Phase={Player.PlayerCombatState?.Phase}");
                return;
            }
            // Ensure it's actually the player's turn — the Play phase can briefly overlap side transitions
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
        TryPlayCard(handIndex, targetIndex, 0);
    }

    // A card can only be played during the Play phase and on the player's turn. AfterPlayerTurnStart
    // (which drives the test's PlayerTurnStart event) fires earlier, in the Start phase, so playing
    // immediately would no-op and stall the whole play/end-turn chain. Retry until the phase is right
    // and TryManualPlay actually plays the card.
    private static void TryPlayCard(int handIndex, int targetIndex, int attempt)
    {
        if (Player == null) return;
        try
        {
            var cm = CombatManager.Instance;
            bool ready = cm != null && cm.IsInProgress
                && Player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                && cm.DebugOnlyGetState()?.CurrentSide == CombatSide.Player;

            var hand = ready ? PileType.Hand.GetPile(Player) : null;
            // A card spawned in the same step may not have reached the hand yet, so treat that like
            // "not ready" and retry instead of abandoning the play silently.
            if (ready && hand != null && hand.Cards.Count > handIndex)
            {
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
                    MainFile.Logger.Info($"[PlayCard] {capturedCard.Id.Entry} idx={handIndex} target={capturedTarget?.Monster?.Id.Entry ?? "none"} played={played} attempt={attempt}");
                    if (!played && attempt < MaxActionAttempts)
                        SchedulePlayRetry(handIndex, targetIndex, attempt + 1);
                }).CallDeferred();
                return;
            }

            if (attempt < MaxActionAttempts)
            {
                if (attempt == 0 || attempt % 20 == 0)
                    MainFile.Logger.Info($"[PlayCard] retry {attempt}: Phase={Player.PlayerCombatState?.Phase}");
                SchedulePlayRetry(handIndex, targetIndex, attempt + 1);
            }
            else
            {
                MainFile.Logger.Info($"[PlayCard] gave up idx={handIndex} after {attempt} attempts (Phase={Player.PlayerCombatState?.Phase})");
            }
        }
        catch { /* combat may have ended */ }
    }

    private static void SchedulePlayRetry(int handIndex, int targetIndex, int attempt)
    {
        var timer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05);
        timer.Timeout += () => TryPlayCard(handIndex, targetIndex, attempt);
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
        // Drawing will trigger ShuffleIfNecessary since draw pile is empty. The shuffle and its
        // AfterShuffle hook complete synchronously inside the draw, which would fire the Shuffle
        // signal while the test is still inside this Do step, before its WaitFor(Shuffle) is armed.
        // Defer the draw so the runner reaches the WaitFor first.
        Callable.From(() => DrawCards(1)).CallDeferred();
    }

    private static readonly Lazy<PotionConsoleCmd> _potionCmd = new(() => new PotionConsoleCmd());

    /// <summary>
    /// Adds a potion to the player's belt via console command.
    /// </summary>
    public static void AddPotion(string potionId) => RunCmd(_potionCmd.Value, potionId);

    /// <summary>
    /// Runs a dev-console command the way DevConsole does: reporting failure and starting the task
    /// the command returns.
    /// </summary>
    /// <remarks>
    /// Calling Process directly drops that task. For commands that do their work inside it — the
    /// potion command defers to PotionCmd.TryToProcure — the potion is never actually added, and the
    /// test then waits for an event that can never fire. Most other commands here finish their work
    /// before returning, which is why only potions surfaced it.
    /// </remarks>
    private static void RunCmd(AbstractConsoleCmd cmd, params string[] args)
    {
        var result = cmd.Process(Player, args);
        if (!result.success)
        {
            MainFile.Logger.Warn($"[Cmd] {cmd.CmdName} {string.Join(" ", args)} failed: {result.msg}");
            return;
        }
        if (result.task != null) TaskHelper.RunSafely(result.task);
    }

    /// <summary>
    /// Uses the first potion in the player's belt, triggering Hook.AfterPotionUsed.
    /// </summary>
    /// <remarks>
    /// Drives PotionModel.OnUseWrapper directly, which is what UsePotionAction ends up calling.
    /// EnqueueManualUse only queues a UsePotionAction on the multiplayer ActionQueueSynchronizer,
    /// and that queue does not drain in a combat the harness jumped into with the fight command, so
    /// the potion sat queued forever and the hook never fired. Same reasoning as SpawnCard, which
    /// bypasses the draw pipeline.
    /// </remarks>
    /// <param name="potionId">The potion to use; the first potion in the belt when null.</param>
    public static void UsePotion(string? potionId = null)
    {
        if (Player == null) return;
        TryUsePotion(0, potionId?.ToUpperInvariant());
    }

    // Like PlayCard, a potion can only be used during the Play phase; retry until then.
    private static void TryUsePotion(int attempt, string? potionId = null)
    {
        if (Player == null) return;
        try
        {
            var cm = CombatManager.Instance;
            bool ready = cm != null && cm.IsInProgress
                && Player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                && cm.DebugOnlyGetState()?.CurrentSide == CombatSide.Player;
            if (ready)
            {
                // The belt may not have the potion yet: AddPotion is usually called in the same
                // step, so fall through and retry rather than giving up, which stranded the test
                // until WaitFor(PotionUsed) timed out.
                var potion = potionId == null
                    ? Player.Potions.FirstOrDefault()
                    : Player.Potions.FirstOrDefault(p => p.Id.Entry == potionId);
                if (potion != null)
                {
                    MainFile.Logger.Info($"[UsePotion] using {potion.Id.Entry} (attempt {attempt})");
                    var target = potion.IsValidTarget(Player.Creature)
                        ? Player.Creature
                        : Player.Creature.CombatState?.HittableEnemies.FirstOrDefault();
                    Callable.From(() =>
                    {
                        try
                        {
                            TaskHelper.RunSafely(potion.OnUseWrapper(new ThrowingPlayerChoiceContext(), target));
                        }
                        catch (Exception e)
                        {
                            MainFile.Logger.Warn($"[UsePotion] OnUseWrapper threw: {e.Message}");
                        }
                    }).CallDeferred();
                    return;
                }
            }
            if (attempt < MaxActionAttempts)
            {
                if (attempt % 20 == 0)
                    MainFile.Logger.Info(
                        $"[UsePotion] retry {attempt}: ready={ready} potions={Player.Potions.Count()} " +
                        $"phase={Player.PlayerCombatState?.Phase}");
                var timer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05);
                timer.Timeout += () => TryUsePotion(attempt + 1, potionId);
            }
            else
            {
                MainFile.Logger.Warn(
                    $"[UsePotion] gave up after {attempt} attempts " +
                    $"(potions={Player.Potions.Count()}, phase={Player.PlayerCombatState?.Phase})");
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[UsePotion] attempt {attempt} threw: {e.Message}");
        }
    }

    /// <summary>
    /// Discards all potions from the player's belt.
    /// </summary>
    public static void ClearPotions()
    {
        if (Player == null) return;
        // Not silent: the potion bar removes a holder only on the PotionDiscarded event. A silent
        // discard left stale holders behind, and the next potion used crashed in
        // NPotionContainer.RemoveUsed looking for a holder that was never created.
        foreach (var potion in Player.Potions.ToArray())
            Player.DiscardPotionInternal(potion, silent: false);
    }

    /// <summary>
    /// Starts an elite encounter. Uses EnterRoomDebug which auto-detects RoomType from the encounter model.
    /// </summary>
    public static void StartEliteFight(string encounterId = "BYGONE_EFFIGY_ELITE") =>
        RunRoomCmd($"fight {encounterId}", _fightCmd.Value, encounterId);

    /// <summary>
    /// Starts a boss encounter. Uses EnterRoomDebug which auto-detects RoomType from the encounter model.
    /// </summary>
    public static void StartBossFight(string encounterId = "KAISER_CRAB_BOSS") =>
        RunRoomCmd($"fight {encounterId}", _fightCmd.Value, encounterId);

    // --- Room navigation ---

    public static void EnterRoom(string roomType) =>
        RunRoomCmd($"room {roomType}", _roomCmd.Value, roomType);

    public static void EnterRestSite() => EnterRoom("RestSite");
    public static void EnterShop() => EnterRoom("Shop");

    // --- Card manipulation ---

    /// <summary>
    /// Enchants a card in hand. Unlike <see cref="SpawnCard"/> the id is not fuzzy-matched, so it
    /// must name a real enchantment; an unknown one is logged rather than silently doing nothing.
    /// </summary>
    public static void EnchantCard(string enchantmentId, int handIndex = 0)
    {
        var result = _enchantCmd.Value.Process(Player, new[] { enchantmentId, "1", handIndex.ToString() });
        if (!result.success)
            MainFile.Logger.Warn($"[EnchantCard] '{enchantmentId}' failed: {result.msg}");
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

    /// <summary>
    /// Adds a card to the permanent deck through the game's card pipeline, so the deck-add hooks fire
    /// (TryModifyCardBeingAddedToDeck synchronously, AfterCardChangedPiles after a tween: WaitFor
    /// <see cref="GameEvent.CardChangedPiles"/>). The card console command needs an EXACT id
    /// (STRIKE_IRONCLAD, DEFEND_IRONCLAD, DEMON_FORM, CLUMSY…); unlike <see cref="SpawnCard"/> there
    /// is no fuzzy match, and a bad id used to fail silently and leave the test asserting on nothing.
    /// Do this while a combat room exists: the add tween is owned by the combat room node.
    /// </summary>
    public static void AddCardToDeck(string cardId) => RunCmd(_cardCmd.Value, cardId, "Deck");

    /// <summary>
    /// Adds a card to a combat pile via the card console command (exact id, see <see cref="AddCardToDeck"/>).
    /// Unlike SpawnCard, this goes through CardPileCmd.Add which fires AfterCardEnteredCombat.
    /// </summary>
    public static void AddCardToCombatPile(string cardId, string pile = "Hand") => RunCmd(_cardCmd.Value, cardId, pile);

    // --- Events & Ancients ---

    private static readonly Lazy<EventConsoleCmd> _eventCmd = new(() => new EventConsoleCmd());

    public static void OpenAncient(string ancientId) =>
        RunRoomCmd($"ancient {ancientId}", _ancientCmd.Value, ancientId);

    public static void OpenEvent(string eventId) =>
        RunRoomCmd($"event {eventId}", _eventCmd.Value, eventId);

    // --- Enemy block ---

    public static void GiveEnemyBlock(int amount)
    {
        var enemies = Player?.Creature.CombatState?.HittableEnemies;
        if (enemies == null || enemies.Count == 0) return;
        // Block console command: block <amount> <targetIndex>
        // Enemy is at creature index 1
        _blockCmd.Value.Process(Player, new[] { amount.ToString(), "1" });
    }

    // --- Rewards ---

    /// <summary>
    /// Generates a combat card reward (3 options) exactly as the room-end reward flow does, with no
    /// screen: fires TryModifyCardRewardOptions / Late and AfterModifyingCardRewardOptions
    /// synchronously. Returns the reward so a test can also exercise its alternatives.
    /// </summary>
    public static CardReward GenerateCardReward(RoomType roomType = RoomType.Monster)
    {
        var options = CardCreationOptions.ForRoom(Player!, roomType)
            .WithFlags(CardCreationFlags.IsFromCombat | CardCreationFlags.IsCardReward);
        var reward = new CardReward(options, 3, Player!);
        reward.Populate();
        MainFile.Logger.Info($"[GenerateCardReward] {roomType}: {reward.Cards.Count()} options");
        return reward;
    }

    /// <summary>
    /// Picks the SACRIFICE alternative (Pael's Wing) on a freshly generated card reward.
    /// </summary>
    public static void SacrificeCardReward()
    {
        var reward = GenerateCardReward();
        var alt = CardRewardAlternative.Generate(reward).FirstOrDefault(a => a.OptionId == "SACRIFICE");
        if (alt == null) { MainFile.Logger.Warn("[SacrificeCardReward] no SACRIFICE alternative offered"); return; }
        TaskHelper.RunSafely(alt.OnSelect());
    }

    // --- Card selection prompts ---

    private static IDisposable? _selectorScope;

    /// <summary>
    /// Answers every card-selection prompt automatically (first N options) until
    /// <see cref="PopCardSelector"/>. Needed for relics whose effect waits on a choose-a-card screen
    /// (Gambling Chip's discard, Hefty Tablet's pick). Push it BEFORE the prompt opens.
    /// </summary>
    public static void PushAutoCardSelector()
    {
        PopCardSelector();
        _selectorScope = CardSelectCmd.PushSelector(new VakuuCardSelector());
    }

    public static void PopCardSelector()
    {
        _selectorScope?.Dispose();
        _selectorScope = null;
    }

    /// <summary>Closes any overlay screen a relic left open (reward screens from Kaleidoscope / Neow's Bones).</summary>
    public static void CloseOverlays()
    {
        try { NOverlayStack.Instance?.Clear(); }
        catch (Exception e) { MainFile.Logger.Warn($"[CloseOverlays] {e.Message}"); }
    }

    // --- Rest site ---

    /// <summary>
    /// Performs the rest-site Rest (heal) action: ModifyRestSiteHealAmount, the heal, then
    /// AfterRestSiteHeal. Entering the rest site alone only offers the options.
    /// </summary>
    public static void RestAtSite()
    {
        if (Player == null) return;
        TaskHelper.RunSafely(HealRestSiteOption.ExecuteRestSiteHeal(Player, isMimicked: false));
    }

    /// <summary>Selects a rest-site option by id ("LIFT", "DIG", "HEAL", …) in the current rest-site room.</summary>
    public static void SelectRestSiteOption(string optionId)
    {
        if (Player?.RunState.CurrentRoom is not RestSiteRoom room)
        {
            MainFile.Logger.Warn($"[SelectRestSiteOption] not in a rest site (room={Player?.RunState.CurrentRoom?.GetType().Name})");
            return;
        }
        var option = room.Options.FirstOrDefault(o => o.OptionId == optionId);
        if (option == null)
        {
            MainFile.Logger.Warn($"[SelectRestSiteOption] '{optionId}' not offered (have: {string.Join(",", room.Options.Select(o => o.OptionId))})");
            return;
        }
        TaskHelper.RunSafely(option.OnSelect());
    }

    // --- Death ---

    /// <summary>Kills every enemy through the Doom path, which fires AfterDiedToDoom (Book Repair Knife).</summary>
    public static void DoomKillEnemies()
    {
        var enemies = Player?.Creature.CombatState?.HittableEnemies.ToList();
        if (enemies == null || enemies.Count == 0) return;
        TaskHelper.RunSafely(DoomPower.DoomKill(enemies));
    }

    // --- Map ---

    public static MapPoint? CurrentMapPoint => Player?.RunState.CurrentMapPoint;

    /// <summary>
    /// Sets the current map point's type (e.g. Unknown for Planisphere). Returns the previous value
    /// so Cleanup can restore it, or null when there is no current point.
    /// </summary>
    public static MapPointType? SetCurrentMapPointType(MapPointType type)
    {
        var point = CurrentMapPoint;
        if (point == null) { MainFile.Logger.Warn("[SetCurrentMapPointType] no current map point"); return null; }
        var previous = point.PointType;
        point.PointType = type;
        return previous;
    }

    /// <summary>
    /// The first map point in the given row, or null past the map's end.
    /// </summary>
    public static MapPoint? MapPointInRow(int row) => Player?.RunState.Map.GetPointsInRow(row).FirstOrDefault();

    /// <summary>
    /// Travels to a map point the way the debug map does: records the coord as visited and enters
    /// the room, so map-travel relics (Winged Boots) see a real move. A point that is not a child of
    /// the current one counts as a non-adjacent jump.
    /// </summary>
    public static void TravelTo(MapPoint point, RoomType roomType) =>
        RunRoomTransition($"travel {point.coord.col},{point.coord.row} {roomType}",
            () => RunManager.Instance.EnterMapCoordDebug(point.coord, roomType, showTransition: false));
}
#endif
