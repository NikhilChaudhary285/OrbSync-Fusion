using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple UI for entering a room name and starting the game.
/// For the assignment, you can skip this and just hard-code a room name.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button joinButton;

    private void Start()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked()
    {
        string room = string.IsNullOrEmpty(roomNameInput.text)
            ? "OrbRoom01"
            : roomNameInput.text;

        GameManager.Instance.NetworkManager.StartGame(room);
        gameObject.SetActive(false); // Hide UI after joining
    }
}