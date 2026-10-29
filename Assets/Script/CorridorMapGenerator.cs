using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 既存の通路プレハブ（ストレート・コーナー・T字・十字・行き止まり）を使って、
/// ASCII形式の間取り図（mapLayout）を元に、自動でマップを組み立てるジェネレーター。
///
/// 使い方:
/// 1. 空のGameObjectを作り、このスクリプトをアタッチ
/// 2. Inspectorの各プレハブ欄に、対応するプレハブを割り当てる
///    （無いものは空のままでOK。その方向には何も置かれません）
/// 3. Cell Size に、1マスぶんの実際のワールドサイズ（プレハブの奥行き）を入力
/// 4. Map Layout に、下記ルールのASCIIマップを書く
/// 5. コンポーネント名を右クリック（または右上の三点リーダー）→
///    "Generate Map" を選択すると自動配置される
/// 6. やり直したい時は "Clear Map" で生成物を一括削除できる
///
/// ASCIIマップのルール（1マス=1文字、改行で次の行）:
///   '.'  通路（ここにプレハブが自動配置される）
///   '#' または半角スペース  壁（何も置かれない）
///   'S'  プレイヤーのスポーン地点（通路として扱い、空のマーカーも生成）
///   'R'  アイテム部屋などの目印地点（通路として扱い、空のマーカーも生成）
///
/// 例:
///   #######
///   #.....#
///   #.#.#.#
///   #S...R#
///   #######
///
/// 各マスの四方（上下左右）の「通路かどうか」を見て、
/// 自動的に「行き止まり/ストレート/コーナー/T字/十字」のどれを置くか、
/// どの向きに回転させるかを判定します。
///
/// 回転の初期値について:
/// 各プレハブの「正面（デフォルトの向き）」がどちらを向いているかはプレハブ次第なので、
/// 実際に生成してみて向きがズレていたら、Inspectorの
/// Straight/Corner/TJunction/Cross/DeadEnd の "Base Rotation Y" を
/// 90度刻みで調整してください（全部同じズレ方をしているはずなので、
/// 1種類だけ合わせれば他も同じ値で揃うことが多いです）。
/// </summary>
public class CorridorMapGenerator : MonoBehaviour
{
    [Header("ASCIIマップ")]
    [TextArea(5, 30)]
    [SerializeField]
    private string mapLayout =
        "#######\n" +
        "#.....#\n" +
        "#.#.#.#\n" +
        "#S...R#\n" +
        "#######";

    [Header("1マスのサイズ（プレハブの奥行きに合わせる）")]
    [SerializeField] private Vector3 cellSize = new Vector3(4f, 0f, 4f);

    [Header("通路プレハブ")]
    [SerializeField] private GameObject deadEndPrefab;     // 接続1方向
    [SerializeField] private GameObject straightPrefab;    // 接続2方向（対向：上下 or 左右）
    [SerializeField] private GameObject cornerPrefab;      // 接続2方向（隣接：L字）
    [SerializeField] private GameObject tJunctionPrefab;   // 接続3方向
    [SerializeField] private GameObject crossPrefab;       // 接続4方向
    [SerializeField] private GameObject isolatedRoomPrefab; // 接続0方向（孤立マス、任意）

    [Header("各プレハブの基準回転（ズレている場合に調整）")]
    [SerializeField] private float deadEndBaseRotationY = 0f;
    [SerializeField] private float straightBaseRotationY = 0f;
    [SerializeField] private float cornerBaseRotationY = 0f;
    [SerializeField] private float tJunctionBaseRotationY = 0f;
    [SerializeField] private float crossBaseRotationY = 0f;

    [Header("マーカー（任意）")]
    [Tooltip("'S'の位置に生成する空オブジェクトの名前")]
    [SerializeField] private string spawnMarkerName = "PlayerSpawnPoint";
    [Tooltip("'R'の位置に生成する空オブジェクトの名前（複数あれば連番になる）")]
    [SerializeField] private string roomMarkerName = "RoomMarker";

    [Header("生成先")]
    [Tooltip("生成したオブジェクトをまとめる親。未設定ならこのオブジェクト自身の下に生成")]
    [SerializeField] private Transform generatedParent;

    private const string GeneratedContainerName = "_GeneratedMap";

    // 方向定義（North, East, South, West）
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),  // North (上 / 行番号-1相当)
        new Vector2Int(1, 0),  // East
        new Vector2Int(0, -1), // South
        new Vector2Int(-1, 0), // West
    };

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ClearMap();

        char[,] grid = ParseLayout(mapLayout, out int width, out int height);
        if (grid == null)
        {
            Debug.LogError("[CorridorMapGenerator] マップのパースに失敗しました。mapLayoutを確認してください。");
            return;
        }

        Transform container = GetOrCreateContainer();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = grid[x, y];
                if (!IsPath(c)) continue;

                Vector2Int cell = new Vector2Int(x, y);
                bool[] connections = GetConnections(grid, width, height, cell);
                int connectionCount = CountTrue(connections);

                PlaceCorridorPiece(cell, connections, connectionCount, container);

                if (c == 'S')
                {
                    CreateMarker(spawnMarkerName, cell, container);
                }
                else if (c == 'R')
                {
                    CreateMarker(roomMarkerName, cell, container);
                }
            }
        }

        Debug.Log($"[CorridorMapGenerator] マップ生成完了（{width} x {height} マス）");
    }

    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        Transform container = (generatedParent != null ? generatedParent : transform)
            .Find(GeneratedContainerName);

        if (container != null)
        {
            if (Application.isPlaying)
                Destroy(container.gameObject);
            else
                DestroyImmediate(container.gameObject);
        }
    }

    // ---------------- 内部処理 ----------------

    private Transform GetOrCreateContainer()
    {
        Transform parent = generatedParent != null ? generatedParent : transform;
        var containerObj = new GameObject(GeneratedContainerName);
        containerObj.transform.SetParent(parent, false);
        return containerObj.transform;
    }

    private char[,] ParseLayout(string layout, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrEmpty(layout)) return null;

        string[] lines = layout.Replace("\r\n", "\n").Split('\n');
        // 空行除去はせず、そのまま使う（意図的な空行＝全部壁の行として扱う）
        height = lines.Length;
        foreach (var line in lines)
        {
            width = Mathf.Max(width, line.Length);
        }

        char[,] grid = new char[width, height];
        for (int y = 0; y < height; y++)
        {
            string line = lines[y];
            for (int x = 0; x < width; x++)
            {
                char c = x < line.Length ? line[x] : '#';
                // Y座標は「上の行がNorth方向」になるよう、後でY反転して使う
                grid[x, height - 1 - y] = c;
            }
        }
        return grid;
    }

    private bool IsPath(char c)
    {
        return c == '.' || c == 'S' || c == 'R';
    }

    private bool[] GetConnections(char[,] grid, int width, int height, Vector2Int cell)
    {
        bool[] result = new bool[4]; // N, E, S, W
        for (int i = 0; i < 4; i++)
        {
            Vector2Int n = cell + Directions[i];
            if (n.x < 0 || n.x >= width || n.y < 0 || n.y >= height)
            {
                result[i] = false;
                continue;
            }
            result[i] = IsPath(grid[n.x, n.y]);
        }
        return result;
    }

    private int CountTrue(bool[] arr)
    {
        int count = 0;
        foreach (var b in arr) if (b) count++;
        return count;
    }

    private void PlaceCorridorPiece(Vector2Int cell, bool[] connections, int connectionCount, Transform container)
    {
        bool n = connections[0], e = connections[1], s = connections[2], w = connections[3];
        Vector3 worldPos = new Vector3(cell.x * cellSize.x, 0f, cell.y * cellSize.z);

        GameObject prefab = null;
        float rotationY = 0f;

        switch (connectionCount)
        {
            case 0:
                prefab = isolatedRoomPrefab;
                rotationY = 0f;
                break;

            case 1:
                prefab = deadEndPrefab;
                // 開いている方向を正面として向ける
                if (n) rotationY = 0f + deadEndBaseRotationY;
                else if (e) rotationY = 90f + deadEndBaseRotationY;
                else if (s) rotationY = 180f + deadEndBaseRotationY;
                else rotationY = 270f + deadEndBaseRotationY;
                break;

            case 2:
                if (n && s)
                {
                    prefab = straightPrefab;
                    rotationY = 0f + straightBaseRotationY;
                }
                else if (e && w)
                {
                    prefab = straightPrefab;
                    rotationY = 90f + straightBaseRotationY;
                }
                else
                {
                    prefab = cornerPrefab;
                    // コーナーのデフォルトを「北・東が開いている」想定で基準化
                    if (n && e) rotationY = 0f + cornerBaseRotationY;
                    else if (e && s) rotationY = 90f + cornerBaseRotationY;
                    else if (s && w) rotationY = 180f + cornerBaseRotationY;
                    else rotationY = 270f + cornerBaseRotationY; // w && n
                }
                break;

            case 3:
                prefab = tJunctionPrefab;
                // 欠けている方向を基準に回転。デフォルトは「北が欠けている(E,S,W開放)」想定
                if (!n) rotationY = 0f + tJunctionBaseRotationY;
                else if (!e) rotationY = 90f + tJunctionBaseRotationY;
                else if (!s) rotationY = 180f + tJunctionBaseRotationY;
                else rotationY = 270f + tJunctionBaseRotationY; // !w
                break;

            case 4:
                prefab = crossPrefab;
                rotationY = 0f + crossBaseRotationY;
                break;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[CorridorMapGenerator] ({cell.x},{cell.y}) の接続数={connectionCount} に対応するプレハブが未設定のためスキップしました。");
            return;
        }

        GameObject instance =
#if UNITY_EDITOR
            Application.isPlaying
                ? Instantiate(prefab, worldPos, Quaternion.Euler(0f, rotationY, 0f), container)
                : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, container);
#else
            Instantiate(prefab, worldPos, Quaternion.Euler(0f, rotationY, 0f), container);
#endif

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            instance.transform.position = worldPos;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.SetParent(container, true);
        }
#endif

        instance.name = $"{prefab.name}_{cell.x}_{cell.y}";
    }

    private void CreateMarker(string baseName, Vector2Int cell, Transform container)
    {
        Vector3 worldPos = new Vector3(cell.x * cellSize.x, 0f, cell.y * cellSize.z);
        var marker = new GameObject(baseName);
        marker.transform.SetParent(container, false);
        marker.transform.position = worldPos;
    }
}
