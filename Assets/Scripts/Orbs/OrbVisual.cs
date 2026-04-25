using UnityEngine;
using Fusion;

/// <summary>
/// Attached to each orb's GameObject.
/// Handles click detection and sends collect RPC.
/// Plain MonoBehaviour — no networking here.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OrbVisual : MonoBehaviour
{
    private int _orbId;
    private OrbManager _manager;
    private bool _isBeingCollected = false; // Prevents double-click race

    public void Initialize(int orbId, OrbManager manager)
    {
        _orbId = orbId;
        _manager = manager;
    }

    // OnMouseDown works with 3D colliders when Camera has no UI overlay
    private void OnMouseDown()
    {
        TryCollect();
    }

    public void TryCollect()
    {
        // Guard: prevent multiple clicks before RPC resolves
        if (_isBeingCollected) return;

        // Extra safety: check runner exists (handles click + leave edge case)
        var runner = NetworkManager.Runner;
        if (runner == null || !runner.IsRunning)
        {
            Debug.LogWarning("[OrbVisual] Runner not available — ignoring click");
            return;
        }

        _isBeingCollected = true;

        // Send collect RPC — all clients will validate
        OrbRpcRelay.Instance.RPC_CollectOrb(_orbId, runner.LocalPlayer);
        Debug.Log($"[OrbVisual] Clicked orb and Sent collect RPC for orb {_orbId}");
    }
}