using UnityEngine;
using Fusion;

/// <summary>
/// Attached to the player NetworkObject prefab.
/// 
/// KEY FUSION CONCEPT:
/// - FixedUpdateNetwork() runs on ALL clients for ALL player objects
/// - GetInput<T>() returns input ONLY for the object's InputAuthority
/// - On all other clients, GetInput returns false → no movement applied
/// - [Networked] position is automatically synced via Fusion's state sync
/// </summary>
public class NetworkedPlayer : NetworkBehaviour
{
    [Networked] public int Score { get; set; }
    [Networked] public Vector3 Position { get; set; }

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 5f;

    // Used for smooth visual interpolation on remote clients
    private NetworkCharacterController _ncc;

    public override void Spawned()
    {
        // Try to get NetworkCharacterController (preferred over plain CC)
        _ncc = GetComponent<NetworkCharacterController>();

        // Register with PlayerStateManager
        PlayerStateManager.Instance.RegisterPlayer(Object.InputAuthority, this);

        // CRITICAL: Tell the runner this object belongs to this player
        // Without this, Local PlayerObject stays None in the inspector
        // and Fusion can't route inputs correctly
        if (HasInputAuthority)
        {
            Runner.SetPlayerObject(Runner.LocalPlayer, Object);
            Debug.Log($"[NetworkedPlayer] Set as local player object for {Runner.LocalPlayer}");
        }

        Debug.Log($"[NetworkedPlayer] Spawned for player {Object.InputAuthority} " +
                  $"| HasInputAuthority: {HasInputAuthority} " +
                  $"| HasStateAuthority: {HasStateAuthority}");

        // Color each player differently so we can see them as separate objects
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            // Use InputAuthority's raw value to pick a color
            Color[] colors = { Color.blue, Color.red, Color.green, Color.magenta };
            int idx = Object.InputAuthority.RawEncoded % colors.Length;
            renderer.material.color = colors[idx];
        }
    }

    public override void FixedUpdateNetwork()
    {
        // GetInput only returns true for the InputAuthority's object
        // On remote clients this returns false → they don't move this object
        if (!GetInput(out PlayerInput input)) return;

        // Build movement vector from networked input
        Vector3 move = new Vector3(input.Direction.x, 0f, input.Direction.y)
                       * moveSpeed * Runner.DeltaTime;

        if (_ncc != null)
        {
            // NetworkCharacterController handles position sync automatically
            _ncc.Move(move);
        }
        else
        {
            // Fallback: move transform directly
            transform.position += move;
        }

        // Keep our [Networked] Position in sync (used for rejoin restore)
        Position = transform.position;
    }

    public void RestorePosition(Vector3 pos)
    {
        transform.position = pos;
        Position = pos;
    }
}   