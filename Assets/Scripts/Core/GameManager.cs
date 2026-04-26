using UnityEngine;
using Fusion;

/// <summary>
/// Central coordinator. Holds references to all managers.
/// Uses a singleton pattern — only one exists in the scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Manager References")]
    public NetworkManager NetworkManager;
    public OrbManager OrbManager;
    public PlayerStateManager PlayerStateManager;
    public RejoinManager RejoinManager;

    [Header("Settings")]
    [SerializeField] private string defaultRoomName = "OrbRoom01";

    public static bool VerboseLogs = true; // Set false before submission

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persist across scene loads
    }

    private void Start()
    {
        // Verify all references before starting
        Debug.Log($"[GameManager] NetworkManager: {NetworkManager != null}");
        Debug.Log($"[GameManager] OrbManager: {OrbManager != null}");
        Debug.Log($"[GameManager] PlayerStateManager: {PlayerStateManager != null}");
        Debug.Log($"[GameManager] RejoinManager: {RejoinManager != null}");
        Debug.Log($"[GameManager] PlayerPrefab: {PlayerStateManager?.playerPrefab != null}");

        // Start networking immediately when game launches
        NetworkManager.StartGame(defaultRoomName);

        // OrbManager.Initialize is called from OnLocalPlayerJoined 
        // after runner is confirmed connected

        // Add a debug HUD -> For Centralized log levels
        if (VerboseLogs && GetComponent<DebugHUD>() == null)
        {
            gameObject.AddComponent<DebugHUD>();
        }
    }

    /// <summary>
    /// Called when THIS client's local player successfully connects.
    /// </summary>
    public void OnLocalPlayerJoined(PlayerRef player)
    {
        // Initialize orb system once connected (host check is inside)
        OrbManager.Initialize(NetworkManager.Runner);

        // Check if this is a rejoin
        if (RejoinManager.TryGetSavedData(player, out PlayerSaveData savedData))
        {
            // Rejoin path: restore position and score
            PlayerStateManager.SpawnAndRestorePlayer(player, savedData);
            Debug.Log($"[GameManager] Player {player} REJOINED — restoring data: score={savedData.Score}");
        }
        else
        {
            // Fresh join path
            PlayerStateManager.SpawnFreshPlayer(player);
            Debug.Log($"[GameManager] Player {player} joined fresh");
        }

        // NOTE: Orb state sync for late joiners is handled differently.
        // In Shared Mode, the host will broadcast active orbs via RPC on join.
        // See OrbManager.SyncStateToPlayer — call this from the host side - In NetworkManager.cs - OnPlayerJoined() Method

    }

    /// <summary>
    /// Called when any player disconnects.
    /// </summary>
    public void OnPlayerLeft(PlayerRef player)
    {
        // Save their state for possible rejoin
        PlayerStateManager.SavePlayerData(player);

        // Start the 30-second rejoin timer
        RejoinManager.StartRejoinTimer(player);
    }
}