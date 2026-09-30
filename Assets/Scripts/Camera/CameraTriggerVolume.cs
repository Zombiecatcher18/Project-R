using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Trigger volume that switches active camera when player enters zone.
/// Used to define camera control areas in exploration scenes (rooms, overlooks, transitions).
/// 
/// Design Pattern:
/// - Place trigger volumes at scene boundaries or significant camera transition points
/// - Each volume has assigned CinemachineCamera
/// - Player enters volume → CameraSwitcher activates that camera
/// - Cinemachine smoothly blends from current to new camera
/// 
/// Requirements:
/// - BoxCollider (configured as trigger)
/// - Rigidbody (configured as kinematic)
/// - CinemachineCamera reference (assigned in inspector)
/// 
/// Usage:
/// 1. Create empty GameObject at camera transition boundary
/// 2. Add BoxCollider and Rigidbody components
/// 3. Attach CameraTriggerVolume component
/// 4. Assign target CinemachineCamera
/// 5. Adjust boxSize to define trigger zone
/// 6. Box Gizmo displays as green wireframe in editor
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class CameraTriggerVolume : MonoBehaviour
{
    /// <summary>
    /// Target CinemachineCamera to switch to when player enters trigger.
    /// Assigned in inspector; can reference camera in same scene or child objects.
    /// </summary>
    [SerializeField] private CinemachineCamera cam;

    /// <summary>
    /// Box collider dimensions (width, height, depth) for trigger zone.
    /// Scaled relative to transform, not world space.
    /// Example: (10, 5, 20) = 10 units wide, 5 high, 20 deep
    /// </summary>
    [SerializeField] private Vector3 boxSize;

    /// <summary>
    /// Cached BoxCollider component (configured as trigger).
    /// </summary>
    BoxCollider box;

    /// <summary>
    /// Cached Rigidbody component (configured as kinematic for physics integration).
    /// </summary>
    Rigidbody rb;

    /// <summary>
    /// Unity lifecycle: Initialize and configure collider/rigidbody.
    /// 
    /// Actions:
    /// 1. Cache BoxCollider and Rigidbody components
    /// 2. Configure BoxCollider as trigger (isTrigger = true)
    /// 3. Set BoxCollider size from boxSize field
    /// 4. Configure Rigidbody as kinematic (non-dynamic, avoids physics simulation)
    /// </summary>
    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        rb = GetComponent<Rigidbody>();
        box.isTrigger = true;
        box.size = boxSize;

        rb.isKinematic = true;  // No physics simulation (static trigger)
    }

    /// <summary>
    /// Debug gizmo: Draw green wireframe box showing trigger volume.
    /// Only visible in editor Scene view, not at runtime.
    /// 
    /// Called by: Unity editor when Gizmos enabled
    /// Visual: Green outline box at trigger position with boxSize dimensions
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, boxSize);
    }

    /// <summary>
    /// Physics callback: Player entered trigger volume.
    /// 
    /// Actions:
    /// 1. Validate collision is player (by tag comparison)
    /// 2. Check if target camera is not already active (prevent redundant switching)
    /// 3. Call CameraSwitcher.SwitchCamera() to activate target camera
    /// 4. Cinemachine automatically blends to new camera over configured blend duration
    /// 
    /// Called by: Physics system when collider enters trigger
    /// Result: Smooth camera transition to assigned CinemachineCamera
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            if (CameraSwitcher.ActiveCamera != cam) 
                CameraSwitcher.SwitchCamera(cam);  // Avoid redundant switch
        }
    }
}
