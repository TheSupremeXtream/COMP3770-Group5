using System;
using UnityEngine;

public class KeyFrame : MonoBehaviour
{
    [SerializeField] int ID = 1;
    [SerializeField] Boolean overrideCameraSpeed = false;
    [SerializeField] float overrideSpeed = 1.0f;
    [SerializeField] Vector3 rotation;
    public Vector3 position;

    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        position = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
