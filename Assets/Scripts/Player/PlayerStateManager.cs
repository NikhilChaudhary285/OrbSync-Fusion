// Assets/Scripts/Player/PlayerStateManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    [Header("Prefab — MUST be assigned in Inspector")]
    [SerializeField] public NetworkObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private Dictionary<PlayerRef, NetworkedPlayer> _players = new();
    private int _spawnIndex = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RegisterPlayer(PlayerRef player, NetworkedPlayer np)
    {
        _players[player] = np;
        Debug.Log($"[PlayerStateManager] Registered {player}. Total players: {_players.Count}");
    }

    public void SpawnFreshPlayer(PlayerRef player)
    {
        var runner = NetworkManager.Runner;

        // Guard: runner must be valid
        if (runner == null || !runner.IsRunning)
        {
            Debug.LogError("[PlayerStateManager] Runner is null or not running!");
            return;
        }

        // Guard: prefab must be assigned
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerStateManager] playerPrefab is NOT assigned in Inspector!");
            return;
        }

        Vector3 spawnPos = GetNextSpawnPoint(player); // pass player ref
        Debug.Log($"[PlayerStateManager] Spawning player {player} at {spawnPos}");

        // In Shared Mode: pass player as the InputAuthority
        var obj = runner.Spawn(
            playerPrefab,
            spawnPos,
            Quaternion.identity,
            player,                  // InputAuthority
            (runner, obj) =>         // OnBeforeSpawned callback
            {
                // This fires right before Spawned() — good place for init
                Debug.Log($"[PlayerStateManager] OnBeforeSpawned for {player}");
            }
        );

        if (obj == null)
            Debug.LogError("[PlayerStateManager] runner.Spawn returned null! Check prefab has NetworkObject.");
        else
            Debug.Log($"[PlayerStateManager] Spawn succeeded: {obj.name}");
    }

    // In PlayerStateManager.cs — replace SpawnAndRestorePlayer:
    public void SpawnAndRestorePlayer(PlayerRef player, PlayerSaveData savedData)
    {
        var runner = NetworkManager.Runner;
        if (runner == null || playerPrefab == null) return;

        runner.Spawn(
            playerPrefab,
            savedData.Position,
            Quaternion.identity,
            player,
            (spawnRunner, obj) =>
            {
                // OnBeforeSpawned — fires synchronously before Spawned()
                // Safe to set initial values here
                var np = obj.GetComponent<NetworkedPlayer>();
                if (np != null)
                {
                    // Queue the score restore — will apply in Spawned()
                    np.PendingScoreRestore = savedData.Score;
                    Debug.Log($"[PlayerStateManager] Set PendingScoreRestore={savedData.Score} for {player}");
                }
            }
        );

        Debug.Log($"[PlayerStateManager] Spawning restored player {player} at {savedData.Position}");
    }

    private IEnumerator RestoreScoreDelayed(PlayerRef player, int score)
    {
        yield return null;
        yield return null;
        if (_players.TryGetValue(player, out var np))
            np.Score = score;
    }

    public void AddScore(int amount)
    {
        var runner = NetworkManager.Runner;
        if (runner == null) return;
        if (_players.TryGetValue(runner.LocalPlayer, out var np))
        {
            np.SetScore(np.CachedScore + amount);
            Debug.Log($"[PlayerStateManager] Score +{amount} → now {np.CachedScore}");
        }
    }

    public int GetScore(PlayerRef player)
        => _players.TryGetValue(player, out var np) ? np.Score : 0;

    public void SavePlayerData(PlayerRef player)
    {
        if (!_players.TryGetValue(player, out var np)) return;

        // Get stable UserId BEFORE the object is despawned
        // np.StableUserId is a [Networked] string — read it while object still exists
        string userId = np.StableUserId.ToString();
        if (string.IsNullOrEmpty(userId))
            userId = player.ToString(); // fallback

        var data = new PlayerSaveData
        {
            Score = np.CachedScore,
            Position = np.transform.position,
            TimeSaved = Time.time,
            IsValid = true,
            UserId = userId
        };

        GameManager.Instance.RejoinManager.SavePlayerData(userId, data);
        GameManager.Instance.RejoinManager.StartRejoinTimer(userId); // ← start timer here
        _players.Remove(player);
        Debug.Log($"[PlayerStateManager] Saved player {player} userId:{userId} — score:{data.Score}");
    }

    private Vector3 GetNextSpawnPoint(PlayerRef player)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return new Vector3(Random.Range(-5f, 5f), 0.5f, Random.Range(-5f, 5f));

        // Use player's raw ID to pick spawn point deterministically
        // Player:1 → index 0, Player:2 → index 1, etc.
        // This is consistent across all clients without needing sync
        int index = (player.RawEncoded - 1) % spawnPoints.Length;
        index = Mathf.Abs(index); // Safety: ensure non-negative

        var pt = spawnPoints[index];
        Debug.Log($"[PlayerStateManager] Player {player} → spawn index {index}");
        return pt != null ? pt.position : Vector3.up * 0.5f;
    }
}