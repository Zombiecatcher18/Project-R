using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Static registry and switcher for Cinemachine camera priority management.
/// Enables smooth camera transitions between multiple viewpoints (exploration, battle, cutscenes, etc.).
/// 
/// Architecture:
/// - Static singleton pattern for global camera control
/// - Maintains list of all CinemachineCamera instances via Register/Unregister
/// - Controls priority to enable/disable cameras (Cinemachine feature)
/// 
/// Priority System (Cinemachine):
/// - Active camera: Priority = 10 (highest, takes control)
/// - Inactive cameras: Priority = 0 (disabled)
/// - Cinemachine brain blends to highest-priority camera
/// - Allows smooth transitions and blending between cameras
/// 
/// Usage:
/// 1. CameraRegister (companion) calls Register() at scene load
/// 2. CameraTriggerVolume calls SwitchCamera() when player enters camera zone
/// 3. CameraSwitcher updates priorities, Cinemachine blends visually
/// 
/// Used by: Scene cameras (exploration, battle areas), UI cameras
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    // ===== CAMERA REGISTRY =====
    /// <summary>
    /// Static list of all Cinemachine cameras in current scene.
    /// Populated by Register() (called by CameraRegister components)
    /// Depleted by Unregister() (called on camera GameObject destruction)
    /// </summary>
    static List<CinemachineCamera> cameras = new List<CinemachineCamera>();

    /// <summary>
    /// Currently active camera (Priority=10).
    /// Updated by SwitchCamera() to track which camera is controlling view.
    /// Checked by IsActiveCamera() for query operations.
    /// </summary>
    public static CinemachineCamera ActiveCamera = null;

    // ===== QUERY METHODS =====
    /// <summary>
    /// Check if specified camera is currently active (has control).
    /// 
    /// Called by: UI elements, camera behavior logic, debug displays
    /// Returns: true if camera == ActiveCamera, false otherwise
    /// </summary>
    public static bool IsActiveCamera(CinemachineCamera camera)
    {
        return camera == ActiveCamera;
    }

    // ===== CAMERA SWITCHING =====
    /// <summary>
    /// Switches to specified camera, deactivating all others.
    /// 
    /// Process:
    /// 1. Set target camera Priority = 10 (highest, activates)
    /// 2. Update ActiveCamera reference
    /// 3. Loop through all registered cameras:
    ///    - If not the target camera and Priority != 0: set Priority = 0 (deactivate)
    ///    - Leaves Priority=0 cameras unchanged (already inactive)
    /// 
    /// Called by: CameraTriggerVolume, cutscene managers, battle transitions
    /// Effect: Cinemachine brain smoothly blends to target camera over blend duration
    /// </summary>
    public static void SwitchCamera(CinemachineCamera camera)
    {
        camera.Priority = 10;  // Activate target
        ActiveCamera = camera;

        // Deactivate all other cameras
        foreach (CinemachineCamera c in cameras)
        {
            if (c != camera && c.Priority != 0)
            {
                c.Priority = 0;
            }
        }
    }

    // ===== REGISTRATION =====
    /// <summary>
    /// Register camera to global list (enable tracking and switching).
    /// Called by CameraRegister.OnEnable() when camera enters scene.
    /// 
    /// Called by: CameraRegister (companion component on camera GameObjects)
    /// Action: Adds camera to static list for priority management
    /// </summary>
    public static void Register(CinemachineCamera camera)
    {
        cameras.Add(camera);
    }

    /// <summary>
    /// Unregister camera from global list (disable tracking).
    /// Called by CameraRegister.OnDisable() when camera destroyed.
    /// 
    /// Called by: CameraRegister (companion component on camera GameObjects)
    /// Action: Removes camera from static list
    /// </summary>
    public static void Unregister(CinemachineCamera camera)
    {
        cameras.Remove(camera);
    }
}
