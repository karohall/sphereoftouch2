using UnityEngine;
using UnityEngine.UI;
using Klak.Spout;

public class WebcamBackground : MonoBehaviour
{
    [SerializeField] SpoutReceiver _spoutReceiver;
    private RawImage rawImage;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
    }

    void Update()
    {
        if (_spoutReceiver != null && _spoutReceiver.receivedTexture != null)
        {
            rawImage.texture = _spoutReceiver.receivedTexture;
        }
    }
}