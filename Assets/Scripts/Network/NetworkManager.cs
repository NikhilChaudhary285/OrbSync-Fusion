// Assets/Scripts/Network/NetworkManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("References")]
    [SerializeField] private NetworkObject playerPrefab; // Assign in Inspector

    public static NetworkRunner Runner { get; private set; }

    // ── Startup ───────────────────────────────────────────────────────────

    public async void StartGame(string roomName)
    {
        var runnerGO = new GameObject("NetworkRunner");
        DontDestroyOnLoad(runnerGO); // Keep runner alive across scenes

        Runner = runnerGO.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;

        // IMPORTANT: Add THIS monobehaviour as the callback listener
        Runner.AddCallbacks(this);

        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = roomName,
            Scene = SceneRef.FromIndex(0),
            SceneManager = runnerGO.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = 4
        });

        if (result.Ok)
            Debug.Log($"[NetworkManager] Joined room: {roomName}");
        else
            Debug.LogError($"[NetworkManager] Failed: {result.ShutdownReason}");
    }

    // ── Callbacks ─────────────────────────────────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] OnPlayerJoined fired. player={player}, localPlayer={runner.LocalPlayer}");

        if (player == runner.LocalPlayer)
        {
            // Small null guard — GameManager should always exist but be safe
            if (GameManager.Instance == null)
            {
                Debug.LogError("[NetworkManager] GameManager.Instance is null during OnPlayerJoined!");
                return;
            }
            GameManager.Instance.OnLocalPlayerJoined(player);
        }

        // Host re-syncs all orbs whenever anyone joins (handles late joiners)
        if (runner.IsSharedModeMasterClient)
        {
            Debug.Log($"[NetworkManager] Host detected join — syncing orb state to {player}");
            GameManager.Instance?.OrbManager?.SyncStateToPlayer(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player left: {player}");
        GameManager.Instance?.OnPlayerLeft(player);
    }

    // ── Required empty callbacks ──────────────────────────────────────────

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // This runs ONLY on the local client, every network tick
        // Collect raw Unity input and package it for Fusion
        var playerInput = new PlayerInput
        {
            Direction = new Vector2(
                Input.GetAxisRaw("Horizontal"),  // A/D or Left/Right
                Input.GetAxisRaw("Vertical")     // W/S or Up/Down
            )
        };

        // Hand it to Fusion — it will route it to the correct NetworkObject
        input.Set(playerInput);
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NetworkManager] Shutdown: {shutdownReason}");
    }
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[NetworkManager] Connected to server");
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[NetworkManager] Disconnected: {reason}");
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessage message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}