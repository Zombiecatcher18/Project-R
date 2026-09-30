// EnemyTargetSelector.cs
using UnityEngine;

[RequireComponent(typeof(EnemyAttackController))]
public class EnemyTargetSelector : MonoBehaviour
{
    private EnemyAttackController controller;

    void Awake()
    {
        controller = GetComponent<EnemyAttackController>();
    }

    void OnMouseDown()
    {
        if (BattleManager.Instances != null)
            BattleManager.Instances.SetCurrentTarget(controller);
    }
}
