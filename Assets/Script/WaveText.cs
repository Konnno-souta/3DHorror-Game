using UnityEngine;
using TMPro;

public class WaveText : MonoBehaviour
{
    TMP_Text textMesh;

    public float amplitude = 10f;   // 揺れの大きさ
    public float frequency = 2f;    // 波の速さ
    public float speed = 2f;        // 全体の動き

    void Start()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    void Update()
    {
        textMesh.ForceMeshUpdate();
        var textInfo = textMesh.textInfo;

        float time = Time.time * speed;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int index = textInfo.characterInfo[i].vertexIndex;
            int matIndex = textInfo.characterInfo[i].materialReferenceIndex;

            Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;

            // 不気味な波（サイン波＋ランダム）
            float wave = Mathf.Sin(time + i * 0.5f) * amplitude;
            float noise = Mathf.PerlinNoise(i, time) * amplitude * 0.5f;

            Vector3 offset = new Vector3(0, wave + noise, 0);

            vertices[index + 0] += offset;
            vertices[index + 1] += offset;
            vertices[index + 2] += offset;
            vertices[index + 3] += offset;
        }

        // メッシュ更新
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textMesh.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}