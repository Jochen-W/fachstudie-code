using UnityEngine;

public class RotateImage : MonoBehaviour
{
    public float degreesPerSecond;
    new RectTransform transform;


    void Start()
    {
        transform = GetComponent<RectTransform>();

    }

    void Update()
    {
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, transform.localEulerAngles.z - degreesPerSecond * Time.deltaTime);
    }
}
