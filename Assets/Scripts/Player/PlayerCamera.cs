using UnityEngine;
using Fusion;

/// <summary>
/// Follows the LOCAL player only.
/// Attaches itself in Spawned() — runs on the InputAuthority client only.
/// </summary>
public class PlayerCamera : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -10f);
    [SerializeField] private float smoothSpeed = 8f;

    private Camera _cam;

    public override void Spawned()
    {
        // Only the local player controls the camera
        if (!HasInputAuthority) return;

        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("[PlayerCamera] No Main Camera found!");
            return;
        }

        // Snap camera immediately to avoid startup lerp
        _cam.transform.position = transform.position + offset;
        Debug.Log("[PlayerCamera] Camera attached to local player");
    }

    private void LateUpdate()
    {
        // Only move camera for the local player's object
        if (!HasInputAuthority || _cam == null) return;

        Vector3 target = transform.position + offset;
        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position,
            target,
            smoothSpeed * Time.deltaTime
        );
    }
}