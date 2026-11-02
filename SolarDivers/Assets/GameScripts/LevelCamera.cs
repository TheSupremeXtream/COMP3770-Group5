using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelCamera : MonoBehaviour
{
    //Default speed for the camera. The keyFrames can override this with their own camera speed.
    [SerializeField] float cameraSpeed = 1.0f;

    //Get gameObject with children of keyframe gameobjects. Simply add another keyframe gameobject to the parent to add more.
    [SerializeField] GameObject keyFrames;
    private Queue<KeyFrame> keyFrameList = new Queue<KeyFrame>();
    private KeyFrame currentKeyFrame;
    private KeyFrame nextKeyFrame;
    

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
        Advance(transform.position, Time.deltaTime);
        //Debug.Log("CamPos: " +  transform.position);

    }
    private bool FloatApprox(float a, float b, float tolerance)
    {
        return (Mathf.Abs(a - b) < tolerance);
    }

    private bool IsCameraAtNextKeyFrame()
    {
        Vector3 camPos = transform.position;
        Vector3 nextPos = nextKeyFrame.position;
        float tolerance = 0.01f;
        if (FloatApprox(camPos.x, nextPos.x, tolerance) && FloatApprox(camPos.y, nextPos.y, tolerance) && FloatApprox(camPos.z, nextPos.z, tolerance))
        {
            return true;
        }
        return false;
    }

    //Camera advances to the next keyframe in the list. It will stop at the last keyframe.
    void Advance(Vector3 position, float dt)
    {
        if (nextKeyFrame == null || currentKeyFrame == nextKeyFrame)
        {
            if (keyFrameList.Count > 0)
            {
                nextKeyFrame = keyFrameList.Dequeue();
            }
            else
            {
                return;
            }
        }

        if (IsCameraAtNextKeyFrame())
        {
            currentKeyFrame = nextKeyFrame;
            return;
        }

        Vector3 newVec = GetDirectionToNextKeyFrame();
        float amount = cameraSpeed * dt;
        

        position.x += newVec.x * amount;
        position.y += newVec.y * amount;
        position.z += newVec.z * amount;

        transform.position = position;
    }

    private Vector3 GetDirectionToNextKeyFrame()
    {
        
        Vector3 retVal = nextKeyFrame.position - currentKeyFrame.position;
        retVal.Normalize();
        return retVal;
    }
}
