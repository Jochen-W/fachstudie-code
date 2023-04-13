namespace Mapbox.Examples
{
    using Mapbox.Unity.Map;
    using Mapbox.Unity.Utilities;
    using Mapbox.Utils;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using System;
    using System.Collections.Generic;
    using TMPro;

    public class QuadTreeCameraMovement : MonoBehaviour
    {
        [Range(1, 20)]
        public float _panSpeed = 1.0f;
        float _zoomSpeed = 0.25f;

        [SerializeField]
        public Camera _referenceCamera;

        [SerializeField]
        AbstractMap _mapManager;

        private bool _dragStartedOnUI = false;
        private bool _isInitialized = false;
        private bool _shouldDrag;  // used for drag
        private Vector3 _origin;  // used for drag
        private Vector3 _mousePosition;  // used for drag
        private Vector3 _mousePositionPrevious;  // used for drag


        // own data
        public TextMeshProUGUI shownCoordinates;  // upper left text
        public LatLonInfo latLonInfo;
        public List<Vector2d> areaMarkers;


        void Awake()
        {
            areaMarkers = new List<Vector2d>(24);

            if (null == _referenceCamera)
            {
                _referenceCamera = GetComponent<Camera>();
                if (null == _referenceCamera) { Debug.LogErrorFormat("{0}: reference camera not set", this.GetType().Name); }
            }
            _mapManager.OnInitialized += () =>
            {
                _isInitialized = true;
            };
        }

        void Start()
        {
            // reset map to state that was previously active (before the scene-change)
            if (DataPersister.Instance is not null)
            {
                var data = DataPersister.Instance.GetData();
                var coordinates = new Vector2d(data.latitude, data.longitude);
                _mapManager.UpdateMap(coordinates, data.zoom);
                UpdateMarkers(coordinates);
                UpdateTextField(coordinates);
                UpdateLatLonInfo(coordinates);
            }
        }

        public void Update()
        {
            if (Input.GetMouseButtonDown(0) && EventSystem.current.IsPointerOverGameObject())
            {
                _dragStartedOnUI = true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                _dragStartedOnUI = false;
            }
        }


        private void LateUpdate()
        {
            if (!_isInitialized) { return; }

            if (!_dragStartedOnUI)
            {
                if (Input.touchSupported && Input.touchCount > 0)
                {
                    HandleTouch();
                }
                else
                {
                    HandleMouseAndKeyBoard();
                }
            }
        }

        void HandleMouseAndKeyBoard()
        {
            // zoom
            float scrollDelta = 0.0f;
            scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            ZoomMapUsingTouchOrMouse(scrollDelta);

            // don't allow wasd-movement, only mouse
            //pan keyboard
            // float xMove = Input.GetAxis("Horizontal");
            // float zMove = Input.GetAxis("Vertical");
            // PanMapUsingKeyBoard(xMove, zMove);

            //pan mouse
            PanMapUsingTouchOrMouse();
        }

        void HandleTouch()
        {
            float zoomFactor = 0.0f;
            //pinch to zoom.
            switch (Input.touchCount)
            {
                case 1:
                    {
                        PanMapUsingTouchOrMouse();
                    }
                    break;
                case 2:
                    {
                        // Store both touches.
                        Touch touchZero = Input.GetTouch(0);
                        Touch touchOne = Input.GetTouch(1);

                        // Find the position in the previous frame of each touch.
                        Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                        Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                        // Find the magnitude of the vector (the distance) between the touches in each frame.
                        float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                        float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                        // Find the difference in the distances between each frame.
                        zoomFactor = 0.01f * (touchDeltaMag - prevTouchDeltaMag);
                    }
                    ZoomMapUsingTouchOrMouse(zoomFactor);
                    break;
                default:
                    break;
            }
        }

        void ZoomMapUsingTouchOrMouse(float zoomFactor)
        {
            var zoom = Mathf.Max(0.0f, Mathf.Min(_mapManager.Zoom + zoomFactor * _zoomSpeed, 15.0f));
            if (Math.Abs(zoom - _mapManager.Zoom) > 0.0f)
            {
                _mapManager.UpdateMap(_mapManager.CenterLatitudeLongitude, zoom);
            }

        }

        // void PanMapUsingKeyBoard(float xMove, float zMove)
        // {
        //     if (Math.Abs(xMove) > 0.0f || Math.Abs(zMove) > 0.0f)
        //     {
        //         // Get the number of degrees in a tile at the current zoom level.
        //         // Divide it by the tile width in pixels ( 256 in our case)
        //         // to get degrees represented by each pixel.
        //         // Keyboard offset is in pixels, therefore multiply the factor with the offset to move the center.
        //         float factor = _panSpeed * (Conversions.GetTileScaleInDegrees((float)_mapManager.CenterLatitudeLongitude.x, _mapManager.AbsoluteZoom));

        //         var latitudeLongitude = new Vector2d(_mapManager.CenterLatitudeLongitude.x + zMove * factor * 2.0f, _mapManager.CenterLatitudeLongitude.y + xMove * factor * 4.0f);

        //         _mapManager.UpdateMap(latitudeLongitude, _mapManager.Zoom);
        //     }
        // }

        void PanMapUsingTouchOrMouse()
        {
            UseMeterConversion();
        }


        void UseMeterConversion()
        {
            if (Input.GetMouseButtonDown(1))  // click
            {
                //assign distance of camera to ground plane to z, otherwise ScreenToWorldPoint() will always return the position of the camera
                //http://answers.unity3d.com/answers/599100/view.html
                var mousePosScreen = Input.mousePosition;
                mousePosScreen.z = _referenceCamera.transform.localPosition.y;
                var pos = _referenceCamera.ScreenToWorldPoint(mousePosScreen);
                var coordinates = _mapManager.WorldToGeoPosition(pos);

                UpdateMarkers(coordinates);
                UpdateTextField(coordinates);
                UpdateLatLonInfo(coordinates);

                // set data to persists
                if (DataPersister.Instance is not null)
                {
                    DataPersister.Instance.SetData(latLonInfo.latitude, latLonInfo.longitude, Mathf.FloorToInt(_mapManager.Zoom));
                }
            }



            if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                var mousePosScreen = Input.mousePosition;
                //assign distance of camera to ground plane to z, otherwise ScreenToWorldPoint() will always return the position of the camera
                //http://answers.unity3d.com/answers/599100/view.html
                mousePosScreen.z = _referenceCamera.transform.localPosition.y;
                _mousePosition = _referenceCamera.ScreenToWorldPoint(mousePosScreen);

                if (_shouldDrag == false)
                {
                    _shouldDrag = true;
                    _origin = _referenceCamera.ScreenToWorldPoint(mousePosScreen);
                }
            }
            else
            {
                _shouldDrag = false;
            }

            if (_shouldDrag == true)
            {
                var changeFromPreviousPosition = _mousePositionPrevious - _mousePosition;
                if (Mathf.Abs(changeFromPreviousPosition.x) > 0.0f || Mathf.Abs(changeFromPreviousPosition.y) > 0.0f)
                {
                    _mousePositionPrevious = _mousePosition;
                    var offset = _origin - _mousePosition;

                    if (Mathf.Abs(offset.x) > 0.0f || Mathf.Abs(offset.z) > 0.0f)
                    {
                        if (null != _mapManager)
                        {
                            float factor = _panSpeed * Conversions.GetTileScaleInMeters((float)0, _mapManager.AbsoluteZoom) / _mapManager.UnityTileSize;
                            var latlongDelta = Conversions.MetersToLatLon(new Vector2d(offset.x * factor, offset.z * factor));
                            var newLatLong = _mapManager.CenterLatitudeLongitude + latlongDelta;

                            _mapManager.UpdateMap(newLatLong, _mapManager.Zoom);
                        }
                    }
                    _origin = _mousePosition;
                }
                else
                {
                    if (EventSystem.current.IsPointerOverGameObject())
                    {
                        return;
                    }
                    _mousePositionPrevious = _mousePosition;
                    _origin = _mousePosition;
                }
            }
        }

        private void UpdateTextField(Vector2d coordinates)
        {
            shownCoordinates.SetText("Selected Coordinates: " + coordinates);
        }

        private void UpdateLatLonInfo(Vector2d coordinates)
        {
            latLonInfo.latitude = coordinates.x;
            latLonInfo.longitude = coordinates.y;
        }

        void UpdateMarkers(Vector2d coordinates)
        {
            var latLongToXY = Mercator.LatLonToXY(coordinates.x, coordinates.y);
            var tile = Mercator.XYToTileXY(latLongToXY.Item1, latLongToXY.Item2, 15);

            if (latLonInfo is not null)
            {
                var latLonInfoToXY = Mercator.LatLonToXY(latLonInfo.latitude, latLonInfo.longitude);
                var latLonInfoTile = Mercator.XYToTileXY(latLonInfoToXY.Item1, latLonInfoToXY.Item2, 15);
                if (
                    latLonInfoTile.Item1 == tile.Item1 && latLonInfoTile.Item2 == tile.Item2 &&
                    areaMarkers.Count > 0
                   )
                {
                    // nothing changed
                    return;
                }
            }

            areaMarkers.Clear();
            // top line
            var xy_top_left = Mercator.TileXYToXY(tile.Item1 - 1, tile.Item2 - 1, 15);
            var latLon_top_left = Mercator.XYToLatLon(xy_top_left.Item1, xy_top_left.Item2);
            var xy_top = Mercator.TileXYToXY(tile.Item1, tile.Item2 - 1, 15);
            var latLon_top = Mercator.XYToLatLon(xy_top.Item1, xy_top.Item2);
            var lonDiff = (latLon_top.Item2 - latLon_top_left.Item2) * 0.5;
            for (int x = 0; x < 7; x++)  // go right
            {
                areaMarkers.Add(new Vector2d(latLon_top_left.Item1, latLon_top_left.Item2 + lonDiff * x));
            }
            // left line
            var xy_left = Mercator.TileXYToXY(tile.Item1 - 1, tile.Item2, 15);
            var latLon_left = Mercator.XYToLatLon(xy_left.Item1, xy_left.Item2);
            var latDiff = (latLon_top_left.Item1 - latLon_left.Item1) * 0.5;
            for (int y = 1; y < 7; y++)  // go down
            {
                // negative, since lat goes from +90 (north) to -90
                areaMarkers.Add(new Vector2d(latLon_top_left.Item1 - latDiff * y, latLon_top_left.Item2));
            }
            // bottom line
            var xy_bottom_right = Mercator.TileXYToXY(tile.Item1 + 2, tile.Item2 + 2, 15);
            var latLon_bottom_right = Mercator.XYToLatLon(xy_bottom_right.Item1, xy_bottom_right.Item2);
            var xy_bottom = Mercator.TileXYToXY(tile.Item1 + 1, tile.Item2 + 2, 15);
            var latLon_bottom = Mercator.XYToLatLon(xy_bottom.Item1, xy_bottom.Item2);
            lonDiff = (latLon_bottom_right.Item2 - latLon_bottom.Item2) * 0.5;
            for (int x = 0; x < 6; x++)  // go left
            {
                areaMarkers.Add(new Vector2d(latLon_bottom_right.Item1, latLon_bottom_right.Item2 - lonDiff * x));
            }
            // right line
            var xy_right = Mercator.TileXYToXY(tile.Item1 + 2, tile.Item2 + 1, 15);
            var latLon_right = Mercator.XYToLatLon(xy_right.Item1, xy_right.Item2);
            latDiff = (latLon_right.Item1 - latLon_bottom_right.Item1) * 0.5;
            for (int y = 1; y < 6; y++)  // go up
            {
                areaMarkers.Add(new Vector2d(latLon_bottom_right.Item1 + latDiff * y, latLon_bottom_right.Item2));
            }
        }
    }
}






