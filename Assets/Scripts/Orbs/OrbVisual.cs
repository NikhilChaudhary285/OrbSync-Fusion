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

    private void OnMouseDown()
    {
        // Guard: prevent multiple clicks before RPC resolves
        if (_isBeingCollected) return;

        _isBeingCollected = true;

        // Send collect RPC — all clients will validate
        OrbRpcRelay.Instance.RPC_CollectOrb(_orbId, NetworkManager.Runner.LocalPlayer);

        Debug.Log($"[OrbVisual] Clicked orb {_orbId}");
    }
}