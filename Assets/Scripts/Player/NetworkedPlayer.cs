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

    // In NetworkedPlayer.cs — adding this field
    public int CachedScore { get; private set; }  // plain C# — always safe to read

    // Update FixedUpdateNetwork or wherever score changes to keep it in sync:
    // Adding this inside Spawned() and whenever Score changes:
    // In NetworkedPlayer.cs — adding this networked property:
    [Networked] public NetworkString<_64> StableUserId { get; set; } // In Spawned() — set it for the local player only:
    // In NetworkedPlayer.cs — add field:
    public int PendingScoreRestore = -1; // -1 means no restore pending // In Spawned() — apply it after registering:
    public override void Spawned()
    {
        _ncc = GetComponent<NetworkCharacterController>();
        CachedScore = Score;

        PlayerStateManager.Instance.RegisterPlayer(Object.InputAuthority, this);

        if (HasInputAuthority)
        {
            Runner.SetPlayerObject(Runner.LocalPlayer, Object);

            var userId = Runner.AuthenticationValues?.UserId;
            StableUserId = !string.IsNullOrEmpty(userId) ? userId : Runner.LocalPlayer.ToString();

            // Apply pending score restore if set
            if (PendingScoreRestore >= 0)
            {
                SetScore(PendingScoreRestore);
                Debug.Log($"[NetworkedPlayer] Score restored to {PendingScoreRestore}");
                PendingScoreRestore = -1;
            }
        }

        Debug.Log($"[NetworkedPlayer] Spawned for player {Object.InputAuthority} | HasInputAuthority: {HasInputAuthority} | HasStateAuthority: {HasStateAuthority}");

        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Color[] colors = { Color.blue, Color.red, Color.green, Color.magenta };
            int idx = Object.InputAuthority.RawEncoded % colors.Length;
            renderer.material.color = colors[idx];
        }
    }

    // Add this method — called by PlayerStateManager.AddScore:
    public void SetScore(int newScore)
    {
        Score = newScore;
        CachedScore = newScore;  // keep cache in sync
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