using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Demons.Editor
{
    public static class PrototypeBuilder
    {
        const string Root = "Assets/_Game";
        const string OfficePath = Root + "/Scenes/Office.unity";
        const string WorkshopPath = Root + "/Scenes/Workshop.unity";

        static readonly Dictionary<string, Material> mats = new();

        [MenuItem("Demons/Build Prototype")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play mode before building the prototype scene.");
                return;
            }
            foreach (var dir in new[] { "Resources", "Materials", "Prefabs", "Scenes" })
                Directory.CreateDirectory($"{Root}/{dir}");
            mats.Clear();

            if (!File.Exists($"{Root}/Resources/Balance.asset"))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Balance>(), $"{Root}/Resources/Balance.asset");

            var pebble = BuildPebble();
            var demon = BuildDemon();

            var prefabs = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabs.Add(new NetworkPrefab { Prefab = pebble.gameObject });
            prefabs.Add(new NetworkPrefab { Prefab = demon.gameObject });
            AssetDatabase.DeleteAsset($"{Root}/NetworkPrefabs.asset");
            AssetDatabase.CreateAsset(prefabs, $"{Root}/NetworkPrefabs.asset");

            BuildOffice(pebble, demon, prefabs);
            BuildWorkshop(pebble, demon, prefabs);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(OfficePath, true), new EditorBuildSettingsScene(WorkshopPath, true) };
            AssetDatabase.SaveAssets();
        }

        static Pebble BuildPebble()
        {
            var root = new GameObject("Pebble");
            root.AddComponent<NetworkObject>();
            var p = root.AddComponent<Pebble>();

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var stone = Prim(PrimitiveType.Sphere, "Stone", body, Vector3.zero, Vector3.one * 0.5f, Mat("Pebble", Gray(0.85f)));

            var dots = new Renderer[Scoring.MaxDemons];
            for (int i = 0; i < dots.Length; i++)
            {
                float a = i * Mathf.PI / 2 + Mathf.PI / 4;
                var dot = Prim(PrimitiveType.Sphere, $"Stamp{i}", body,
                    new Vector3(Mathf.Cos(a) * 0.17f, 0.17f, Mathf.Sin(a) * 0.17f), Vector3.one * 0.15f, Mat("Stamp", Color.white));
                dot.SetActive(false);
                dots[i] = dot.GetComponent<Renderer>();
            }

            var wings = new GameObject("Wings");
            wings.transform.SetParent(root.transform, false);
            Prim(PrimitiveType.Cube, "WingL", wings.transform, new Vector3(-0.35f, 0.1f, 0), new Vector3(0.45f, 0.03f, 0.25f), Mat("Wing", Color.white));
            Prim(PrimitiveType.Cube, "WingR", wings.transform, new Vector3(0.35f, 0.1f, 0), new Vector3(0.45f, 0.03f, 0.25f), Mat("Wing", Color.white));
            wings.SetActive(false);

            Set(p, "body", body);
            Set(p, "stone", stone.GetComponent<Renderer>());
            Set(p, "wings", wings);
            SetArray(p, "stampDots", dots);
            return SavePrefab(root).GetComponent<Pebble>();
        }

        static Demon BuildDemon()
        {
            var root = new GameObject("Demon");
            root.AddComponent<NetworkObject>();
            var rb = root.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.8f, 0);
            col.height = 1.6f;
            col.radius = 0.4f;

            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncPositionY = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var d = root.AddComponent<Demon>();
            var body = Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0, 0.8f, 0), new Vector3(0.8f, 0.8f, 0.8f), Mat("Demon", Color.white));
            var head = Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0, 1.75f, 0), Vector3.one * 0.6f, Mat("Demon", Color.white));
            Prim(PrimitiveType.Cube, "Nose", root.transform, new Vector3(0, 1.75f, 0.3f), Vector3.one * 0.15f, Mat("Nose", Gray(0.1f)));
            var hand = new GameObject("Hand").transform;
            hand.SetParent(root.transform, false);
            hand.localPosition = new Vector3(0, 1.0f, 0.7f);
            var marker = Prim(PrimitiveType.Cylinder, "Marker", root.transform, Vector3.zero, new Vector3(0.9f, 0.01f, 0.9f), Mat("Marker", Color.white));
            marker.SetActive(false);

            d.Hand = hand;
            Set(d, "body", body.GetComponent<Renderer>());
            Set(d, "head", head.GetComponent<Renderer>());
            Set(d, "marker", marker.transform);
            return SavePrefab(root).GetComponent<Demon>();
        }

        static void BuildOffice(Pebble pebble, Demon demon, NetworkPrefabsList prefabs)
        {
            var cam = NewRoom(pebble, demon, prefabs);
            foreach (var pos in new[] { new Vector3(-6.5f, 0, -1.5f), new Vector3(-2.2f, 0, 0.6f), new Vector3(2.2f, 0, 0.6f), new Vector3(6.5f, 0, -1.5f) })
                BuildStation(pos, cam);
            SaveScene(OfficePath);
        }

        static void BuildWorkshop(Pebble pebble, Demon demon, NetworkPrefabsList prefabs)
        {
            var cam = NewRoom(pebble, demon, prefabs);
            var go = new GameObject("Workshop");
            go.AddComponent<NetworkObject>();
            Set(go.AddComponent<Workshop>(), "pebblePrefab", pebble);
            var bays = go.transform;

            FloorLabel("GRAB & DROP", bays, new Vector3(-6.6f, 0, -1.1f));
            Seed(bays, new Vector3(-7.1f, 0, 0.6f));
            Seed(bays, new Vector3(-6.1f, 0, 0.6f));
            Seed(bays, new Vector3(-6.6f, 0, -0.3f));

            var solo = BuildStation(new Vector3(-2.8f, 0, 0.8f), cam);
            FloorLabel("HOLD STAMP", bays, new Vector3(-2.8f, 0, -0.6f));
            Seed(bays, solo.Slot.position, desk: solo);

            var together = BuildStation(new Vector3(1.6f, 0, 0.8f), cam);
            FloorLabel("STAMP TOGETHER\nfaster + combo", bays, new Vector3(1.6f, 0, -0.7f));
            Seed(bays, together.Slot.position, stamps: 0b0001, desk: together);

            FloorLabel("LEFT ALONE\nwings, -influence", bays, new Vector3(6f, 0, -0.7f));
            Seed(bays, new Vector3(6f, 0, 0.6f), pinned: false);

            FloorLabel("STAMP MIXES", bays, new Vector3(0, 0, -3.7f));
            FloorLabel("THROW: stamp key while holding\ncatch with empty hands", bays, new Vector3(-5.6f, 0, -2.5f));
            for (byte mask = 1; mask < 16; mask++)
                Seed(bays, new Vector3(-8 + mask, 0, -4.4f), stamps: mask);

            SaveScene(WorkshopPath);
        }

        static void Seed(Transform parent, Vector3 pos, byte stamps = 0, Station desk = null, bool pinned = true)
        {
            var seed = Point("Seed", parent, pos).gameObject.AddComponent<PebbleSeed>();
            seed.Stamps = stamps;
            seed.Desk = desk;
            seed.Pinned = pinned;
        }

        static void SaveScene(string path)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, path);
            scene = EditorSceneManager.OpenScene(path);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Camera NewRoom(Pebble pebble, Demon demon, NetworkPrefabsList prefabs)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 15, -12.5f);
            cam.transform.LookAt(new Vector3(0, 0, 0.3f));
            cam.fieldOfView = 34;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Gray(0.06f);
            cam.gameObject.AddComponent<AudioListener>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -25, 0);

            Prim(PrimitiveType.Cube, "Floor", null, new Vector3(0, -0.1f, 0), new Vector3(17, 0.2f, 11), Mat("Floor", Gray(0.32f)), collider: true);
            Prim(PrimitiveType.Cube, "WallBack", null, new Vector3(0, 0.75f, 5.75f), new Vector3(17, 1.5f, 0.5f), Mat("Wall", Gray(0.5f)), collider: true);
            Prim(PrimitiveType.Cube, "WallFront", null, new Vector3(0, 0.2f, -5.75f), new Vector3(17, 0.4f, 0.5f), Mat("Wall", Gray(0.5f)), collider: true);
            foreach (var x in new[] { -8.75f, 8.75f })
            {
                Prim(PrimitiveType.Cube, "WallSide", null, new Vector3(x, 0.75f, -1.3f), new Vector3(0.5f, 1.5f, 9.4f), Mat("Wall", Gray(0.5f)), collider: true);
                Prim(PrimitiveType.Cube, "WallSide", null, new Vector3(x, 0.75f, 5.5f), new Vector3(0.5f, 1.5f, 1f), Mat("Wall", Gray(0.5f)), collider: true);
            }

            var beltGo = new GameObject("Belt");
            beltGo.transform.position = new Vector3(0, 0, 4.2f);
            beltGo.AddComponent<NetworkObject>();
            var belt = beltGo.AddComponent<Belt>();
            Prim(PrimitiveType.Cube, "Conveyor", beltGo.transform, new Vector3(0, 0.25f, 0), new Vector3(20, 0.5f, 1), Mat("Belt", Gray(0.15f)), collider: true);
            Set(belt, "start", Point("Start", beltGo.transform, new Vector3(-9.8f, 0.75f, 0)));
            Set(belt, "end", Point("End", beltGo.transform, new Vector3(9.8f, 0.75f, 0)));
            Set(belt, "pebblePrefab", pebble);
            Label("THOUGHTS →", beltGo.transform, new Vector3(-6.5f, 1.2f, 0), cam);

            var netGo = new GameObject("Network");
            var nm = netGo.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = netGo.AddComponent<OfflineTransport>() };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
            netGo.AddComponent<NetBootstrap>();
            netGo.AddComponent<LocalPlayers>();

            var stateGo = new GameObject("GameState");
            stateGo.AddComponent<NetworkObject>();
            Set(stateGo.AddComponent<GameState>(), "demonPrefab", demon);
            return cam;
        }

        static Station BuildStation(Vector3 pos, Camera cam)
        {
            var go = new GameObject("Desk");
            go.transform.position = pos;
            go.AddComponent<NetworkObject>();
            var st = go.AddComponent<Station>();
            Prim(PrimitiveType.Cube, "Top", go.transform, new Vector3(0, 0.5f, 0), new Vector3(1.3f, 1f, 1.3f), Mat("Desk", Gray(0.72f)), collider: true);
            st.Slot = Point("Slot", go.transform, new Vector3(0, 1.25f, 0));
            Label("DESK", go.transform, new Vector3(0, 0.5f, -0.7f), cam);
            return st;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Transform Point(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static void FloorLabel(string text, Transform parent, Vector3 pos)
        {
            var t = Point("FloorLabel", parent, new Vector3(pos.x, 0.02f, pos.z));
            t.rotation = Quaternion.Euler(90, 0, 0);
            var tm = TextLabel(t, text);
            tm.characterSize = 0.07f;
            tm.color = Gray(0.8f);
        }

        static void Label(string text, Transform parent, Vector3 pos, Camera cam)
        {
            var t = Point("Label", parent, pos);
            t.rotation = cam.transform.rotation;
            TextLabel(t, text);
        }

        static TextMesh TextLabel(Transform t, string text)
        {
            var tm = t.gameObject.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.12f;
            tm.fontSize = 48;
            tm.color = Gray(0.05f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            return tm;
        }

        static Material Mat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var m)) return m;
            var path = $"{Root}/Materials/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return mats[name] = m;
        }

        static Color Gray(float v) => new(v, v, v);

        static GameObject SavePrefab(GameObject root)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
