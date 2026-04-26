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

        Vector3 spawnPos = GetNextSpawnPoint();
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

    public void SpawnAndRestorePlayer(PlayerRef player, PlayerSaveData savedData)
    {
        var runner = NetworkManager.Runner;
        if (runner == null || playerPrefab == null) return;

        runner.Spawn(playerPrefab, savedData.Position, Quaternion.identity, player);
        StartCoroutine(RestoreScoreDelayed(player, savedData.Score));
        Debug.Log($"[PlayerStateManager] Restored player {player} — score:{savedData.Score} pos:{savedData.Position}");
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
            np.Score += amount;
            Debug.Log($"[PlayerStateManager] Score +{amount} → now {np.Score}");
        }
        else
        {
            Debug.LogWarning("[PlayerStateManager] AddScore called but local player not registered yet");
        }
    }

    public int GetScore(PlayerRef player)
        => _players.TryGetValue(player, out var np) ? np.Score : 0;

    public void SavePlayerData(PlayerRef player)
    {
        if (!_players.TryGetValue(player, out var np)) return;

        var data = new PlayerSaveData
        {
            Score = np.Score,
            Position = np.transform.position,
            TimeSaved = Time.time,
            IsValid = true
        };

        GameManager.Instance.RejoinManager.SavePlayerData(player, data);
        _players.Remove(player);
        Debug.Log($"[PlayerStateManager] Saved and unregistered player {player}");
    }

    private Vector3 GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return new Vector3(UnityEngine.Random.Range(-5f, 5f), 0.5f, UnityEngine.Random.Range(-5f, 5f));

        var pt = spawnPoints[_spawnIndex % spawnPoints.Length];
        _spawnIndex++;
        return pt != null ? pt.position : Vector3.zero + Vector3.up * 0.5f;
    }
}