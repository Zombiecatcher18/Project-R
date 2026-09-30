using UnityEngine;

public class TurnController : MonoBehaviour
{
    public BattleState state = BattleState.START;

    public void SetState(BattleState newState)
    {
        state = newState;
        Debug.Log("STATE → " + newState);
    }
}
