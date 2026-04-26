using UnityEngine;
using Fusion;

/// <summary>
/// Handles click detection on an orb GameObject.
/// Sends RPC_RequestCollect to HOST ONLY — not to all clients.
/// The host validates, then broadcasts the result.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OrbVisual : MonoBehaviour
{
    private int _orbId;
    private OrbManager _manager;

    // Prevents sending duplicate requests from the same client
    // (e.g. double-click before the orb disappears)
    private bool _requestSent = false;

    public void Initialize(int orbId, OrbManager manager)
    {
        _orbId = orbId;
        _manager = manager;
    }

    private void OnMouseDown()
    {
        TryRequestCollect();
    }

    public void TryRequestCollect()
    {
        // Guard 1: already sent a request for this orb from this client
        if (_requestSent)
        {
            Debug.Log($"[OrbVisual] Request already sent for orb {_orbId} — ignoring duplicate click");
            return;
        }

        // Guard 2: runner must be valid (handles click + disconnect edge case)
        var runner = NetworkManager.Runner;
        if (runner == null || !runner.IsRunning)
        {
            Debug.LogWarning($"[OrbVisual] Runner not ready — cannot request collect for orb {_orbId}");
            return;
        }

        // Guard 3: relay must exist
        if (OrbRpcRelay.Instance == null)
        {
            Debug.LogWarning($"[OrbVisual] OrbRpcRelay not ready — cannot request collect");
            return;
        }

        _requestSent = true;

        // Send to HOST ONLY — not RpcTargets.All
        // The host will validate and broadcast RPC_ConfirmCollect if approved
        OrbRpcRelay.Instance.RPC_RequestCollect(_orbId, runner.LocalPlayer);

        Debug.Log($"[OrbVisual] Sent RPC_RequestCollect for orb {_orbId} by {runner.LocalPlayer}");
    }
}