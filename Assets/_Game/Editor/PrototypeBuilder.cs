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
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(WorkshopPath, true), new EditorBuildSettingsScene(OfficePath, true) };
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

            var shell = Prim(PrimitiveType.Sphere, "Shell", body, Vector3.zero, Vector3.one * 0.62f, Mat("Shell", Gray(0.28f)));
            shell.SetActive(false);
            var pill = Prim(PrimitiveType.Capsule, "Pill", body, Vector3.zero, new Vector3(0.28f, 0.22f, 0.28f), Mat("Pill", new Color(0.55f, 0.75f, 1f)));
            pill.transform.localRotation = Quaternion.Euler(0, 0, 90);
            pill.SetActive(false);

            Set(p, "body", body);
            Set(p, "stone", stone.GetComponent<Renderer>());
            Set(p, "wings", wings);
            Set(p, "shell", shell);
            Set(p, "pill", pill);
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

            var scribble = new GameObject("Scribble");
            scribble.transform.SetParent(root.transform, false);
            for (int i = 0; i < 5; i++)
            {
                var line = Prim(PrimitiveType.Cube, $"Line{i}", scribble.transform, new Vector3(0, 0.5f + i * 0.25f, 0), new Vector3(1.15f, 0.05f, 0.05f), Mat("Ink", Gray(0.05f)));
                line.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 18f : -22f, i * 37f, i % 3 == 0 ? -15f : 20f);
            }
            scribble.SetActive(false);

            d.Hand = hand;
            Set(d, "scribble", scribble);
            Set(d, "body", body.GetComponent<Renderer>());
            Set(d, "head", head.GetComponent<Renderer>());
            Set(d, "marker", marker.transform);
            return SavePrefab(root).GetComponent<Demon>();
        }

        static void BuildOffice(Pebble pebble, Demon demon, NetworkPrefabsList prefabs)
        {
            var cam = NewRoom(pebble, demon, prefabs, new Vector2(8.5f, 5.5f), walls: true);
            foreach (var pos in new[] { new Vector3(-6.5f, 0, -1.5f), new Vector3(-2.2f, 0, 0.6f), new Vector3(2.2f, 0, 0.6f), new Vector3(6.5f, 0, -1.5f) })
                BuildStation(pos, cam);
            BuildStation(new Vector3(-4.5f, 0, -4f), cam, StationKind.Hammer, Feature.Shell);
            BuildStation(new Vector3(4.5f, 0, -4f), cam, StationKind.Pot, Feature.Pot);
            BuildSpotlight(new Vector3(0, 0.02f, 0), demo: false);
            BuildClaw(new Vector3(0, 7f, 0), demo: false);
            BuildPen(new Vector3(0, 2.2f, -4.5f), demo: false);
            new GameObject("Progression").AddComponent<NetworkObject>().gameObject.AddComponent<Progression>();
            SaveScene(OfficePath);
        }

        static void BuildWorkshop(Pebble pebble, Demon demon, NetworkPrefabsList prefabs)
        {
            var cam = NewRoom(pebble, demon, prefabs, new Vector2(16f, 10.5f), walls: false);
            var state = Object.FindFirstObjectByType<GameState>();
            var so = new SerializedObject(state);
            so.FindProperty("startSceneWhenAllFall").stringValue = "Office";
            so.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(GameObject.Find("EdgeFront"));
            FloorLabel("↓ FIRST WEEK AT WORK ↓", null, new Vector3(-8f, 0, -10.1f));
            FloorLabel("↓ INFINITE SURVIVAL  ·  best {best} ↓", null, new Vector3(8f, 0, -10.1f));
            Prim(PrimitiveType.Cube, "ModeDivider", null, new Vector3(0, 0.01f, -9.9f), new Vector3(0.12f, 0.02f, 1.2f), Mat("Divider", Gray(0.75f)));

            var go = new GameObject("Workshop");
            go.AddComponent<NetworkObject>();
            var workshop = go.AddComponent<Workshop>();
            Set(workshop, "pebblePrefab", pebble);
            var bays = go.transform;

            FloorLabel("{grab}\nTO GRAB & DROP", bays, new Vector3(-13f, 0, 2.5f));
            Seed(bays, new Vector3(-13.6f, 0, 4.4f));
            Seed(bays, new Vector3(-12.4f, 0, 4.4f));
            Seed(bays, new Vector3(-13f, 0, 3.4f));

            var solo = BuildStation(new Vector3(-8f, 0, 4.4f), cam);
            FloorLabel("HOLD\n{haunt}\nTO HAUNT", bays, new Vector3(-8f, 0, 2.4f));
            Seed(bays, solo.Slot.position, desk: solo);

            var together = BuildStation(new Vector3(-3f, 0, 4.4f), cam);
            FloorLabel("HAUNT TOGETHER\nfaster + combo", bays, new Vector3(-3f, 0, 2.8f));
            Seed(bays, together.Slot.position, stamps: 0b0001, desk: together);

            var big = BuildStation(new Vector3(2f, 0, 4.4f), cam);
            FloorLabel("BIG THOUGHT\nhaunt it together\n3x points", bays, new Vector3(2f, 0, 2.6f));
            Seed(bays, big.Slot.position, desk: big, kind: PebbleKind.Big);

            FloorLabel("LEFT ALONE\nwings, -influence", bays, new Vector3(7f, 0, 2.8f));
            Seed(bays, new Vector3(7f, 0, 4.2f), pinned: false);

            FloorLabel("PILL\nif it reaches him,\ncontrols flip", bays, new Vector3(12f, 0, 2.6f));
            Seed(bays, new Vector3(12f, 0, 4.2f), kind: PebbleKind.Pill);

            BuildStation(new Vector3(-12f, 0, -0.8f), cam, StationKind.Pot, Feature.Pot);
            FloorLabel("POT\nhaunts by itself", bays, new Vector3(-12f, 0, -2.6f));

            BuildStation(new Vector3(-7f, 0, -0.8f), cam, StationKind.Hammer, Feature.Shell);
            FloorLabel("HAMMER: HOLD\n{haunt}\nTO CRACK SHELLS", bays, new Vector3(-7f, 0, -2.8f));
            Seed(bays, new Vector3(-5.4f, 0, -0.6f), shelled: true);

            FloorLabel("THROW →\n{haunt}\nWHILE HOLDING", bays, new Vector3(1.6f, 0, -3.6f));
            Seed(bays, new Vector3(1.2f, 0, -1.6f));
            BuildStation(new Vector3(7.5f, 0, -1.6f), cam);
            FloorLabel("THROW TARGET", bays, new Vector3(7.5f, 0, -3.1f));
            FloorLabel("CATCH\nempty hands, in the arc", bays, new Vector3(4.4f, 0, 0.4f));

            var phone = BuildStation(new Vector3(13f, 0, -1.2f), cam);
            FloorLabel("PHONE: HOLD\n{haunt}\nTO ANSWER", bays, new Vector3(13f, 0, -3.2f));
            Set(workshop, "phoneDesk", phone);

            FloorLabel("HAUNTED BY MANY", bays, new Vector3(-12f, 0, -5.5f));
            byte[] mixes = { 1, 2, 4, 8, 3, 12, 15 };
            for (int i = 0; i < mixes.Length; i++)
                Seed(bays, new Vector3(-15f + i * 1.0f, 0, -6.7f), stamps: mixes[i]);

            var clawDesk = BuildStation(new Vector3(-5.5f, 0, -6.2f), cam);
            Seed(bays, clawDesk.Slot.position, desk: clawDesk, stamps: 0b0010);
            BuildClaw(new Vector3(-5.5f, 7f, -6.2f), demo: true);
            FloorLabel("THE HAND\nsave thoughts before it lands", bays, new Vector3(-5.5f, 0, -8.1f));

            BuildPen(new Vector3(2f, 2.2f, -6.8f), demo: true);
            FloorLabel("THE PEN: MASH\n{haunt}\nTO SHAKE IT OFF", bays, new Vector3(2f, 0, -8.3f));
            FloorLabel("{dash}\nTO DASH", bays, new Vector3(6.5f, 0, -6.4f));

            BuildSpotlight(new Vector3(12.5f, 0.02f, -6.4f), demo: true);
            FloorLabel("THERAPIST'S LIGHT\nlit demons drop things", bays, new Vector3(12.5f, 0, -8.6f));

            SaveScene(WorkshopPath);
        }

        static void BuildClaw(Vector3 pos, bool demo)
        {
            var go = new GameObject(demo ? "HandDemo" : "Hand");
            go.transform.position = pos;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var claw = go.AddComponent<Claw>();
            claw.Demo = demo;

            var look = new GameObject("Look");
            look.transform.SetParent(go.transform, false);
            var skin = Mat("Skin", Gray(0.82f));
            Prim(PrimitiveType.Cube, "Palm", look.transform, new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.25f, 0.8f), skin);
            Prim(PrimitiveType.Cube, "Arm", look.transform, new Vector3(0, 4f, 0), new Vector3(0.5f, 7f, 0.5f), skin);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, $"Finger{i}", look.transform, new Vector3(-0.32f + i * 0.21f, 0f, 0.3f), new Vector3(0.14f, 0.55f, 0.14f), skin);
            Prim(PrimitiveType.Cube, "Thumb", look.transform, new Vector3(0.45f, 0.05f, -0.15f), new Vector3(0.14f, 0.45f, 0.14f), skin);
            claw.Hand = Point("Grip", go.transform, new Vector3(0, -0.15f, 0));
            look.SetActive(false);

            var shadow = Prim(PrimitiveType.Cylinder, "Shadow", go.transform, Vector3.zero, new Vector3(1.6f, 0.005f, 1.6f), Unlit("HandShadow", Gray(0.12f)));
            shadow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadow.SetActive(false);
            Set(claw, "look", look);
            Set(claw, "shadow", shadow.transform);
        }

        static void BuildPen(Vector3 pos, bool demo)
        {
            var go = new GameObject(demo ? "PenDemo" : "Pen");
            go.transform.position = pos;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var pen = go.AddComponent<Pen>();
            pen.Demo = demo;

            var look = new GameObject("Look");
            look.transform.SetParent(go.transform, false);
            look.transform.localRotation = Quaternion.Euler(0, 0, 30);
            Prim(PrimitiveType.Cylinder, "Body", look.transform, new Vector3(0, 0.3f, 0), new Vector3(0.12f, 0.45f, 0.12f), Mat("Ink", Gray(0.05f)));
            Prim(PrimitiveType.Cylinder, "Cap", look.transform, new Vector3(0, 0.8f, 0), new Vector3(0.13f, 0.08f, 0.13f), Mat("Phone", new Color(0.98f, 0.8f, 0.08f)));
            look.SetActive(false);
            Set(pen, "look", look);
        }

        static void BuildSpotlight(Vector3 pos, bool demo)
        {
            var go = new GameObject(demo ? "TherapistDemo" : "Therapist");
            go.transform.position = pos;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncPositionY = false;
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var spot = go.AddComponent<Spotlight>();
            spot.Demo = demo;

            var look = new GameObject("Look");
            look.transform.SetParent(go.transform, false);
            float d = Balance.I.spotRadius * 2;
            var disc = Prim(PrimitiveType.Cylinder, "Disc", look.transform, Vector3.zero, new Vector3(d, 0.005f, d), Unlit("Spot", new Color(1f, 0.9f, 0.6f)));
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(look.transform, false);
            light.transform.localPosition = new Vector3(0, 6, 0);
            light.transform.localRotation = Quaternion.Euler(90, 0, 0);
            light.type = LightType.Spot;
            light.spotAngle = 32;
            light.range = 10;
            light.intensity = 30;
            light.color = new Color(1f, 0.88f, 0.6f);
            Set(spot, "look", look);
        }

        static void Seed(Transform parent, Vector3 pos, byte stamps = 0, Station desk = null, bool pinned = true,
            PebbleKind kind = PebbleKind.Normal, bool shelled = false)
        {
            var seed = Point("Seed", parent, pos).gameObject.AddComponent<PebbleSeed>();
            seed.Stamps = stamps;
            seed.Desk = desk;
            seed.Pinned = pinned;
            seed.Kind = kind;
            seed.Shelled = shelled;
        }

        static void SaveScene(string path)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, path);
            scene = EditorSceneManager.OpenScene(path);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Camera NewRoom(Pebble pebble, Demon demon, NetworkPrefabsList prefabs, Vector2 half, bool walls)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float zoom = half.x / 8.5f;


            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 15, -12.5f) * zoom;
            cam.transform.LookAt(new Vector3(0, 0, 0.3f));
            cam.fieldOfView = 34;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Gray(0.06f);
            cam.gameObject.AddComponent<AudioListener>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -25, 0);

            Prim(PrimitiveType.Cube, "Floor", null, new Vector3(0, -0.1f, 0), new Vector3(half.x * 2, 0.2f, half.y * 2), Mat("Floor", Gray(0.32f)), collider: true);
            float beltZ = half.y - 1.3f, wx = half.x + 0.25f, wz = half.y + 0.25f;
            if (walls)
            {
                Prim(PrimitiveType.Cube, "WallBack", null, new Vector3(0, 0.75f, wz), new Vector3(half.x * 2, 1.5f, 0.5f), Mat("Wall", Gray(0.5f)), collider: true);
                Prim(PrimitiveType.Cube, "WallFront", null, new Vector3(0, 0.2f, -wz), new Vector3(half.x * 2, 0.4f, 0.5f), Mat("Wall", Gray(0.5f)), collider: true);
                float gapLow = beltZ - 0.8f, gapHigh = beltZ + 0.8f, top = half.y + 0.5f, bottom = -half.y - 0.5f;
                foreach (var x in new[] { -wx, wx })
                {
                    Prim(PrimitiveType.Cube, "WallSide", null, new Vector3(x, 0.75f, (bottom + gapLow) / 2), new Vector3(0.5f, 1.5f, gapLow - bottom), Mat("Wall", Gray(0.5f)), collider: true);
                    Prim(PrimitiveType.Cube, "WallSide", null, new Vector3(x, 0.75f, (gapHigh + top) / 2), new Vector3(0.5f, 1.5f, top - gapHigh), Mat("Wall", Gray(0.5f)), collider: true);
                }
            }
            else
            {
                foreach (var (name, pos, size) in new[]
                {
                    ("EdgeBack", new Vector3(0, 1, wz), new Vector3(half.x * 2 + 1, 2, 0.5f)),
                    ("EdgeFront", new Vector3(0, 1, -wz), new Vector3(half.x * 2 + 1, 2, 0.5f)),
                    ("EdgeLeft", new Vector3(-wx, 1, 0), new Vector3(0.5f, 2, half.y * 2 + 1)),
                    ("EdgeRight", new Vector3(wx, 1, 0), new Vector3(0.5f, 2, half.y * 2 + 1)),
                })
                {
                    var edge = new GameObject(name).AddComponent<BoxCollider>();
                    edge.transform.position = pos;
                    edge.size = size;
                }
            }

            var beltGo = new GameObject("Belt");
            beltGo.transform.position = new Vector3(0, 0, beltZ);
            beltGo.AddComponent<NetworkObject>();
            var belt = beltGo.AddComponent<Belt>();
            Prim(PrimitiveType.Cube, "Conveyor", beltGo.transform, new Vector3(0, 0.25f, 0), new Vector3(half.x * 2 + 3, 0.5f, 1), Mat("Belt", Gray(0.15f)), collider: true);
            Set(belt, "start", Point("Start", beltGo.transform, new Vector3(-half.x - 1.3f, 0.75f, 0)));
            Set(belt, "end", Point("End", beltGo.transform, new Vector3(half.x + 1.3f, 0.75f, 0)));
            Set(belt, "pebblePrefab", pebble);
            Label("THOUGHTS →", beltGo.transform, new Vector3(-half.x + 2, 1.2f, 0), cam);

            var netGo = new GameObject("Network");
            var nm = netGo.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = netGo.AddComponent<OfflineTransport>() };
            netGo.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>().UseWebSockets = true;
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
            netGo.AddComponent<NetBootstrap>();
            netGo.AddComponent<LocalPlayers>();

            var stateGo = new GameObject("GameState");
            stateGo.AddComponent<NetworkObject>();
            var state = stateGo.AddComponent<GameState>();
            state.RoomHalfExtents = half;
            Set(state, "demonPrefab", demon);
            return cam;
        }

        static Station BuildStation(Vector3 pos, Camera cam, StationKind kind = StationKind.Desk, Feature requires = Feature.None)
        {
            var go = new GameObject(kind.ToString());
            go.transform.position = pos;
            go.AddComponent<NetworkObject>();
            var st = go.AddComponent<Station>();
            st.Kind = kind;
            st.Requires = requires;
            var visual = Point("Visual", go.transform, Vector3.zero);
            switch (kind)
            {
                case StationKind.Pot:
                    Prim(PrimitiveType.Cylinder, "Pot", visual, new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.45f, 1.1f), Mat("Pot", Gray(0.22f)), collider: true);
                    st.Slot = Point("Slot", go.transform, new Vector3(0, 1.05f, 0));
                    break;
                case StationKind.Hammer:
                    Prim(PrimitiveType.Cube, "Anvil", visual, new Vector3(0, 0.4f, 0), new Vector3(1.1f, 0.8f, 1.1f), Mat("Anvil", Gray(0.45f)), collider: true);
                    Prim(PrimitiveType.Cube, "Head", visual, new Vector3(0.35f, 1.7f, 0), new Vector3(0.5f, 0.25f, 0.25f), Mat("Nose", Gray(0.1f)));
                    Prim(PrimitiveType.Cube, "Handle", visual, new Vector3(0.35f, 1.3f, 0), new Vector3(0.08f, 0.6f, 0.08f), Mat("Nose", Gray(0.1f)));
                    st.Slot = Point("Slot", go.transform, new Vector3(0, 1.05f, 0));
                    break;
                default:
                    Prim(PrimitiveType.Cube, "Top", visual, new Vector3(0, 0.5f, 0), new Vector3(1.3f, 1f, 1.3f), Mat("Desk", Gray(0.72f)), collider: true);
                    st.Slot = Point("Slot", go.transform, new Vector3(0, 1.25f, 0));
                    var phone = Prim(PrimitiveType.Cube, "Phone", visual, new Vector3(0.4f, 1.1f, 0.35f), new Vector3(0.35f, 0.18f, 0.22f), Mat("Phone", new Color(0.98f, 0.8f, 0.08f)));
                    phone.SetActive(false);
                    Set(st, "phone", phone);
                    break;
            }
            Label(kind.ToString().ToUpper(), visual, new Vector3(0, 0.5f, -0.7f), cam);
            Set(st, "visual", visual.gameObject);
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
            tm.characterSize = 0.115f;
            tm.alignment = TextAlignment.Center;
            tm.color = Gray(0.85f);
            if (text.Contains("{")) t.gameObject.AddComponent<ControlsLabel>().Template = text;
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

        static Material Mat(string name, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            if (mats.TryGetValue(name, out var m)) return m;
            var path = $"{Root}/Materials/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return mats[name] = m;
        }

        static Material Unlit(string name, Color color) => Mat(name, color, "Universal Render Pipeline/Unlit");

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
