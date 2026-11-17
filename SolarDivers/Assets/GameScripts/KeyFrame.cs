using System;
using UnityEngine;

public class KeyFrame : MonoBehaviour
{
    [SerializeField] int ID = 1;
    [SerializeField] Boolean overrideCameraSpeed = false;
    [SerializeField] float overrideSpeed = 1.0f;
    //If false then camera will continue to next key frame after reaching this one. Else stays on this key frame.
    [SerializeField] public bool cameraStay = false;
    
    [SerializeField] public bool useBezier;
    public Quaternion rotation;
    public Vector3 position;

    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        position = transform.position;
        rotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}
