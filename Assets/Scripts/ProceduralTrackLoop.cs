using System.Collections.Generic;
using UnityEngine;

namespace IceEscape
{
    /// <summary>
    /// Procedural track looping manager that recycles hill segments end-to-end.
    /// Preserves existing physics and track setup ("bozmadan") while providing an endless
    /// or extended sliding run by repositioning past segments to the front.
    /// </summary>
    public class ProceduralTrackLoop : MonoBehaviour
    {
        [Header("Track Configuration")]
        [SerializeField] private bool enableProceduralLoop = true;
        [SerializeField] private int initialSegments = 4;
        [SerializeField] private float segmentLength = 300f; // Matching Hill scale.z

        [Header("Track Elements to Duplicate")]
        [Tooltip("The parent object or primary root containing Hill and Rails.")]
        [SerializeField] private Transform trackRoot;
        [SerializeField] private GameObject hillMesh;
        [SerializeField] private GameObject railLeftMesh;
        [SerializeField] private GameObject railRightMesh;

        [Header("Obstacle Spawning on Recycled Segments")]
        [SerializeField] private bool spawnObstaclesOnRecycle = true;
        [SerializeField] private GameObject[] obstaclePrefabs;
        [SerializeField] private int minObstaclesPerSegment = 8;
        [SerializeField] private int maxObstaclesPerSegment = 14;
        [SerializeField] private float trackWidth = 50f; // Distance between left and right rails
        [SerializeField] private float obstacleSurfaceYOffset = 1.0f; // Height offset to sit on hill mesh surface

        private struct TrackSegment
        {
            public GameObject rootObject;
            public Vector3 centerPosition;
            public float centerDistAlongSlope; // Center distance along slope direction
            public List<GameObject> spawnedObstacles;
        }

        private List<TrackSegment> activeSegments = new List<TrackSegment>();
        private Transform playerTransform;
        private Vector3 slopeForward;
        private Vector3 initialTrackPos;
        private Quaternion initialTrackRot;

        [Header("Safety Margin")]
        [Tooltip("Extra distance past the end of a segment the player must travel before recycling the rear segment.")]
        [SerializeField] private float recycleSafetyMargin = 80f;

        private void Start()
        {
            if (!enableProceduralLoop)
                return;

            FindPlayer();
            SetupInitialTrack();
        }

        private void FindPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        private void SetupInitialTrack()
        {
            // Find existing Hill if trackRoot is not assigned
            if (hillMesh == null)
            {
                hillMesh = GameObject.Find("Hill");
            }
            if (railLeftMesh == null)
            {
                railLeftMesh = GameObject.Find("Rail_Left");
            }
            if (railRightMesh == null)
            {
                railRightMesh = GameObject.Find("Rail_Right");
            }

            if (hillMesh == null)
            {
                Debug.LogWarning("[ProceduralTrackLoop] Hill GameObject not found in scene!");
                return;
            }

            initialTrackPos = hillMesh.transform.position;
            initialTrackRot = hillMesh.transform.rotation;
            slopeForward = initialTrackRot * Vector3.forward;

            // Create a root parent for segment 0 if needed
            GameObject seg0 = new GameObject("TrackSegment_0");
            seg0.transform.position = initialTrackPos;
            seg0.transform.rotation = initialTrackRot;

            if (hillMesh.transform.parent == null) hillMesh.transform.SetParent(seg0.transform, true);
            if (railLeftMesh != null && railLeftMesh.transform.parent == null) railLeftMesh.transform.SetParent(seg0.transform, true);
            if (railRightMesh != null && railRightMesh.transform.parent == null) railRightMesh.transform.SetParent(seg0.transform, true);

            // Move existing scene obstacles into seg0 if they exist
            GameObject obstaclesRoot = GameObject.Find("Obstacles");
            if (obstaclesRoot != null && obstaclesRoot.transform.parent == null)
            {
                obstaclesRoot.transform.SetParent(seg0.transform, true);
            }

            TrackSegment segment0 = new TrackSegment
            {
                rootObject = seg0,
                centerPosition = initialTrackPos,
                centerDistAlongSlope = 0f,
                spawnedObstacles = new List<GameObject>()
            };
            activeSegments.Add(segment0);

            // Load obstacle prefabs from Resources/Prefabs if empty
            if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            {
                LoadDefaultObstaclePrefabs();
            }

            // Generate additional segments sequentially ahead
            for (int i = 1; i < initialSegments; i++)
            {
                CreateSegmentAhead(i);
            }
        }

        /// <summary>
        /// Editor-only convenience for scenes where obstaclePrefabs was left empty.
        ///
        /// AssetDatabase does not exist in a player build, so this whole list comes back empty
        /// there and recycled segments spawn nothing — the track just goes bare after the
        /// opening stretch. Assigning obstaclePrefabs in the inspector is what actually ships;
        /// the warning below makes the failure loud instead of silent.
        /// </summary>
        private void LoadDefaultObstaclePrefabs()
        {
            List<GameObject> prefabs = new List<GameObject>();

#if UNITY_EDITOR
            string[] prefabPaths = new string[]
            {
                "Assets/Prefabs/Obstacles/Obstacle_StaticPillar.prefab",
                "Assets/Prefabs/Obstacles/Obstacle_Tentacle.prefab",
                "Assets/Prefabs/Obstacles/Obstacle_LavaColumn.prefab",
                "Assets/Prefabs/Obstacles/Obstacle_FallingRock.prefab",
                "Assets/Prefabs/Obstacles/Obstacle_LavaRiver.prefab",
                "Assets/Prefabs/Environment/Ramp_Boost.prefab"
            };

            foreach (string path in prefabPaths)
            {
                GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) prefabs.Add(p);
            }
#endif

            obstaclePrefabs = prefabs.ToArray();

            if (obstaclePrefabs.Length == 0 && spawnObstaclesOnRecycle)
            {
                Debug.LogWarning("[ProceduralTrackLoop] No obstacle prefabs available, so recycled " +
                                 "track segments will be empty. Assign 'Obstacle Prefabs' in the " +
                                 "inspector — the editor-only fallback does not exist in a build.", this);
            }
        }

        private void CreateSegmentAhead(int index)
        {
            TrackSegment lastSeg = activeSegments[activeSegments.Count - 1];
            Vector3 newCenterPos = lastSeg.centerPosition + (slopeForward * segmentLength);
            float newCenterDist = lastSeg.centerDistAlongSlope + segmentLength;

            GameObject newSegObj = Instantiate(activeSegments[0].rootObject, newCenterPos, initialTrackRot);
            newSegObj.name = $"TrackSegment_{index}";

            // Clear pre-existing obstacles in duplicated segment root
            List<GameObject> newObstacles = new List<GameObject>();
            foreach (Transform child in newSegObj.transform)
            {
                if (child.name.Contains("Obstacles") || child.name.Contains("Obstacle") || child.name.Contains("SpeedBoostPad") || child.name.Contains("Ramp"))
                {
                    Destroy(child.gameObject);
                }
            }

            TrackSegment seg = new TrackSegment
            {
                rootObject = newSegObj,
                centerPosition = newCenterPos,
                centerDistAlongSlope = newCenterDist,
                spawnedObstacles = newObstacles
            };

            if (spawnObstaclesOnRecycle && obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            {
                PopulateObstaclesForSegment(ref seg);
            }

            activeSegments.Add(seg);
        }

        private void Update()
        {
            if (!enableProceduralLoop || activeSegments.Count < 2)
                return;

            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            // Calculate player progress along slope direction relative to initial track origin
            Vector3 playerOffset = playerTransform.position - initialTrackPos;
            float playerDistAlongSlope = Vector3.Dot(playerOffset, slopeForward);

            // First segment ends at (centerDistAlongSlope + half length).
            // Recycle ONLY when the player is safely past the end of the segment + safety margin.
            TrackSegment firstSeg = activeSegments[0];
            float firstSegEndDist = firstSeg.centerDistAlongSlope + (segmentLength * 0.5f);

            if (playerDistAlongSlope > firstSegEndDist + recycleSafetyMargin)
            {
                RecycleFirstSegment();
            }
        }

        private void RecycleFirstSegment()
        {
            TrackSegment oldestSeg = activeSegments[0];
            activeSegments.RemoveAt(0);

            TrackSegment lastSeg = activeSegments[activeSegments.Count - 1];
            Vector3 newCenterPos = lastSeg.centerPosition + (slopeForward * segmentLength);
            float newCenterDist = lastSeg.centerDistAlongSlope + segmentLength;

            oldestSeg.rootObject.transform.position = newCenterPos;
            oldestSeg.centerPosition = newCenterPos;
            oldestSeg.centerDistAlongSlope = newCenterDist;

            // Clear previous spawned obstacles
            if (oldestSeg.spawnedObstacles != null)
            {
                foreach (GameObject obs in oldestSeg.spawnedObstacles)
                {
                    if (obs != null) Destroy(obs);
                }
                oldestSeg.spawnedObstacles.Clear();
            }

            // Populate fresh procedural obstacles on the recycled segment
            if (spawnObstaclesOnRecycle && obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            {
                PopulateObstaclesForSegment(ref oldestSeg);
            }

            activeSegments.Add(oldestSeg);
        }

        private void PopulateObstaclesForSegment(ref TrackSegment seg)
        {
            if (seg.spawnedObstacles == null)
                seg.spawnedObstacles = new List<GameObject>();

            int totalObstacles = Random.Range(minObstaclesPerSegment, maxObstaclesPerSegment + 1);
            float minZ = -segmentLength * 0.42f;
            float maxZ = segmentLength * 0.42f;
            float sideLimit = (trackWidth * 0.5f) - 6f; // Max X distance to rail

            // 1. Spawn 1-2 Glowing Neon Speed Boost Pads on each segment
            int boostPadCount = Random.Range(1, 3);
            for (int b = 0; b < boostPadCount; b++)
            {
                float padX = Random.Range(-sideLimit * 0.7f, sideLimit * 0.7f);
                float padZ = Mathf.Lerp(minZ, maxZ, (b + 0.5f) / boostPadCount) + Random.Range(-10f, 10f);
                SpawnSpeedBoostPadAt(ref seg, padX, padZ);
            }

            // 2. Spawn 1 Horizontal Lava Stream River across the track width (with a jump ramp in front!)
            float lavaZ = Random.Range(minZ * 0.35f, maxZ * 0.35f);
            SpawnLavaStreamRiverAt(ref seg, lavaZ);

            // 3. Divide segment Z length into wave sections for high-speed arcade slalom feel
            if (obstaclePrefabs != null && obstaclePrefabs.Length > 0)
            {
                int waveRows = Mathf.Max(3, totalObstacles / 2);
                float zStep = (maxZ - minZ) / waveRows;

                for (int r = 0; r < waveRows; r++)
                {
                    float rowZ = minZ + (r * zStep) + Random.Range(-4f, 4f);
                    int patternType = Random.Range(0, 4);

                    switch (patternType)
                    {
                        case 0: // Center Obstacle + Side Escape
                            SpawnObstacleAt(ref seg, 0f, rowZ);
                            if (Random.value > 0.5f)
                                SpawnObstacleAt(ref seg, Random.value > 0.5f ? -sideLimit : sideLimit, rowZ + 8f);
                            break;

                        case 1: // Both Sides Blocked + Center Escape
                            SpawnObstacleAt(ref seg, -sideLimit * Random.Range(0.6f, 0.95f), rowZ);
                            SpawnObstacleAt(ref seg, sideLimit * Random.Range(0.6f, 0.95f), rowZ);
                            break;

                        case 2: // Zig-Zag Slalom (Left then Right)
                            float xPos = (r % 2 == 0) ? -sideLimit * 0.7f : sideLimit * 0.7f;
                            SpawnObstacleAt(ref seg, xPos, rowZ);
                            break;

                        case 3: // Chaotic Double Scatter
                            float x1 = Random.Range(-sideLimit, sideLimit);
                            float x2 = Random.Range(-sideLimit, sideLimit);
                            SpawnObstacleAt(ref seg, x1, rowZ);
                            if (Mathf.Abs(x1 - x2) > 8f)
                                SpawnObstacleAt(ref seg, x2, rowZ + Random.Range(-5f, 5f));
                            break;
                    }
                }
            }
        }

        private void SpawnObstacleAt(ref TrackSegment seg, float localX, float localZ)
        {
            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            if (prefab == null) return;

            Vector3 localPos = new Vector3(localX, obstacleSurfaceYOffset, localZ);
            Vector3 worldPos = seg.centerPosition + (initialTrackRot * localPos);

            // Ramps and Lava Rivers must face forward along the slope without random Y rotation
            bool isRamp = prefab.name.Contains("Ramp");
            bool isLavaRiver = prefab.name.Contains("LavaRiver");
            bool alignWithTrack = isRamp || isLavaRiver;

            Quaternion rotation = alignWithTrack ? initialTrackRot : (initialTrackRot * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            GameObject obsInstance = Instantiate(prefab, worldPos, rotation);

            if (isRamp)
            {
                obsInstance.transform.localScale = new Vector3(6f, 1.8f, 5f);
            }
            else if (isLavaRiver)
            {
                obsInstance.transform.localScale = new Vector3(trackWidth * 0.95f, 0.35f, 6.5f); // Spans horizontally across track width
            }

            obsInstance.transform.SetParent(seg.rootObject.transform, true);
            seg.spawnedObstacles.Add(obsInstance);
        }

        private void SpawnSpeedBoostPadAt(ref TrackSegment seg, float localX, float localZ)
        {
            Vector3 localPos = new Vector3(localX, 0.1f, localZ);
            Vector3 worldPos = seg.centerPosition + (initialTrackRot * localPos);

            GameObject padObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            padObj.name = "SpeedBoostPad_Procedural";
            padObj.transform.position = worldPos;
            padObj.transform.rotation = initialTrackRot;
            padObj.transform.localScale = new Vector3(12f, 0.25f, 15f); // Upgraded scale (12, 0.25, 15)

            Collider col = padObj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            padObj.AddComponent<SpeedBoostPad>();

            Renderer renderer = padObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    Color neonBlue = new Color(0.1f, 0.9f, 1.0f);
                    mat.color = neonBlue;
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", neonBlue * 2.5f);
                    }
                    renderer.sharedMaterial = mat;
                }
            }

            padObj.transform.SetParent(seg.rootObject.transform, true);
            seg.spawnedObstacles.Add(padObj);
        }

        private void SpawnLavaStreamRiverAt(ref TrackSegment seg, float localZ)
        {
            Vector3 localPos = new Vector3(0f, 0.15f, localZ);
            Vector3 worldPos = seg.centerPosition + (initialTrackRot * localPos);

            GameObject riverObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            riverObj.name = "LavaStreamRiver_Procedural";
            riverObj.transform.position = worldPos;
            riverObj.transform.rotation = initialTrackRot;
            riverObj.transform.localScale = new Vector3(trackWidth * 0.92f, 0.3f, 6.5f); // Spans full track width!

            Collider col = riverObj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            riverObj.AddComponent<LavaStreamRiver>();

            riverObj.transform.SetParent(seg.rootObject.transform, true);
            seg.spawnedObstacles.Add(riverObj);

            // Also spawn a Jump Ramp 18m in front of the lava river so the player can jump over it!
            float rampZ = localZ - 18f;
            float rampX = Random.Range(-trackWidth * 0.2f, trackWidth * 0.2f);
            SpawnObstacleAt(ref seg, rampX, rampZ);
        }
    }
}
