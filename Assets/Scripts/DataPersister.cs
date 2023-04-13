using UnityEngine;

public class DataPersister : MonoBehaviour
{
    public static DataPersister Instance;

    public double latitude;
    public double longitude;
    public int zoom;
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetData(double latitude, double longitude, int zoom)
    {
        DataPersister.Instance.latitude = latitude;
        DataPersister.Instance.longitude = longitude;
        DataPersister.Instance.zoom = zoom;
    }


    public (double latitude, double longitude, int zoom) GetData()
    {
        return (DataPersister.Instance.latitude, DataPersister.Instance.longitude, DataPersister.Instance.zoom);
    }
}