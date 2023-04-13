using System.Collections.Generic;
using UnityEngine;

public class ChangeText : MonoBehaviour
{
    public List<string> texts;
    public float textChangeTime = 5.0f;  // seconds

    private float startTime;
    private int index = 0;
    private TMPro.TextMeshProUGUI textMesh;

    void Start()
    {
        if (texts.Count <= 0)
        {
            return;
        }

        textMesh = GetComponent<TMPro.TextMeshProUGUI>();
        index = (index + 1) % texts.Count;
        startTime = Time.time;
    }

    void Update()
    {
        if (texts.Count <= 0)
        {
            return;
        }

        if (Time.time - startTime >= textChangeTime)
        {
            textMesh.text = texts[index];
            index = (index + 1) % texts.Count;
            startTime = Time.time;
        }
    }
}
