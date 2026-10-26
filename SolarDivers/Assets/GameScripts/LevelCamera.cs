using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelCamera : MonoBehaviour
{
    [SerializeField] float cameraSpeed = 1.0f;

    [SerializeField] GameObject keyFrames;
    private Queue<KeyFrame> keyFrameList = new Queue<KeyFrame>();
    private KeyFrame currentKeyFrame;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        for (int i = 0; i < keyFrames.transform.childCount; i++) {
            GameObject child = keyFrames.transform.GetChild(i).gameObject;
            keyFrameList.Enqueue(child.GetComponent<KeyFrame>());
        }

        currentKeyFrame = keyFrameList.Dequeue();
        transform.position = currentKeyFrame.position;
    }

    // Update is called once per frame
    void Update()
    {
        

        
    }
}
