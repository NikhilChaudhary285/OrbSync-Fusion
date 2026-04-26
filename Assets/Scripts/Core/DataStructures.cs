using UnityEngine;
using Fusion;

/// <summary>
/// Data saved when a player disconnects — used for rejoin restoration.
/// </summary>
// In DataStructures.cs — update PlayerSaveData:
[System.Serializable]
public struct PlayerSaveData
{
    public int Score;
    public Vector3 Position;
    public float TimeSaved;
    public bool IsValid;
    public string UserId;    // ← ADD THIS: stable identity across reconnects
}

/// <summary>
/// Represents one orb in the world. Synced via RPC, not NetworkObject.
/// </summary>
[System.Serializable]
public struct OrbData
{
    public int OrbId;        // Unique ID — prevents duplicates
    public Vector3 Position;
    public bool IsClaimed;    // True = orb has been collected
}