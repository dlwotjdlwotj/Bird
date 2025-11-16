using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The bird object to follow..")]
    public Transform birdTarget;

    void Start()
    {
        // Initialize camera position
        Vector3 initialPos = transform.position;
        initialPos.x = birdTarget.position.x;
        initialPos.y = birdTarget.position.y;
        transform.position = initialPos;
    }

    void LateUpdate()
    {
        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        // Always center the bird in camera view instantly
        Vector3 newPos = transform.position;
        newPos.x = birdTarget.position.x;
        newPos.y = birdTarget.position.y;
        transform.position = newPos;
    }
}
