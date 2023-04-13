using UnityEngine;
using System.Collections.Generic;

// simple CameraController to to rotate and move the camera.
// Use the LeftShift key to duplicate your movement speed.
public class CameraController : MonoBehaviour
{

    public LatLonInfo latLonInfo;
    public ConfigInfo configInfo;
    public float moveSpeed = 12f;
    public Vector2 rotationSpeed = Vector2.one;

    private Vector3 eulerStart = Vector3.forward;
    private Vector3 mousePosStart = Vector3.forward;

    private Dictionary<KeyCode, (float, float)> keyToVecDict = new Dictionary<KeyCode, (float, float)>(){
        {KeyCode.W, ( 1, 0)},
        {KeyCode.A, ( 0,-1)},
        {KeyCode.S, (-1, 0)},
        {KeyCode.D, ( 0, 1)},
    };

    async void Awake()
    {
        (int tile_x, int tile_y) = latLonInfo.AsTileXY(configInfo);
        Texture2D heightMap = await CachedRequestMaker.GetTextureTileData(configInfo, tile_x, tile_y, TileType.ELEVATION);
        float groundHeight = CachedRequestMaker.HeightFromRGB(heightMap.GetPixel(128, 128));
        gameObject.transform.position = new Vector3(0, (groundHeight + 10) * latLonInfo.GetHeightMultiplier(configInfo), 0);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // start of press
            eulerStart = transform.eulerAngles;
            mousePosStart = Input.mousePosition;
        }
        // Mouse rotation
        if (Input.GetMouseButton(1))
        {
            // while pressed
            Vector3 mouseDiff = Input.mousePosition - mousePosStart;
            Vector3 angles = Vector3.Scale(mouseDiff, new Vector3(360f / Screen.width, 180f / Screen.height, 0));
            Vector3 realRotation = eulerStart + new Vector3(-angles.y * rotationSpeed.y, angles.x * rotationSpeed.x, 0);
            transform.eulerAngles = realRotation;
        }


        // WASD-movement
        float sprint = Input.GetKey(KeyCode.LeftShift) ? 2 : 1;
        Vector3 dir = Vector3.zero;
        foreach ((KeyCode key, (float fwMult, float rightMult)) in keyToVecDict)
        {
            if (Input.GetKey(key))
            {
                dir += fwMult * transform.forward + rightMult * transform.right;
            }
        }
        transform.position += dir * moveSpeed * sprint * Time.deltaTime;
    }

}