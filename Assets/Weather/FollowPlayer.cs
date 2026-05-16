using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public Transform player; // Assign Main Camera atau Player
    public bool followX = true;
    public bool followZ = true;

    void LateUpdate()
    {
        Vector3 pos = transform.position;
        if (followX) pos.x = player.position.x;
        if (followZ) pos.z = player.position.z;
        transform.position = pos;
    }
}