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
        // Start networking immediately when game launches
        NetworkManager.StartGame(defaultRoomName);
    }

    /// <summary>
    /// Called when THIS client's local player successfully connects.
    /// </summary>
    public void OnLocalPlayerJoined(PlayerRef player)
    {
        // Check if this is a rejoin
        if (RejoinManager.TryGetSavedData(player, out PlayerSaveData savedData))
        {
            // Rejoin path: restore position and score
            PlayerStateManager.SpawnAndRestorePlayer(player, savedData);
            Debug.Log($"[GameManager] Player {player} REJOINED — restoring data");
        }
        else
        {
            // Fresh join path
            PlayerStateManager.SpawnFreshPlayer(player);
            Debug.Log($"[GameManager] Player {player} joined fresh");
        }
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