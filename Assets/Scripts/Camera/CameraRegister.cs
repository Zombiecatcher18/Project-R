using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Companion component for Cinemachine cameras that registers/unregisters with CameraSwitcher.
/// Automatically notifies global camera system when camera enters/leaves scene.
/// 
/// Lifecycle:
/// - OnEnable: Called when camera GameObject becomes active → Register with CameraSwitcher
/// - OnDisable: Called when camera destroyed or disabled → Unregister from CameraSwitcher
/// 
/// Design Pattern:
/// Each CinemachineCamera has CameraRegister component attached.
/// CameraRegister handles registration lifecycle automatically.
/// CameraSwitcher maintains global registry for priority switching.
/// 
/// Usage: Simply attach to any CinemachineCamera GameObject; it auto-registers.
/// </summary>
public class CameraRegister : MonoBehaviour
{
    /// <summary>
    /// Unity lifecycle: Called when camera GameObject/component enabled.
    /// Registers this camera with global CameraSwitcher registry.
    /// 
    /// Called by: Physics system on GameObject.SetActive(true) or component enable
    /// Action: CameraSwitcher.Register() adds camera to global list
    /// </summary>
    public void OnEnable()
    {
        CameraSwitcher.Register(GetComponent<CinemachineCamera>());
    }

    /// <summary>
    /// Unity lifecycle: Called when camera GameObject/component disabled or destroyed.
    /// Unregisters this camera from global CameraSwitcher registry.
    /// 
    /// Called by: Physics system on GameObject.SetActive(false), scene unload, or Destroy()
    /// Action: CameraSwitcher.Unregister() removes camera from global list
    /// </summary>
    public void OnDisable()
    {
        CameraSwitcher.Unregister(GetComponent<CinemachineCamera>());
    }
}
