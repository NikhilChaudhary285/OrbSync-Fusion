using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Stores disconnected player data and manages the 30-second rejoin window.
/// Uses PlayerRef as key — this is stable across disconnect/reconnect in Fusion Shared Mode.
///
/// IMPORTANT: PlayerRef is NOT guaranteed to be the same on reconnect in all configurations.
/// For production, use a custom player token/ID. For this assignment, PlayerRef is sufficient.
/// </summary>
public class RejoinManager : MonoBehaviour
{
    public static RejoinManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float rejoinWindowSeconds = 30f;

    // Stores saved data keyed by PlayerRef
    private Dictionary<PlayerRef, PlayerSaveData> _savedData = new();

    // Tracks active timers so we can cancel them if player rejoins
    private Dictionary<PlayerRef, Coroutine> _expiryTimers = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Save ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerStateManager when a player disconnects.
    /// Stores their score and position.
    /// </summary>
    public void SavePlayerData(PlayerRef player, PlayerSaveData data)
    {
        _savedData[player] = data;
        Debug.Log($"[RejoinManager] Saved data for {player} — score:{data.Score} pos:{data.Position}");
    }

    // ─── Timer ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a 30-second countdown. If it expires, the data is cleared.
    /// If the player rejoins before expiry, the timer is cancelled.
    /// </summary>
    public void StartRejoinTimer(PlayerRef player)
    {
        // Cancel any existing timer (shouldn't happen but be safe)
        if (_expiryTimers.TryGetValue(player, out var existing))
        {
            StopCoroutine(existing);
        }

        var timer = StartCoroutine(RejoinExpiry(player));
        _expiryTimers[player] = timer;

        Debug.Log($"[RejoinManager] Started 30s timer for {player}");
    }

    private IEnumerator RejoinExpiry(PlayerRef player)
    {
        yield return new WaitForSeconds(rejoinWindowSeconds);

        // Timer expired — clear saved data
        _savedData.Remove(player);
        _expiryTimers.Remove(player);

        Debug.Log($"[RejoinManager] Rejoin window EXPIRED for {player} — data cleared");
    }

    // ─── Restore ──────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if valid save data exists for this player.
    /// Returns true + data if within the rejoin window.
    /// Returns false if no data or window expired.
    /// </summary>
    public bool TryGetSavedData(PlayerRef player, out PlayerSaveData data)
    {
        if (_savedData.TryGetValue(player, out data) && data.IsValid)
        {
            // Double-check time window (belt-and-suspenders)
            float elapsed = Time.time - data.TimeSaved;
            if (elapsed <= rejoinWindowSeconds)
            {
                // Cancel the expiry timer — player is back
                CancelTimer(player);
                _savedData.Remove(player); // Consume the save data

                Debug.Log($"[RejoinManager] Valid rejoin for {player} — {elapsed:F1}s elapsed");
                return true;
            }
            else
            {
                // Expired (timer should have cleared it, but be safe)
                _savedData.Remove(player);
                Debug.Log($"[RejoinManager] Rejoin data EXPIRED for {player}");
                data = default;
                return false;
            }
        }

        data = default;
        return false;
    }

    private void CancelTimer(PlayerRef player)
    {
        if (_expiryTimers.TryGetValue(player, out var timer))
        {
            StopCoroutine(timer);
            _expiryTimers.Remove(player);
            Debug.Log($"[RejoinManager] Timer cancelled for {player} — rejoined in time");
        }
    }

    // ─── Debug ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns how many seconds are left in the rejoin window for a player.
    /// Returns -1 if no saved data exists.
    /// </summary>
    public float GetTimeRemaining(PlayerRef player)
    {
        if (_savedData.TryGetValue(player, out var data))
        {
            return rejoinWindowSeconds - (Time.time - data.TimeSaved);
        }
        return -1f;
    }
}