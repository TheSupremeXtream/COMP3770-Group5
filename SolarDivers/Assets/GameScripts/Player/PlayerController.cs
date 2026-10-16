using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float speed;

    private CharacterController charController;

    private InputAction moveinput;

    
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        charController = GetComponent<CharacterController>();
        moveinput = InputSystem.actions.FindAction("Move");

        
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveVal = moveinput.ReadValue<Vector2>();

        HandleMovement(moveVal);
    }

    void HandleMovement(Vector2 moveVal)
    {

        Vector3 newVec = transform.TransformVector(moveVal.x*speed, 0, moveVal.y * speed);

        Vector3 localPosition = transform.localPosition;

        
        charController.Move(newVec);
    }
}
