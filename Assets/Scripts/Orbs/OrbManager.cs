using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Manages orb state using a two-phase collect pattern.
///
/// TryClaimOnHost()       — runs on host only, atomically marks orb claimed
/// OnOrbCollectConfirmed()— runs on ALL clients after host approves
///
/// This separation means validation is single-threaded (host tick order)
/// and the result is broadcast authoritatively to all clients.
/// </summary>
public class OrbManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxOrbs = 10;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float spawnAreaX = 12f;
    [SerializeField] private float spawnAreaZ = 12f;
    [SerializeField] private float orbY = 0.5f;

    [Header("Prefab")]
    [SerializeField] private GameObject orbVisualPrefab;

    // Authoritative data (OrbData is source of truth)
    private Dictionary<int, OrbData> _orbDataMap = new();
    private Dictionary<int, OrbVisual> _orbVisuals = new();

    private int _nextOrbId = 0;
    private Coroutine _spawnCoroutine;

    // ── Init ──────────────────────────────────────────────────────────────

    private bool _initialized = false;

    public void Initialize(NetworkRunner runner)
    {
        if (_initialized)
        {
            Debug.LogWarning("[OrbManager] Initialize called again — ignored");
            return;
        }
        _initialized = true;

        if (runner.IsSharedModeMasterClient)
        {
            _spawnCoroutine = StartCoroutine(SpawnOrbLoop(runner));
            Debug.Log("[OrbManager] Host: orb spawn loop started");
        }
    }

    // ── Host: spawn loop ──────────────────────────────────────────────────

    private IEnumerator SpawnOrbLoop(NetworkRunner runner)
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            if (CountActiveOrbs() < maxOrbs)
                SpawnOrb(runner);
        }
    }

    private void SpawnOrb(NetworkRunner runner)
    {
        int orbId = _nextOrbId++;
        var data = new OrbData
        {
            OrbId = orbId,
            Position = new Vector3(
                Random.Range(-spawnAreaX, spawnAreaX),
                orbY,
                Random.Range(-spawnAreaZ, spawnAreaZ)),
            IsClaimed = false
        };

        // Store in data map FIRST
        _orbDataMap[orbId] = data;

        // Create visual immediately on host — don't wait for RPC loopback
        // The RPC will fire on this object too, but the duplicate guard will
        // correctly skip it since we already have the data
        CreateVisual(data);

        // Broadcast to all REMOTE clients via RPC
        OrbRpcRelay.Instance.RPC_SpawnOrb(data.OrbId, data.Position);

        Debug.Log($"[OrbManager] Host spawned orb {data.OrbId} at {data.Position}");
    }

    // ── All clients: receive spawn ────────────────────────────────────────

    public void OnOrbSpawnReceived(int orbId, Vector3 position)
    {
        // If we already have this orb (host created it directly, or duplicate RPC)
        // just skip — visual already exists
        if (_orbDataMap.ContainsKey(orbId))
        {
            // This is EXPECTED on the host — not a warning, just a no-op
            Debug.Log($"[OrbManager] OnOrbSpawnReceived: orb {orbId} already exists — skipping (expected on host)");
            return;
        }

        // Remote client path: create data record and visual
        var data = new OrbData { OrbId = orbId, Position = position, IsClaimed = false };
        _orbDataMap[orbId] = data;
        CreateVisual(data);

        Debug.Log($"[OrbManager] OnOrbSpawnReceived: created orb {orbId} at {position}");
    }

    // ── HOST ONLY: atomic claim validation ────────────────────────────────

    /// <summary>
    /// Called ONLY on the host inside RPC_RequestCollect.
    /// Returns true if this is the FIRST valid claim for this orb.
    /// Returns false if already claimed — second caller is rejected.
    ///
    /// Because Fusion processes RPCs in tick order on a single thread,
    /// two simultaneous requests are serialized here. No mutex needed.
    /// The first one to arrive marks IsClaimed = true; the second sees
    /// IsClaimed = true and returns false immediately.
    /// </summary>
    public bool TryClaimOnHost(int orbId, PlayerRef requestingPlayer)
    {
        // Orb doesn't exist at all
        if (!_orbDataMap.TryGetValue(orbId, out OrbData data))
        {
            Debug.LogWarning($"[OrbManager] TryClaimOnHost: orb {orbId} not found");
            return false;
        }

        // Already claimed — second request rejected
        if (data.IsClaimed)
        {
            Debug.LogWarning($"[OrbManager] TryClaimOnHost: orb {orbId} already claimed — {requestingPlayer} rejected");
            return false;
        }

        // First valid claim — mark it atomically
        data.IsClaimed = true;   // structs are value types — must re-assign:
        _orbDataMap[orbId] = data;

        Debug.Log($"[OrbManager] TryClaimOnHost: orb {orbId} APPROVED for {requestingPlayer}");
        return true;
    }

    // ── All clients: receive confirmed collect ────────────────────────────

    /// <summary>
    /// Called on ALL clients when host broadcasts RPC_ConfirmCollect.
    /// By this point validation is already done — just execute the result.
    /// No need to re-validate here: host already guaranteed exactly one winner.
    /// </summary>
    public void OnOrbCollectConfirmed(int orbId, PlayerRef winner)
    {
        // Mark claimed in local data map too (keeps non-host clients in sync)
        if (_orbDataMap.TryGetValue(orbId, out OrbData data))
        {
            data.IsClaimed = true;
            _orbDataMap[orbId] = data;
        }

        // Destroy the visual on this client
        if (_orbVisuals.TryGetValue(orbId, out var visual))
        {
            _orbVisuals.Remove(orbId);
            Destroy(visual.gameObject);
        }

        // Award score ONLY to the local player if they are the confirmed winner
        var runner = NetworkManager.Runner;
        if (runner != null && winner == runner.LocalPlayer)
        {
            PlayerStateManager.Instance.AddScore(1);
            Debug.Log($"[OrbManager] Score awarded — orb {orbId} confirmed winner: {winner}");
        }
        else
        {
            Debug.Log($"[OrbManager] Orb {orbId} collected by {winner} — not local player, no score");
        }
    }

    // ── World state sync for rejoining players ────────────────────────────

    public void SyncStateToPlayer(PlayerRef player)
    {
        int count = 0;
        foreach (var kvp in _orbDataMap)
        {
            if (!kvp.Value.IsClaimed)
            {
                OrbRpcRelay.Instance.RPC_SpawnOrb(kvp.Value.OrbId, kvp.Value.Position);
                count++;
            }
        }
        Debug.Log($"[OrbManager] Synced {count} active orbs to {player}");
    }

    // ── Data accessors ────────────────────────────────────────────────────

    public List<OrbData> GetAllOrbData() => new List<OrbData>(_orbDataMap.Values);
    public List<OrbData> GetActiveOrbData()
    {
        var result = new List<OrbData>();
        foreach (var d in _orbDataMap.Values)
            if (!d.IsClaimed) result.Add(d);
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void CreateVisual(OrbData data)
    {
        var go = Instantiate(orbVisualPrefab, data.Position, Quaternion.identity);
        var visual = go.GetComponent<OrbVisual>();
        if (visual == null)
        {
            Debug.LogError("[OrbManager] orbVisualPrefab missing OrbVisual component!");
            Destroy(go);
            return;
        }
        visual.Initialize(data.OrbId, this);
        _orbVisuals[data.OrbId] = visual;
    }

    private int CountActiveOrbs()
    {
        int n = 0;
        foreach (var d in _orbDataMap.Values)
            if (!d.IsClaimed) n++;
        return n;
    }
}