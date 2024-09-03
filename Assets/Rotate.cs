using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    private float rotationSpeed = 360f;

    void Update()
    {
        float rotationThisFrame = rotationSpeed * Time.deltaTime;

        transform.Rotate(Vector3.up, rotationThisFrame);
    }
}
