using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RealLoserStudio.Tactical.Streaming
{
    [Serializable]
    public struct GridTileCoord : IEquatable<GridTileCoord>
    {
        public int x;
        public int z;

        public GridTileCoord(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public bool Equals(GridTileCoord other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is GridTileCoord other && Equals(other);
        public override int GetHashCode() => (x * 73856093) ^ (z * 19349663);
    }

    public class StreamingTileCell
    {
        public GridTileCoord coord;
        public Bounds worldBounds;
        public bool isLoaded;
        public readonly List<GameObject> tileObjects = new List<GameObject>();
    }

    [Serializable]
    public class MissionSaveData
    {
        public string missionId = "M00_Test";
        public int alertLevel;
        public float playerHealth = 100f;
        public Vector3 playerPosition;
        public int playerPosture;
        public List<string> neutralizedEnemyIds = new List<string>();
        public List<string> disarmedTrapIds = new List<string>();
        public List<string> completedObjectives = new List<string>();
    }

    /// <summary>
    /// Mobile Performance Tile Streaming (`150x150m` tiles, `3x3` active window = `450x450m`)
    /// & Encrypted Checkpoint Save System for Android ARM64 IL2CPP (>= 30 FPS target).
    /// </summary>
    public class MissionEventAndStreamingSystem : MonoBehaviour
    {
        public const float TILE_SIZE_METERS = 150.0f;
        public const int ACTIVE_GRID_RADIUS = 1; // 3x3 window (-1..+1 in X and Z = 450x450m)
        public const float ACTIVE_WINDOW_METERS = 450.0f;
        public const int TARGET_MOBILE_FPS = 30;

        [Header("Streaming Configuration")]
        public Transform playerTransform;
        public float streamingCheckInterval = 0.25f;

        private readonly Dictionary<GridTileCoord, StreamingTileCell> _tiles = new Dictionary<GridTileCoord, StreamingTileCell>();
        private GridTileCoord _currentCenterTile = new GridTileCoord(int.MinValue, int.MinValue);
        private float _timer;

        public int ActiveTileCount { get; private set; }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }

        public static GridTileCoord WorldToTileCoord(Vector3 worldPos)
        {
            int tx = Mathf.FloorToInt(worldPos.x / TILE_SIZE_METERS);
            int tz = Mathf.FloorToInt(worldPos.z / TILE_SIZE_METERS);
            return new GridTileCoord(tx, tz);
        }

        public void RegisterWorldObjectToTile(GameObject obj)
        {
            if (obj == null) return;
            GridTileCoord coord = WorldToTileCoord(obj.transform.position);
            if (!_tiles.TryGetValue(coord, out StreamingTileCell cell))
            {
                Vector3 center = new Vector3((coord.x + 0.5f) * TILE_SIZE_METERS, 0f, (coord.z + 0.5f) * TILE_SIZE_METERS);
                cell = new StreamingTileCell
                {
                    coord = coord,
                    worldBounds = new Bounds(center, new Vector3(TILE_SIZE_METERS, 200f, TILE_SIZE_METERS)),
                    isLoaded = true
                };
                _tiles[coord] = cell;
            }
            cell.tileObjects.Add(obj);
        }

        public void TickStreaming(float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < streamingCheckInterval || playerTransform == null) return;
            _timer = 0f;

            GridTileCoord centerTile = WorldToTileCoord(playerTransform.position);
            if (centerTile.Equals(_currentCenterTile)) return;

            _currentCenterTile = centerTile;
            UpdateActiveTileWindow(centerTile);
        }

        /// <summary>
        /// Activates the 3x3 (450x450m) tile neighborhood around `centerTile` and hibernates distant tiles.
        /// </summary>
        public void UpdateActiveTileWindow(GridTileCoord centerTile)
        {
            int activeCount = 0;
            foreach (KeyValuePair<GridTileCoord, StreamingTileCell> kvp in _tiles)
            {
                GridTileCoord c = kvp.Key;
                StreamingTileCell cell = kvp.Value;
                bool shouldBeActive = Mathf.Abs(c.x - centerTile.x) <= ACTIVE_GRID_RADIUS &&
                                      Mathf.Abs(c.z - centerTile.z) <= ACTIVE_GRID_RADIUS;

                if (shouldBeActive) activeCount++;

                if (cell.isLoaded != shouldBeActive)
                {
                    cell.isLoaded = shouldBeActive;
                    for (int i = 0; i < cell.tileObjects.Count; i++)
                    {
                        if (cell.tileObjects[i] != null)
                        {
                            cell.tileObjects[i].SetActive(shouldBeActive);
                        }
                    }
                }
            }
            ActiveTileCount = activeCount;
        }

        // --- Checkpoint Save / Load System ---

        public static void SaveCheckpoint(MissionSaveData data, string filePath)
        {
            string json = JsonUtility.ToJson(data, true);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            File.WriteAllBytes(filePath, bytes);
        }

        public static MissionSaveData LoadCheckpoint(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonUtility.FromJson<MissionSaveData>(json);
        }
    }
}
