using UnityEngine;
using Fusion;

/// <summary>
/// Attached to the player's NetworkObject prefab.
/// Syncs position and score across the network automatically.
/// Each player has authority over their own object (InputAuthority in Shared Mode).
/// </summary>
public class NetworkedPlayer : NetworkBehaviour
{
    // [Networked] vars are automatically synced to all clients by Fusion
    [Networked] public int Score { get; set; }
    [Networked] public Vector3 Position { get; set; }

    // Cached ref for local control
    private CharacterController _cc;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 5f;

    public override void Spawned()
    {
        _cc = GetComponent<CharacterController>();

        // Tell PlayerStateManager to track this player
        PlayerStateManager.Instance.RegisterPlayer(Object.InputAuthority, this);

        Debug.Log($"[NetworkedPlayer] Spawned for player {Object.InputAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        // Only the local player (InputAuthority) moves their character
        if (!HasInputAuthority) return;

        HandleMovement();

        // Sync our position to the networked var so others see it
        Position = transform.position;
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        var move = new Vector3(h, 0, v) * moveSpeed * Runner.DeltaTime;

        if (_cc != null)
            _cc.Move(move);
        else
            transform.position += move;
    }

    /// <summary>
    /// Teleports the player to a saved position (used on rejoin).
    /// Only call on the object's InputAuthority.
    /// </summary>
    public void RestorePosition(Vector3 pos)
    {
        transform.position = pos;
        Position = pos;
    }
}