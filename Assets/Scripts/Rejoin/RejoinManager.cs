// Assets/Scripts/Rejoin/RejoinManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Persists player save data to PlayerPrefs so it survives
/// process restarts (Editor stop/play, build close/reopen).
///
/// Key design: UserId (stable device ID) → JSON save data stored in PlayerPrefs.
/// The 30-second window is checked against real wall-clock time using
/// DateTimeOffset so it works correctly across restarts.
/// </summary>
public class RejoinManager : MonoBehaviour
{
    public static RejoinManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float rejoinWindowSeconds = 30f;

    // In-memory timers for the current session
    private Dictionary<string, Coroutine> _expiryTimers = new();

    // PlayerPrefs key prefix
    private const string PREFS_PREFIX = "RejoinData_";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Save ─────────────────────────────────────────────────────────────

    public void SavePlayerData(string userId, PlayerSaveData data)
    {
        // Store save time as Unix timestamp (survives process restart)
        var saveRecord = new SaveRecord
        {
            Score = data.Score,
            PosX = data.Position.x,
            PosY = data.Position.y,
            PosZ = data.Position.z,
            SaveTimestamp = GetUnixTimestamp()
        };

        string json = JsonUtility.ToJson(saveRecord);
        PlayerPrefs.SetString(PREFS_PREFIX + userId, json);
        PlayerPrefs.Save();

        Debug.Log($"[RejoinManager] Saved to PlayerPrefs — userId:{userId} score:{data.Score} pos:{data.Position}");
    }

    // ── Timer ─────────────────────────────────────────────────────────────

    public void StartRejoinTimer(string userId)
    {
        // Cancel existing timer
        if (_expiryTimers.TryGetValue(userId, out var existing))
            StopCoroutine(existing);

        var timer = StartCoroutine(RejoinExpiry(userId));
        _expiryTimers[userId] = timer;
        Debug.Log($"[RejoinManager] Started {rejoinWindowSeconds}s timer for userId:{userId}");
    }

    private IEnumerator RejoinExpiry(string userId)
    {
        yield return new WaitForSeconds(rejoinWindowSeconds);
        ClearSavedData(userId);
        _expiryTimers.Remove(userId);
        Debug.Log($"[RejoinManager] Rejoin window EXPIRED for userId:{userId}");
    }

    private void ClearSavedData(string userId)
    {
        PlayerPrefs.DeleteKey(PREFS_PREFIX + userId);
        PlayerPrefs.Save();
    }

    // ── Restore ───────────────────────────────────────────────────────────

    public bool TryGetSavedData(string userId, out PlayerSaveData data)
    {
        string key = PREFS_PREFIX + userId;

        if (!PlayerPrefs.HasKey(key))
        {
            Debug.Log($"[RejoinManager] No saved data for userId:{userId}");
            data = default;
            return false;
        }

        string json = PlayerPrefs.GetString(key);
        SaveRecord record;

        try
        {
            record = JsonUtility.FromJson<SaveRecord>(json);
        }
        catch
        {
            Debug.LogWarning($"[RejoinManager] Corrupt save data for userId:{userId} — clearing");
            ClearSavedData(userId);
            data = default;
            return false;
        }

        // Check time window using real wall-clock time
        long now = GetUnixTimestamp();
        long elapsed = now - record.SaveTimestamp;

        Debug.Log($"[RejoinManager] Found save for userId:{userId} — {elapsed}s elapsed (window:{rejoinWindowSeconds}s)");

        if (elapsed <= (long)rejoinWindowSeconds)
        {
            // Valid rejoin — consume the data
            data = new PlayerSaveData
            {
                Score = record.Score,
                Position = new Vector3(record.PosX, record.PosY, record.PosZ),
                TimeSaved = Time.time,
                IsValid = true,
                UserId = userId
            };

            CancelTimer(userId);
            ClearSavedData(userId); // consume save — one-time use
            Debug.Log($"[RejoinManager] Valid rejoin — score:{data.Score} pos:{data.Position}");
            return true;
        }
        else
        {
            // Expired
            ClearSavedData(userId);
            Debug.Log($"[RejoinManager] Data EXPIRED for userId:{userId} ({elapsed}s > {rejoinWindowSeconds}s)");
            data = default;
            return false;
        }
    }

    private void CancelTimer(string userId)
    {
        if (_expiryTimers.TryGetValue(userId, out var timer))
        {
            StopCoroutine(timer);
            _expiryTimers.Remove(userId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static long GetUnixTimestamp()
    {
        return (long)(System.DateTimeOffset.UtcNow - System.DateTimeOffset.UnixEpoch).TotalSeconds;
    }

    public float GetTimeRemaining(string userId)
    {
        string key = PREFS_PREFIX + userId;
        if (!PlayerPrefs.HasKey(key)) return -1f;

        string json = PlayerPrefs.GetString(key);
        try
        {
            var record = JsonUtility.FromJson<SaveRecord>(json);
            long elapsed = GetUnixTimestamp() - record.SaveTimestamp;
            return Mathf.Max(0f, rejoinWindowSeconds - elapsed);
        }
        catch { return -1f; }
    }

    // ── Serializable record stored in PlayerPrefs ─────────────────────────

    [System.Serializable]
    private class SaveRecord
    {
        public int Score;
        public float PosX;
        public float PosY;
        public float PosZ;
        public long SaveTimestamp; // Unix seconds — survives restarts
    }
}