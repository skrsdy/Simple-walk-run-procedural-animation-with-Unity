using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera : MonoBehaviour
{ 
    public PA pa;
    public UnityEngine.Camera mainCamera;
    public Collider target; 
    public Transform bodyForward;  
    void Update()
    {
        mainCamera.transform.LookAt(target.transform); 
        Vector3 cameraForward = transform.forward; 
        cameraForward.y = 0;
        Vector3 left = new(-cameraForward.z, 0, cameraForward.x);
        Vector3 forward = Vector3.zero; 
        if (Input.GetKey(KeyCode.W)) forward += cameraForward; 
        if (Input.GetKey(KeyCode.A)) forward += left; 
        if (Input.GetKey(KeyCode.S)) forward += -cameraForward; 
        if (Input.GetKey(KeyCode.D)) forward += -left; 
        bodyForward.position = target.transform.position+ forward.normalized;
        pa.movementDirection = forward.normalized;
        float mouseX = Input.GetAxis("Mouse X"); 
        pa.transform.rotation *= Quaternion.Euler(0, 200 * mouseX * Time.deltaTime, 0); 
    }
}
