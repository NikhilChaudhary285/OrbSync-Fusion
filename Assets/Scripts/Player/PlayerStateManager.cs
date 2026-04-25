using UnityEngine;
using Fusion;

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SpawnFreshPlayer(PlayerRef player) { }
    public void SpawnAndRestorePlayer(PlayerRef player, PlayerSaveData data) { }
    public void SavePlayerData(PlayerRef player) { }
    public void AddScore(int score) { }
}