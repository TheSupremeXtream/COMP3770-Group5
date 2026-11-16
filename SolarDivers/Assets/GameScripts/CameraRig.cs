using NUnit;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CameraRig : MonoBehaviour
{
    //When in a keyFrame, if this is true then rig will proceed to the next keyFrame.
    [SerializeField] bool goToNextKF = true;
    //Default speed for rig movement. The keyFrames can override this with their own speed.
    [SerializeField] float defaultSpeed = 1.0f;

    //Get gameObject with children of keyframe gameobjects. Simply add another keyframe gameobject to the parent to add more.
    [SerializeField] GameObject keyFrames;
    private Queue<KeyFrame> keyFrameList = new Queue<KeyFrame>();
    private KeyFrame currentKeyFrame;
    private KeyFrame nextKeyFrame;

    public float duration = 4f;
    float elapsed;

    Quaternion startRotation, endRotation;
    Vector3 startPos, endPos;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < keyFrames.transform.childCount; i++) {
            GameObject child = keyFrames.transform.GetChild(i).gameObject;
            keyFrameList.Enqueue(child.GetComponent<KeyFrame>());
        }
        Debug.Log(keyFrameList.Count);

        currentKeyFrame = keyFrameList.Dequeue();
        transform.position = currentKeyFrame.position;
        transform.rotation = currentKeyFrame.rotation;
        startPos = transform.position;
        startRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (goToNextKF)
        {
            AdvanceToNextKeyFrame(transform.position, Time.deltaTime);
        }
        //Debug.Log("CamPos: " +  transform.position);

    }

    

    //Camera advances to the next keyframe in the list. It will stop at the last keyframe.
    void AdvanceToNextKeyFrame(Vector3 position, float dt)
    {
        if (nextKeyFrame == null || currentKeyFrame == nextKeyFrame)
        {
            if (keyFrameList.Count > 0)
            {
                nextKeyFrame = keyFrameList.Dequeue();
                endPos = nextKeyFrame.position;
                endRotation = nextKeyFrame.rotation;
                startPos = transform.position;
                startRotation = transform.rotation;

            }
            else
            {
                return;
            }
        }

        

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.position = Vector3.Lerp(startPos, endPos, t);
        transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

        if (t >= 1f)
        {
            elapsed = 0f;
        }

        if (IsCameraAtNextKeyFrame())
        {
            currentKeyFrame = nextKeyFrame;
            return;
        }

        /*
        Vector3 newVec = GetDirectionToNextKeyFrame();
        float amount = defaultSpeed * dt;


        position.x += newVec.x * amount;
        position.y += newVec.y * amount;
        position.z += newVec.z * amount;

        transform.position = position;
        */
    }

    private bool FloatApprox(float a, float b, float tolerance)
    {
        return (Mathf.Abs(a - b) < tolerance);
    }

    private bool IsCameraAtNextKeyFrame()
    {
        Vector3 camPos = transform.position;
        float tolerance = 0.01f;
        if (FloatApprox(camPos.x, endPos.x, tolerance) && FloatApprox(camPos.y, endPos.y, tolerance) && FloatApprox(camPos.z, endPos.z, tolerance))
        {
            return true;
        }
        return false;
    }

    

    private Vector3 GetDirectionToNextKeyFrame()
    {
        
        Vector3 retVal = nextKeyFrame.position - currentKeyFrame.position;
        retVal.Normalize();
        return retVal;
    }

    //
    private void ChangeRotation()
    {

    }
}
