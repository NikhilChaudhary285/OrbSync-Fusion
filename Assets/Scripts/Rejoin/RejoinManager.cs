using UnityEngine;
using Fusion;

public class RejoinManager : MonoBehaviour
{
    public bool TryGetSavedData(PlayerRef player, out PlayerSaveData data)
    {
        data = default;
        return false;
    }

    public void StartRejoinTimer(PlayerRef player) { }

    public void SavePlayerData(PlayerRef player, PlayerSaveData data) { }
}