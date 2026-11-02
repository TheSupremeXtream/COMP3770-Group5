
using UnityEngine;
using System.Collections;

public class EnemyMovement : MonoBehaviour
{
    [Header("Objects")]
    public Transform[] objects;

    [Header("Polygon Vertices")]
    public Transform[] vertices;

    [Header("Movement Settings")]
    public float moveDuration = 1f;
    public float waitBetweenMoves = 0.5f;
    public bool clockwise = true;
    private int[] currentVertexIndices;

    void Start()
    {
        currentVertexIndices = new int[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            currentVertexIndices[i] = i;
        }

        StartCoroutine(ContinuousMovement());
    }

    bool ValidateSetup()
    {
        if (objects == null || vertices == null ||
            objects.Length < 2 ||
            objects.Length != vertices.Length)
        {
            Debug.LogError("Objects and vertices must have the same size (at least 2).");
            return false;
        }

        foreach (Transform obj in objects)
        {
            if (obj == null)
            {
                Debug.LogError("One or more objects are missing.");
                return false;
            }
        }

        foreach (Transform vertex in vertices)
        {
            if (vertex == null)
            {
                Debug.LogError("One or more vertices are missing.");
                return false;
            }
        }

        if (moveDuration <= 0)
        {
            Debug.LogError("Move duration must be greater than zero.");
            return false;
        }

        return true;
    }

    IEnumerator ContinuousMovement()
    {
        while (true)
        {
            Debug.Log("Starting movement...");

            yield return StartCoroutine(MoveObjects());

            Debug.Log("Movement completed!");

            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator MoveObjects()
    {
        Vector3[] startPositions = new Vector3[objects.Length];
        Vector3[] targetPositions = new Vector3[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            startPositions[i] = objects[i].position;

            int nextIndex = clockwise
                ? (currentVertexIndices[i] + 1) % vertices.Length
                : (currentVertexIndices[i] - 1 + vertices.Length) % vertices.Length;

            targetPositions[i] = vertices[nextIndex].position;
        }

        // Debugging: Log the start and target positions for each object
        for (int i = 0; i < objects.Length; i++)
        {
            Debug.Log(
                objects[i].name +
                " | Current: " + objects[i].position +
                " | Target: " + targetPositions[i]
            );
        }

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].position = Vector3.Lerp(
                    startPositions[i],
                    targetPositions[i],
                    t
                );
            }

            yield return null;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            objects[i].position = targetPositions[i];

            currentVertexIndices[i] = clockwise
                ? (currentVertexIndices[i] + 1) % vertices.Length
                : (currentVertexIndices[i] - 1 + vertices.Length) % vertices.Length;
        }
    }
}
