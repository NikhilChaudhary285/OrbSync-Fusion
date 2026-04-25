using UnityEngine;
using Fusion;

/// <summary>
/// Thin NetworkBehaviour whose sole job is to carry RPCs for the OrbManager.
/// Must be a NetworkObject in the scene (not spawned at runtime).
/// </summary>
public class OrbRpcRelay : NetworkBehaviour
{
    public static OrbRpcRelay Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Host calls this → all clients (including host) receive it.
    /// RpcSources.StateAuthority = only the host can send this.
    /// RpcTargets.All = every client receives it.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SpawnOrb(int orbId, Vector3 position)
    {
        GameManager.Instance.OrbManager.OnOrbSpawnReceived(orbId, position);
    }

    /// <summary>
    /// Any player can claim an orb — but the server validates it first.
    /// RpcSources.InputAuthority = the player who owns this object sends it.
    ///
    /// NOTE: For collect, we use a PlayerNetworkBehaviour approach instead.
    /// See OrbCollectRpc below.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_CollectOrb(int orbId, PlayerRef collector)
    {
        GameManager.Instance.OrbManager.OnOrbCollectReceived(orbId, collector);
    }
}