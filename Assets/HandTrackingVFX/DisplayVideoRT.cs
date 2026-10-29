using UnityEngine;
using UnityEngine.UI;

public class DisplayVideoRT : MonoBehaviour
{
    public RenderTexture videoRT;

    void Start()
    {
        GetComponent<RawImage>().texture = videoRT;
    }
}