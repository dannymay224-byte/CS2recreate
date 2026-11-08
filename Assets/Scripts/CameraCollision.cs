using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    [SerializeField] private float collisionDistance = 1.0f;
    [SerializeField] private LayerMask obstacleMask;

    public void HandleCollision(ref Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;

        if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, collisionDistance,
                obstacleMask))
        {
            targetPosition = hit.point - direction.normalized * 0.5f;
        }
    }

}