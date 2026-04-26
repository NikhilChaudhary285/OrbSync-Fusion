using UnityEngine;
using Fusion;

/// <summary>
/// Draws an on-screen debug overlay using Unity's built-in IMGUI.
/// Useful during testing — remove or #if DEBUG before shipping.
/// </summary>
public class DebugHUD : MonoBehaviour
{
    private GUIStyle _style;

    private void OnGUI()
    {
        var runner = NetworkManager.Runner;
        if (runner == null) return;

        _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };

        var ps = PlayerStateManager.Instance;
        var rm = RejoinManager.Instance;
        var om = GameManager.Instance?.OrbManager;

        string info = $"[Fusion Debug]\n" +
                      $"Mode: {runner.GameMode}\n" +
                      $"Player: {runner.LocalPlayer}\n" +
                      $"Is Host: {runner.IsSharedModeMasterClient}\n" +
                      $"My Score: {(ps != null ? ps.GetScore(runner.LocalPlayer).ToString() : "?")}\n" +
                      $"Tick: {runner.Tick}";

        GUI.Label(new Rect(10, 10, 300, 200), info, _style);
    }
}