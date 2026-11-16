using UnityEngine;

public class LevelState_Mars1EnterOrbit : IState
{
    //Game camera
    [SerializeField] GameObject camera;
    [SerializeField] GameObject mars;
    [SerializeField] GameObject RailRig;

    

    public void OnEnter()
    {
        // 
    }

    public void UpdateState()
    {
        //Move camera to position
    }

    public void OnExit()
    {
        // "Must've been the wind"
    }
}
