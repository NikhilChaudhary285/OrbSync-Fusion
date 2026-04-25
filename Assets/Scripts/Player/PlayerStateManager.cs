using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Tracks all active players, manages score, and handles spawn/despawn.
/// Singleton — accessible from anywhere via Instance.
/// </summary>
public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    // PlayerId → their NetworkedPlayer object
    private Dictionary<PlayerRef, NetworkedPlayer> _players = new();

    private int _spawnIndex = 0;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Registration ─────────────────────────────────────────────────────

    public void RegisterPlayer(PlayerRef player, NetworkedPlayer networkedPlayer)
    {
        _players[player] = networkedPlayer;
        Debug.Log($"[PlayerStateManager] Registered player {player}");
    }

    // ─── Spawning ─────────────────────────────────────────────────────────

    public void SpawnFreshPlayer(PlayerRef player)
    {
        var runner = NetworkManager.Runner;
        Vector3 spawnPos = GetNextSpawnPoint();

        // In Shared Mode: each client spawns their own player object
        var obj = runner.Spawn(
            playerPrefab,
            spawnPos,
            Quaternion.identity,
            player           // InputAuthority = this player owns the object
        );

        Debug.Log($"[PlayerStateManager] Spawned fresh player {player} at {spawnPos}");
    }

    public void SpawnAndRestorePlayer(PlayerRef player, PlayerSaveData savedData)
    {
        var runner = NetworkManager.Runner;

        var obj = runner.Spawn(
            playerPrefab,
            savedData.Position,  // Restore saved position
            Quaternion.identity,
            player
        );

        // Restore score after a short delay (wait for Spawned() to fire)
        StartCoroutine(RestoreScoreDelayed(player, savedData.Score));

        Debug.Log($"[PlayerStateManager] Restored player {player} — score: {savedData.Score}");
    }

    private System.Collections.IEnumerator RestoreScoreDelayed(PlayerRef player, int score)
    {
        // Wait one frame for Spawned() to register the player
        yield return null;
        yield return null;

        if (_players.TryGetValue(player, out var np))
        {
            np.Score = score;
            Debug.Log($"[PlayerStateManager] Score restored to {score} for player {player}");
        }
    }

    // ─── Score ────────────────────────────────────────────────────────────

    public void AddScore(int amount)
    {
        var runner = NetworkManager.Runner;
        if (runner == null) return;

        if (_players.TryGetValue(runner.LocalPlayer, out var np))
        {
            np.Score += amount;
            Debug.Log($"[PlayerStateManager] Score now: {np.Score}");
        }
    }

    public int GetScore(PlayerRef player)
    {
        return _players.TryGetValue(player, out var np) ? np.Score : 0;
    }

    // ─── Save / Despawn ───────────────────────────────────────────────────

    public void SavePlayerData(PlayerRef player)
    {
        if (!_players.TryGetValue(player, out var np)) return;

        var data = new PlayerSaveData
        {
            Score = np.Score,
            Position = np.Position,
            TimeSaved = Time.time,
            IsValid = true
        };

        GameManager.Instance.RejoinManager.SavePlayerData(player, data);
        _players.Remove(player);

        Debug.Log($"[PlayerStateManager] Saved data for player {player}: score={data.Score}");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private Vector3 GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return new Vector3(Random.Range(-5f, 5f), 0.5f, Random.Range(-5f, 5f));

        var point = spawnPoints[_spawnIndex % spawnPoints.Length].position;
        _spawnIndex++;
        return point;
    }
}