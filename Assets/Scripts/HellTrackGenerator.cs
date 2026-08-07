using UnityEngine;

namespace IceEscape
{
    public class HellTrackGenerator : MonoBehaviour
    {
        public static void GenerateTrack()
        {
            GameObject trackHolder = GameObject.Find("HellTrackHolder");
            if (trackHolder != null)
            {
                DestroyImmediate(trackHolder);
            }

            trackHolder = new GameObject("HellTrackHolder");

            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.color = new Color(0.12f, 0.12f, 0.16f);

            Material lavaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lavaMat.color = new Color(1.0f, 0.25f, 0.05f);
            if (lavaMat.HasProperty("_EmissionColor"))
            {
                lavaMat.EnableKeyword("_EMISSION");
                lavaMat.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.0f));
            }

            Material boostMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            boostMat.color = new Color(0.0f, 0.9f, 1.0f);
            if (boostMat.HasProperty("_EmissionColor"))
            {
                boostMat.EnableKeyword("_EMISSION");
                boostMat.SetColor("_EmissionColor", new Color(0.0f, 0.8f, 1.0f));
            }

            Vector3 currentPos = new Vector3(0f, 10f, 0f);

            // 1. Start Ramp (High Speed Downhill slope)
            CreateSegment(trackHolder, "StartRamp", currentPos, new Vector3(8f, 1f, 30f), new Vector3(15f, 0f, 0f), floorMat);
            currentPos += new Vector3(0f, -7.5f, 28f);

            // 2. Speed Boost Zone (Rocket Launch Pad)
            GameObject boostSeg = CreateSegment(trackHolder, "BoostZone", currentPos, new Vector3(8f, 1f, 15f), new Vector3(10f, 0f, 0f), boostMat);
            SpeedBoostPad boostPad = boostSeg.AddComponent<SpeedBoostPad>();
            currentPos += new Vector3(0f, -2.5f, 14f);

            // 3. Winding Slalom Section with Obstacles & Ice Crystals
            CreateSegment(trackHolder, "SlalomRoad", currentPos, new Vector3(10f, 1f, 40f), new Vector3(12f, 0f, 0f), floorMat);
            
            // Add Obstacles on Slalom
            CreateObstacle(trackHolder, currentPos + new Vector3(-2.5f, 2f, 10f), new Vector3(1.5f, 3f, 1.5f));
            CreateObstacle(trackHolder, currentPos + new Vector3(2.5f, 2f, 22f), new Vector3(1.5f, 3f, 1.5f));
            CreateObstacle(trackHolder, currentPos + new Vector3(-2f, 2f, 32f), new Vector3(1.5f, 3f, 1.5f));

            // Add Ice Crystal Pickups on Slalom
            CreateCrystal(trackHolder, currentPos + new Vector3(2.5f, 2.5f, 10f));
            CreateCrystal(trackHolder, currentPos + new Vector3(-2.5f, 2.5f, 22f));
            CreateCrystal(trackHolder, currentPos + new Vector3(0f, 2.5f, 32f));

            currentPos += new Vector3(0f, -8f, 38f);

            // 4. Epic Jump Ramp over Lava Chasm
            CreateSegment(trackHolder, "JumpRamp", currentPos, new Vector3(8f, 1f, 16f), new Vector3(-20f, 0f, 0f), floorMat);
            currentPos += new Vector3(0f, 5f, 18f);

            // Lava Chasm Below Jump
            GameObject lavaChasm = CreateSegment(trackHolder, "LavaChasm", currentPos + new Vector3(0f, -12f, 10f), new Vector3(25f, 0.5f, 35f), Vector3.zero, lavaMat);
            lavaChasm.GetComponent<Collider>().isTrigger = true;
            lavaChasm.AddComponent<LavaHazard>();

            currentPos += new Vector3(0f, -6f, 22f); // Landing target

            // 5. High-Speed Landing Track
            CreateSegment(trackHolder, "LandingTrack", currentPos, new Vector3(12f, 1f, 50f), new Vector3(8f, 0f, 0f), floorMat);

            // Row of Ice Crystals on Landing Track
            for (int i = 0; i < 5; i++)
            {
                CreateCrystal(trackHolder, currentPos + new Vector3(0f, 2.5f, 8f + i * 8f));
            }
        }

        private static GameObject CreateSegment(GameObject parent, string name, Vector3 pos, Vector3 scale, Vector3 rot, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent.transform, false);
            obj.transform.position = pos;
            obj.transform.localScale = scale;
            obj.transform.eulerAngles = rot;
            if (mat != null) obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return obj;
        }

        private static void CreateObstacle(GameObject parent, Vector3 pos, Vector3 scale)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "HellObstacle";
            obj.transform.SetParent(parent.transform, false);
            obj.transform.position = pos;
            obj.transform.localScale = scale;

            Material obsMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            obsMat.color = new Color(0.4f, 0.1f, 0.1f);
            obj.GetComponent<MeshRenderer>().sharedMaterial = obsMat;
        }

        private static void CreateCrystal(GameObject parent, Vector3 pos)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "IceCrystal";
            obj.transform.SetParent(parent.transform, false);
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            Collider col = obj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Material mat = AssetDatabaseLoader.GetIceMaterial();
            if (mat != null) obj.GetComponent<MeshRenderer>().sharedMaterial = mat;

            obj.AddComponent<IcePickup>();
        }
    }
}
