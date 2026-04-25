using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Manages all orbs in the game world.
/// Orbs are NOT NetworkObjects — they are synced purely via RPCs.
/// The host generates orb data and broadcasts it; all clients create visuals locally.
/// </summary>
public class OrbManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxOrbs = 10;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float spawnAreaX = 12f;
    [SerializeField] private float spawnAreaZ = 12f;
    [SerializeField] private float orbY = 0.5f;  // Height above ground

    [Header("Prefab")]
    [SerializeField] private GameObject orbVisualPrefab; // Plain MonoBehaviour prefab

    // Tracks all active orbs: OrbId → OrbVisual
    private Dictionary<int, OrbVisual> _activeOrbs = new();

    // Tracks claimed orb IDs to prevent double-scoring
    private HashSet<int> _claimedOrbIds = new();

    // Monotonically increasing ID — host only
    private int _nextOrbId = 0;

    private Coroutine _spawnCoroutine;

    // Called by GameManager once runner is ready
    public void Initialize(NetworkRunner runner)
    {
        // Only the host spawns orbs
        if (runner.IsSharedModeMasterClient)
        {
            _spawnCoroutine = StartCoroutine(SpawnOrbLoop(runner));
            Debug.Log("[OrbManager] Host: starting orb spawn loop");
        }
    }

    // ─── Host-side spawn loop ──────────────────────────────────────────────

    private IEnumerator SpawnOrbLoop(NetworkRunner runner)
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            if (_activeOrbs.Count < maxOrbs)
            {
                SpawnOrb(runner);
            }
        }
    }

    private void SpawnOrb(NetworkRunner runner)
    {
        // Generate a unique ID and random position
        int orbId = _nextOrbId++;
        Vector3 position = new Vector3(
            Random.Range(-spawnAreaX, spawnAreaX),
            orbY,
            Random.Range(-spawnAreaZ, spawnAreaZ)
        );

        // Tell ALL clients (including self) to create this orb via RPC
        // We use a NetworkBehaviour to call RPC — see below
        OrbRpcRelay.Instance.RPC_SpawnOrb(orbId, position);

        Debug.Log($"[OrbManager] Host spawned orb {orbId} at {position}");
    }

    // ─── Called on ALL clients when orb spawn RPC arrives ─────────────────

    public void OnOrbSpawnReceived(int orbId, Vector3 position)
    {
        // Guard: don't create duplicates
        if (_activeOrbs.ContainsKey(orbId))
        {
            Debug.LogWarning($"[OrbManager] Duplicate spawn for orb {orbId} — ignored");
            return;
        }

        // Guard: don't create already-claimed orbs (handles late joiners)
        if (_claimedOrbIds.Contains(orbId))
        {
            Debug.LogWarning($"[OrbManager] Orb {orbId} already claimed — ignored");
            return;
        }

        var go = Instantiate(orbVisualPrefab, position, Quaternion.identity);
        var visual = go.GetComponent<OrbVisual>();
        visual.Initialize(orbId, this);
        _activeOrbs[orbId] = visual;
    }

    // ─── Called on ALL clients when orb collect RPC arrives ───────────────

    public void OnOrbCollectReceived(int orbId, PlayerRef collector)
    {
        // Guard: prevent double-collection
        if (_claimedOrbIds.Contains(orbId))
        {
            Debug.LogWarning($"[OrbManager] Orb {orbId} already claimed — ignoring duplicate RPC");
            return;
        }

        _claimedOrbIds.Add(orbId);

        // Destroy the visual if it exists on this client
        if (_activeOrbs.TryGetValue(orbId, out var visual))
        {
            _activeOrbs.Remove(orbId);
            Destroy(visual.gameObject);
        }

        // Award score only to the local player if they are the collector
        if (collector == NetworkManager.Runner.LocalPlayer)
        {
            PlayerStateManager.Instance.AddScore(1);
            Debug.Log($"[OrbManager] Local player collected orb {orbId} — score +1");
        }
    }

    /// <summary>
    /// Sends the entire current orb state to a newly-joining player.
    /// Called during rejoin to sync world state.
    /// </summary>
    public void SyncStateToPlayer(PlayerRef player)
    {
        foreach (var kvp in _activeOrbs)
        {
            OrbRpcRelay.Instance.RPC_SpawnOrb(kvp.Key, kvp.Value.transform.position);
        }
    }
}