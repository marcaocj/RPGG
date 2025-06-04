using UnityEngine;
using System.Collections.Generic;

// ================================
// CameraController.cs - Implementação completa
// ================================
public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -10);
    public float followSpeed = 2f;
    public float rotationSpeed = 2f;
    
    [Header("Camera Limits")]
    public float minDistance = 3f;
    public float maxDistance = 15f;
    public float minHeight = 1f;
    public float maxHeight = 10f;
    
    private Vector3 currentVelocity;
    
    private void Start()
    {
        if (target == null)
        {
            // Tentar encontrar o player automaticamente
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / followSpeed);
        
        transform.LookAt(target);
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
}
