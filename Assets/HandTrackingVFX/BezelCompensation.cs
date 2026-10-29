using UnityEngine;

[ExecuteAlways]
public class BezelCompensation : MonoBehaviour
{
    [Range(0f, 0.1f)] public float bezelX = 0.02f;
    [Range(0f, 0.1f)] public float bezelY = 0.02f;

    void Update()
    {
        GetComponent<Camera>().rect = new Rect(
            -bezelX, -bezelY,
            1f + bezelX * 2f, 1f + bezelY * 2f
        );
    }
}