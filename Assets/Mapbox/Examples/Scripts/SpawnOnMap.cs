namespace Mapbox.Examples
{
    using UnityEngine;
    using Mapbox.Unity.Map;
    using System.Collections.Generic;

    public class SpawnOnMap : MonoBehaviour
    {
        public AbstractMap _map;
        public QuadTreeCameraMovement quadTreeCameraMovement;
        public GameObject tilePointsPrefab;

        private List<GameObject> _spawnedObjects;  // object pool

        void Start()
        {
            _spawnedObjects = new List<GameObject>();
        }

        private void Update()
        {
            var markers = quadTreeCameraMovement.areaMarkers;
            // adjust object pool...
            if (markers.Count > _spawnedObjects.Count)
            {
                // ...add new objects
                for (int i = _spawnedObjects.Count; i < markers.Count; i++)
                {
                    _spawnedObjects.Add(Instantiate(tilePointsPrefab));
                }
            }
            else if (markers.Count < _spawnedObjects.Count)
            {
                // ...or deactivate objects
                for (int i = markers.Count; i < _spawnedObjects.Count; i++)
                {
                    _spawnedObjects[i].SetActive(false);
                }
            }

            for (int i = 0; i < markers.Count; i++)
            {
                _spawnedObjects[i].transform.localPosition = _map.GeoToWorldPosition(markers[i], true);
                _spawnedObjects[i].SetActive(true);
            }
        }
    }
}