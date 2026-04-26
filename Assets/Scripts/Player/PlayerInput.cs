using Fusion;
using UnityEngine;

/// <summary>
/// The network input struct. Fusion serializes this and sends it
/// from the InputAuthority to all other clients every tick.
/// MUST implement INetworkInput.
/// </summary>
public struct PlayerInput : INetworkInput
{
    // We store direction as a Vector2 (X = horizontal, Y = vertical)
    // Vector2 is compact — only 8 bytes per tick
    public Vector2 Direction;
}