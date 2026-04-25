using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

/// <summary>
/// Handles starting Fusion, joining/creating rooms, and player connect/disconnect events.
/// This is the entry point for all networking.
/// </summary>
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("References")]
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;

    // The active runner — one per client
    public static NetworkRunner Runner { get; private set; }

    // Called by GameManager on start
    public async void StartGame(string roomName)
    {
        // Create the NetworkRunner at runtime (not in scene)
        var runnerGO = new GameObject("NetworkRunner");
        Runner = runnerGO.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true; // This client will send input

        // Start or join a room
        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,          // Shared Mode - no dedicated server
            SessionName = roomName,                 // Room name players use to join
            Scene = SceneRef.FromIndex(0),    // Load scene index 0 (your MainScene)
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = 4                        // Max 4 players
        });

        if (result.Ok)
        {
            Debug.Log($"[NetworkManager] Joined room: {roomName}");
        }
        else
        {
            Debug.LogError($"[NetworkManager] Failed to start: {result.ShutdownReason}");
        }
    }

    // ─── INetworkRunnerCallbacks ───────────────────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player joined: {player}");

        // Only the host (StateAuthority) spawns player objects
        // In Shared Mode, each player spawns THEIR OWN object
        if (player == runner.LocalPlayer)
        {
            // Ask GameManager to handle spawn + possible rejoin restore
            GameManager.Instance.OnLocalPlayerJoined(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player left: {player}");
        GameManager.Instance.OnPlayerLeft(player);
    }

    // ─── Unused callbacks (required by interface — leave empty) ───────────

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}