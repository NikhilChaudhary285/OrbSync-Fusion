using UnityEngine;
using Fusion;

/// <summary>
/// Carries all orb RPCs. Two-phase pattern for race-safe collection:
///
/// PHASE 1 — client → host:   RPC_RequestCollect   (StateAuthority target only)
/// PHASE 2 — host  → all:     RPC_ConfirmCollect   (All target)
///
/// Why two phases?
/// If we send directly to All, each client validates independently.
/// Network jitter means Client A sees A's RPC first; Client B sees B's RPC first.
/// Both pass validation → both get score.
///
/// With two phases, the host is the ONLY validator.
/// It processes requests in strict tick order. First arrival wins, period.
/// Then one authoritative ConfirmCollect goes to everyone — all clients
/// execute identical logic with identical data → guaranteed consistency.
/// </summary>
public class OrbRpcRelay : NetworkBehaviour
{
    public static OrbRpcRelay Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Orb spawn (host → all) ────────────────────────────────────────────

    /// <summary>
    /// Host spawns an orb → all clients create the visual.
    /// RpcSources.StateAuthority = only the master client can call this.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SpawnOrb(int orbId, Vector3 position)
    {
        if (GameManager.Instance?.OrbManager == null)
        {
            Debug.LogWarning("[OrbRpcRelay] RPC_SpawnOrb received before OrbManager ready");
            return;
        }
        Debug.Log($"[OrbRpcRelay] RPC_SpawnOrb received — orb {orbId}");
        GameManager.Instance.OrbManager.OnOrbSpawnReceived(orbId, position);
    }

    // ── PHASE 1: Client requests collection from host only ────────────────

    /// <summary>
    /// Any client clicks an orb → sends this to the HOST ONLY.
    /// RpcSources.All         = any client can send this.
    /// RpcTargets.StateAuthority = ONLY the host receives and processes it.
    ///
    /// This is the key fix: validation happens in ONE place (host), not on
    /// every client independently.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestCollect(int orbId, PlayerRef requestingPlayer)
    {
        Debug.Log($"[OrbRpcRelay] RPC_RequestCollect — orb {orbId} requested by {requestingPlayer}");

        // Host validates: is this orb still available?
        bool approved = GameManager.Instance.OrbManager.TryClaimOnHost(orbId, requestingPlayer);

        if (approved)
        {
            // Host approves → broadcast confirm to ALL clients
            RPC_ConfirmCollect(orbId, requestingPlayer);
            Debug.Log($"[OrbRpcRelay] Approved — broadcasting RPC_ConfirmCollect for orb {orbId}");
        }
        else
        {
            // Rejected — orb was already claimed by someone else
            Debug.Log($"[OrbRpcRelay] REJECTED — orb {orbId} already claimed, ignoring {requestingPlayer}");
        }
    }

    // ── PHASE 2: Host confirms collection to all clients ──────────────────

    /// <summary>
    /// Host sends this after approving a collect request.
    /// RpcSources.StateAuthority = only the host sends this (called from RPC_RequestCollect above).
    /// RpcTargets.All            = every client receives and executes this.
    ///
    /// Because this comes from one authority with one winner, all clients
    /// execute identical logic → no inconsistency possible.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ConfirmCollect(int orbId, PlayerRef winner)
    {
        Debug.Log($"[OrbRpcRelay] RPC_ConfirmCollect — orb {orbId} won by {winner}");

        if (GameManager.Instance?.OrbManager == null) return;
        GameManager.Instance.OrbManager.OnOrbCollectConfirmed(orbId, winner);
    }
}