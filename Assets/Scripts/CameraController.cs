using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform cameraTransform;
    private CameraCollision cameraCollision;
    public float panSpeed = 20f;
    public float panSmoothing = 8f;
    public Vector2 LimitX;
    public Vector2 LimitZ;
    public float zoomMin = 5f;
    public float zoomMax = 120f;
    public float zoomSpeed = 15f;
    public float zoomSmoothing = 8f;
 
    public float pitchAtMinZoom = 70f;
    public float pitchAtMaxZoom = 30f;
 
    public float rotateSpeed = 120f;
    public float rotateSmoothing = 8f;
 
    private float currentZoom, targetZoom;
    private float currentYaw, targetYaw;
    private float currentPitch, targetPitch;
    private Vector3 targetPosition;
    private Vector3 rotateDragStart;
    void Start()
    {
        cameraCollision = GetComponent<CameraCollision>();
        
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
 
        currentZoom = targetZoom = Mathf.Clamp(transform.position.y, zoomMin, zoomMax);
        currentYaw = targetYaw = transform.eulerAngles.y;
 
        float t = Mathf.InverseLerp(zoomMin, zoomMax, currentZoom);
        currentPitch = targetPitch = Mathf.Lerp(pitchAtMinZoom, pitchAtMaxZoom, t);
 
        targetPosition = transform.position;
    }

    void Update()
    {
        // Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed * targetZoom * 0.1f, zoomMin, zoomMax);
 
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomSmoothing);
 
        float zoomT = Mathf.InverseLerp(zoomMin, zoomMax, currentZoom);
        targetPitch = Mathf.Lerp(pitchAtMinZoom, pitchAtMaxZoom, zoomT);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * rotateSmoothing);
 
        // Rotation
        if (Input.GetKey(KeyCode.Q)) targetYaw -= rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) targetYaw += rotateSpeed * Time.deltaTime;
        if (Input.GetMouseButtonDown(2)) rotateDragStart = Input.mousePosition;
        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - rotateDragStart;
            targetYaw += delta.x * 0.3f;
            rotateDragStart = Input.mousePosition;
        }
 
        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotateSmoothing); 
        // Pan
        Vector3 move = Vector3.zero;
        Vector3 forward = Quaternion.Euler(0, currentYaw, 0) * Vector3.forward;
        Vector3 right   = Quaternion.Euler(0, currentYaw, 0) * Vector3.right;
 
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            move += forward * v + right * h;
 
        if (Input.GetMouseButton(1))
        {
            move -= right   * Input.GetAxis("Mouse X") * 0.5f;
            move -= forward * Input.GetAxis("Mouse Y") * 0.5f;
        }
 
        float speedScale = Mathf.Lerp(0.3f, 1f, zoomT);
        targetPosition += move.normalized * panSpeed * speedScale * Time.deltaTime;
        targetPosition.x = Mathf.Clamp(targetPosition.x, LimitX.x, LimitX.y);
        targetPosition.z = Mathf.Clamp(targetPosition.z, LimitZ.x, LimitZ.y);
        if (cameraCollision != null)
        {
            cameraCollision.HandleCollision(ref targetPosition);
        }

        // Apply
        Vector3 desiredPosition = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * panSmoothing);
        desiredPosition.y = currentZoom;

    
        transform.position = desiredPosition;
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
 
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }
    }
}
