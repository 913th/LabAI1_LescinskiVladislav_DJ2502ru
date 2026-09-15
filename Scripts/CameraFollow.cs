using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;        // цель (персонаж)
    public Vector3 offset = new Vector3(0, 2f, -10);  // X=0, Y=2 (выше), Z=-10
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else return;
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;
    }
}