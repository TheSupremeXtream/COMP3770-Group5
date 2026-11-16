using UnityEngine;

public class StateController : MonoBehaviour
{
    IState currentState;

    

    void Start()
    {
        currentState = new LevelState_Mars1EnterOrbit();
    }

    void Update()
    {
        currentState.UpdateState();
    }

    public void ChangeState(IState newState)
    {
        currentState.OnExit();
        currentState = newState;
        currentState.OnEnter();
    }
}

public interface IState
{
    public void OnEnter();

    public void UpdateState();

    

    public void OnExit();
}
