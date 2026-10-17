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
        const string HeadquartersPath = Root + "/Scenes/Headquarters.unity";

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
            if (!File.Exists($"{Root}/Resources/Sounds.asset"))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Sounds>(), $"{Root}/Resources/Sounds.asset");

            BuildSpriteShadow();
            var pebble = BuildPebble();
            var demon = BuildDemon();

            var prefabs = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabs.Add(new NetworkPrefab { Prefab = pebble.gameObject });
            prefabs.Add(new NetworkPrefab { Prefab = demon.gameObject });
            AssetDatabase.DeleteAsset($"{Root}/NetworkPrefabs.asset");
            AssetDatabase.CreateAsset(prefabs, $"{Root}/NetworkPrefabs.asset");

            BuildOffice(pebble, demon, prefabs);
            BuildHeadquarters(pebble, demon, prefabs);
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.WebGL.template = "PROJECT:Fill";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HeadquartersPath, true), new EditorBuildSettingsScene(OfficePath, true) };
            AssetDatabase.SaveAssets();
        }

        static Pebble BuildPebble()
        {
            var root = new GameObject("Pebble");
            root.AddComponent<NetworkObject>();
            var p = root.AddComponent<Pebble>();

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var stone = Prim(PrimitiveType.Sphere, "Stone", body, Vector3.zero, Vector3.one * 0.5f, Ink("Pebble", Gray(0.85f)));

            var dots = new Renderer[Scoring.MaxDemons];
            for (int i = 0; i < dots.Length; i++)
            {
                float a = i * Mathf.PI / 2 + Mathf.PI / 4;
                var dot = Prim(PrimitiveType.Sphere, $"Stamp{i}", body,
                    new Vector3(Mathf.Cos(a) * 0.17f, 0.17f, Mathf.Sin(a) * 0.17f), Vector3.one * 0.15f, Ink("Stamp", Color.white));
                dot.SetActive(false);
                dots[i] = dot.GetComponent<Renderer>();
            }

            var wings = new GameObject("Wings");
            wings.transform.SetParent(root.transform, false);
            Prim(PrimitiveType.Cube, "WingL", wings.transform, new Vector3(-0.35f, 0.1f, 0), new Vector3(0.45f, 0.03f, 0.25f), Ink("Wing", Color.white));
            Prim(PrimitiveType.Cube, "WingR", wings.transform, new Vector3(0.35f, 0.1f, 0), new Vector3(0.45f, 0.03f, 0.25f), Ink("Wing", Color.white));
            wings.SetActive(false);

            var shell = Prim(PrimitiveType.Sphere, "Shell", body, Vector3.zero, Vector3.one * 0.62f, Ink("Shell", Gray(0.28f)));
            shell.SetActive(false);

            Set(p, "body", body);
            Set(p, "stone", stone.GetComponent<Renderer>());
            Set(p, "wings", wings);
            Set(p, "shell", shell);
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
            var cam = NewRoom(pebble, demon, prefabs, new Vector2(8.5f, 5.5f), walls: true, beltZ: 5f, beltStart: -9.8f, beltEnd: 9.8f);
            desksBuilt = 0;
            cam.gameObject.AddComponent<CameraRig>().Follow = false;
            foreach (var pos in new[] { new Vector3(-6.5f, 0, -1.5f), new Vector3(-2.2f, 0, 0.6f), new Vector3(2.2f, 0, 0.6f), new Vector3(6.5f, 0, -1.5f) })
                BuildStation(pos, cam);
            BuildStation(new Vector3(-4.5f, 0, -4f), cam, StationKind.Hammer, Feature.Shell);
            BuildStation(new Vector3(4.5f, 0, -4f), cam, StationKind.Pot, Feature.Pot);
            BuildSpotlight(new Vector3(0, 0.02f, 0), demo: false);
            BuildClaw(new Vector3(0, 7f, 0), demo: false);
            BuildPen(new Vector3(0, 2.2f, -4.5f), demo: false);
            BuildMeds(new Vector3(0, 2.1f, 5.9f), demo: false, size: 2.2f);
            BuildEyeView(cam);
            BuildExhibits(cam);
            new GameObject("Progression").AddComponent<NetworkObject>().gameObject.AddComponent<Progression>();
            SaveScene(OfficePath);
        }

        static void BuildHeadquarters(Pebble pebble, Demon demon, NetworkPrefabsList prefabs)
        {
            var half = new Vector2(18f, 20f);
            var spawn = new Vector3(0, 0, 7f);
            var cam = NewRoom(pebble, demon, prefabs, half, walls: false, beltZ: -1.5f, beltStart: -19.3f, beltEnd: 3f);
            desksBuilt = 0;
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Focus = new Vector3(0, 0, 11.4f);
            rig.IdleExtents = new Vector3(14f, 0, 3.8f);
            cam.transform.position = spawn + new Vector3(0, 15, -12.5f);

            var state = Object.FindAnyObjectByType<GameState>();
            state.SpawnOrigin = spawn;
            var so = new SerializedObject(state);
            so.FindProperty("startSceneWhenAllFall").stringValue = "Office";
            so.ApplyModifiedPropertiesWithoutUndo();

            FloorLabel("First week at work", null, new Vector3(-9f, 0, 16.5f), 0.3f, Jolly);
            FloorLabel("Infinite survival", null, new Vector3(9f, 0, 16.5f), 0.3f, Jolly);
            FloorLabel("best here: {best}\n\nTop scores\n{top}", null, new Vector3(9f, 0, 12.6f), 0.13f);
            Prim(PrimitiveType.Cube, "ModeDivider", null, new Vector3(0, 0.01f, 16f), new Vector3(0.15f, 0.02f, 8f), Paper("Divider", new Color(0.78f, 0.74f, 0.66f)));
            FloorLabel("Jump off to start", null, new Vector3(0, 0, 10.6f), 0.14f);
            FloorArrow(new Vector3(-4.2f, 0, 10.6f), 1);
            FloorArrow(new Vector3(4.2f, 0, 10.6f), 1);
            FloorLabel("Wardrobe\nstand on a color", null, new Vector3(-11.1f, 0, 9f), 0.12f);
            for (int i = 0; i < Demon.Palette.Length; i++)
            {
                var pad = Prim(PrimitiveType.Cylinder, $"ColorPad{i}", null, new Vector3(-15f + i * 1.3f, 0.01f, 7f), new Vector3(0.9f, 0.01f, 0.9f), Unlit($"Pad{i}", Demon.Palette[i]));
                pad.AddComponent<ColorPad>().Index = i;
            }
            FloorLabel("Learn the mechanics", null, new Vector3(0, 0, 4f), 0.14f);
            FloorArrow(new Vector3(-4.6f, 0, 4f), -1);
            FloorArrow(new Vector3(4.6f, 0, 4f), -1);

            var go = new GameObject("Headquarters");
            go.AddComponent<NetworkObject>();
            var headquarters = go.AddComponent<Headquarters>();
            Set(headquarters, "pebblePrefab", pebble);
            var bays = go.transform;

            const float a = -6f, b = -11.6f, c = -16.8f;

            FloorLabel("{grab}\nto grab & drop", bays, new Vector3(-13f, 0, a - 1.9f));
            Seed(bays, new Vector3(-13.6f, 0, a));
            Seed(bays, new Vector3(-12.4f, 0, a));
            Seed(bays, new Vector3(-13f, 0, a - 1f));

            var solo = BuildStation(new Vector3(-8f, 0, a), cam);
            FloorLabel(Lessons[Feature.None], bays, new Vector3(-8f, 0, a - 2f));
            Seed(bays, solo.Slot.position, desk: solo);

            var together = BuildStation(new Vector3(-3f, 0, a), cam);
            FloorLabel("Haunt together\nfaster + combo", bays, new Vector3(-3f, 0, a - 1.6f));
            Seed(bays, together.Slot.position, stamps: 0b0001, desk: together);

            var big = BuildStation(new Vector3(2f, 0, a), cam);
            FloorLabel(Lessons[Feature.Big], bays, new Vector3(2f, 0, a - 1.8f));
            Seed(bays, big.Slot.position, desk: big, kind: PebbleKind.Big);

            FloorLabel("Left alone\nwings, -revenue", bays, new Vector3(7f, 0, a - 1.6f));
            Seed(bays, new Vector3(7f, 0, a - 0.2f), pinned: false);

            FloorLabel(Lessons[Feature.Pill], bays, new Vector3(12f, 0, a - 1.8f));
            BuildMeds(new Vector3(12f, 0.9f, a), demo: true);

            BuildStation(new Vector3(-12f, 0, b), cam, StationKind.Pot, Feature.Pot);
            FloorLabel(Lessons[Feature.Pot], bays, new Vector3(-12f, 0, b - 1.8f));

            BuildStation(new Vector3(-7f, 0, b), cam, StationKind.Hammer, Feature.Shell);
            FloorLabel(Lessons[Feature.Shell], bays, new Vector3(-7f, 0, b - 2f));
            Seed(bays, new Vector3(-5.4f, 0, b + 0.2f), shelled: true);

            FloorLabel("Throw →\n{haunt}\nwhile holding", bays, new Vector3(1.6f, 0, b - 2.4f));
            Seed(bays, new Vector3(1.2f, 0, b - 0.4f));
            BuildStation(new Vector3(7.5f, 0, b - 0.4f), cam);
            FloorLabel("Throw target", bays, new Vector3(7.5f, 0, b - 1.9f));
            FloorLabel("Catch\nempty hands, in the arc", bays, new Vector3(4.4f, 0, b + 1.6f));

            var phone = BuildStation(new Vector3(13f, 0, b), cam);
            FloorLabel(Lessons[Feature.Phone], bays, new Vector3(13f, 0, b - 2f));
            Set(headquarters, "phoneDesk", phone);

            FloorLabel("Haunted by many", bays, new Vector3(-12f, 0, c + 1.2f));
            byte[] mixes = { 1, 2, 4, 8, 3, 12, 15 };
            for (int i = 0; i < mixes.Length; i++)
                Seed(bays, new Vector3(-15f + i * 1.0f, 0, c), stamps: mixes[i]);

            var clawDesk = BuildStation(new Vector3(-5.5f, 0, c), cam);
            Seed(bays, clawDesk.Slot.position, desk: clawDesk, stamps: 0b0010);
            BuildClaw(new Vector3(-5.5f, 7f, c), demo: true);
            FloorLabel(Lessons[Feature.Claw], bays, new Vector3(-5.5f, 0, c - 1.9f));

            BuildPen(new Vector3(2f, 2.2f, c), demo: true);
            FloorLabel(Lessons[Feature.Pen], bays, new Vector3(2f, 0, c - 1.6f));
            FloorLabel("{dash}\nto dash", bays, new Vector3(6.5f, 0, c + 0.4f));

            BuildSpotlight(new Vector3(12.5f, 0.02f, c), demo: true);
            FloorLabel(Lessons[Feature.Therapist], bays, new Vector3(12.5f, 0, c - 2.2f));

            SaveScene(HeadquartersPath);
        }

        // What each mechanic's workstation says, in the headquarters and on its exhibit in the Office.
        static readonly Dictionary<Feature, string> Lessons = new()
        {
            [Feature.None] = "Hold\n{haunt}\nto haunt",
            [Feature.Big] = "Big thought\nhaunt it together\n5x points",
            [Feature.Pill] = "Meds\nwhen he takes one,\ncontrols flip",
            [Feature.Pot] = "Pot\nhaunts by itself",
            [Feature.Shell] = "Hammer: hold\n{haunt}\nto crack shells",
            [Feature.Phone] = "Phone: hold\n{haunt}\nto answer",
            [Feature.Claw] = "The Hand\nsave thoughts before it lands",
            [Feature.Pen] = "The Pen: mash\n{haunt}\nor wiggle ←→",
            [Feature.Therapist] = "Therapist's light\nlit demons drop things",
        };

        // The headquarters' workstations again, in the mist past the Office's left wall, each out of reach and shown on
        // the day the First Week at Work brings in its mechanic. Two on a day stand one above the other on screen.
        static void BuildExhibits(Camera cam)
        {
            Vector3 Floor(float y)
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.1f, y, 0));
                return ray.GetPoint(-ray.origin.y / ray.direction.y);
            }
            for (int day = 0; day < 7; day++)
            {
                var mechanics = new List<Feature>();
                for (var f = Feature.None; f <= Feature.Claw; f++)
                    if (Progression.Introduces(day, f)) mechanics.Add(f);
                for (int i = 0; i < mechanics.Count; i++)
                    BuildExhibit(mechanics[i], Floor(mechanics.Count == 1 ? 0.5f : i == 0 ? 0.66f : 0.3f), cam);
            }
        }

        static void BuildExhibit(Feature mechanic, Vector3 at, Camera cam)
        {
            var root = new GameObject($"Exhibit{mechanic}").transform;
            root.position = at;
            var exhibit = root.gameObject.AddComponent<Exhibit>();
            exhibit.Mechanic = mechanic;
            var prop = at + Vector3.forward * 0.9f;
            var label = at + Vector3.back * 1.1f;

            Transform Inert(StationKind kind = StationKind.Desk)
            {
                var st = BuildStation(prop, cam, kind);
                var t = st.transform;
                Object.DestroyImmediate(st);
                Object.DestroyImmediate(t.GetComponent<NetworkObject>());
                foreach (var c in t.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                t.SetParent(root, true);
                return t;
            }
            void Thought(Transform desk, float size = 0.5f, bool shelled = false)
            {
                var slot = desk.Find("Slot").position;
                Prim(PrimitiveType.Sphere, "Thought", root, slot, Vector3.one * size, Ink("Pebble", Gray(0.85f)));
                if (shelled) Prim(PrimitiveType.Sphere, "Shell", root, slot, Vector3.one * 0.62f, Ink("Shell", Gray(0.5f)));
            }
            void Demo(GameObject go) => go.transform.SetParent(root, true);

            switch (mechanic)
            {
                case Feature.None: Thought(Inert()); break;
                case Feature.Big: Thought(Inert(), 0.8f); break;
                case Feature.Pot: Inert(StationKind.Pot); break;
                case Feature.Shell: Thought(Inert(StationKind.Hammer), shelled: true); break;
                case Feature.Phone: Inert().Find("Visual/Phone").gameObject.SetActive(true); break;
                case Feature.Claw:
                    Thought(Inert());
                    Demo(BuildClaw(prop + Vector3.up * 7, demo: true));
                    break;
                case Feature.Pen: Demo(BuildPen(prop + Vector3.up * 2.2f, demo: true)); break;
                case Feature.Pill: Demo(BuildMeds(prop + Vector3.up * 0.9f, demo: true)); break;
                case Feature.Therapist: Demo(BuildSpotlight(prop + Vector3.up * 0.02f, demo: true)); break;
            }
            FloorLabel(Lessons[mechanic], root, label);

            var center = at + Vector3.up * 0.6f;
            var mist = Prim(PrimitiveType.Quad, "Mist", root, Vector3.zero, Vector3.one * 7.5f, Mat("Mist", new Color(0.02f, 0.02f, 0.03f), "Haunted/Mist"));
            mist.transform.SetPositionAndRotation(center + (cam.transform.position - center).normalized * 4, cam.transform.rotation);
            mist.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Set(exhibit, "mist", mist.GetComponent<Renderer>());
        }

        static GameObject BuildClaw(Vector3 pos, bool demo)
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
            return go;
        }

        static GameObject BuildPen(Vector3 pos, bool demo)
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
            // A drawn pencil hanging from its eraser, tip down (the sprite stands on its tip).
            var pencil = Point("Pencil", look.transform, new Vector3(0, -1.1f, 0)).gameObject.AddComponent<SpriteRenderer>();
            pencil.sprite = Art("Pencil");
            pencil.transform.localScale = Vector3.one * 0.65f;
            pencil.sharedMaterial = Resources.Load<Material>("Fx/Card");
            look.SetActive(false);
            Set(pen, "look", look);
            return go;
        }

        // Tilt the camera up and back so the board sits low, under what the host sees across the top of the screen.
        static void BuildEyeView(Camera cam)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0, 19.37f, -16.89f), Quaternion.Euler(44.3f, 0, 0));
            var material = Fade(Unlit("EyeView", Color.white));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Resources/Eyes/day0.png"));
            var view = Prim(PrimitiveType.Quad, "EyeView", null, Vector3.zero, Vector3.one, material);
            view.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.AddComponent<EyeView>();
        }

        static GameObject BuildMeds(Vector3 pos, bool demo, float size = 1)
        {
            var go = new GameObject(demo ? "MedsDemo" : "Meds");
            go.transform.position = pos;
            var meds = go.AddComponent<Meds>();
            meds.Demo = demo;
            var look = new GameObject("Look");
            look.transform.SetParent(go.transform, false);
            look.transform.localScale = Vector3.one * size;
            var pill = Prim(PrimitiveType.Capsule, "Pill", look.transform, Vector3.zero, new Vector3(0.6f, 0.55f, 0.6f), Fade(Mat("Pill", new Color(0.55f, 0.75f, 1f))));
            pill.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Prim(PrimitiveType.Sphere, "Cap", look.transform, new Vector3(0.3f, 0, 0), Vector3.one * 0.62f, Fade(Mat("PillCap", Gray(0.95f))));
            look.SetActive(false);
            Set(meds, "look", look.transform);
            return go;
        }

        static GameObject BuildSpotlight(Vector3 pos, bool demo)
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
            var warm = new Color(1f, 0.88f, 0.6f);

            var look = new GameObject("Look");
            look.transform.SetParent(go.transform, false);
            // Wider than the gameplay ring: the light spills past the edge that counts.
            float d = Balance.I.spotRadius * 2 * 1.6f;
            var disc = Prim(PrimitiveType.Quad, "Glow", look.transform, new Vector3(0, 0.02f, 0), new Vector3(d, d, 1),
                Additive("SpotGlow", new Color(1f, 0.85f, 0.55f, 0.6f), Gradient("SpotGlow", radial: true)));
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var beam = new GameObject("Beam");
            beam.transform.SetParent(go.transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = Cone();
            var beamRenderer = beam.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = Additive("SpotBeam", new Color(1f, 0.88f, 0.62f, 0.07f), Gradient("SpotBeam", radial: false));
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(go.transform, false);
            lamp.type = LightType.Spot;
            lamp.intensity = 90;
            lamp.color = warm;
            lamp.shadows = LightShadows.Soft;

            var glow = new GameObject("FloorGlow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0, 0.9f, 0);
            glow.type = LightType.Point;
            glow.intensity = 4;
            glow.color = warm;

            Set(spot, "look", look);
            Set(spot, "beam", beam.transform);
            Set(spot, "lamp", lamp);
            Set(spot, "glow", glow);
            Set(spot, "ringMaterial", Unlit("SpotRing", warm));
            return go;
        }

        // Soft alpha falloff: radial for the pool of light, lengthwise for the beam (faint at the lamp, fading out at the floor).
        static Texture2D Gradient(string name, bool radial)
        {
            var path = $"{Root}/Materials/{name}.png";
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a;
                if (radial)
                {
                    float r = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                    a = Mathf.Pow(1 - Mathf.SmoothStep(0f, 1f, r), 1.5f);
                }
                else
                {
                    float v = y / (size - 1f);
                    a = Mathf.Pow(v, 1.6f) * (1 - Mathf.SmoothStep(0.8f, 1f, v));
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Alpha-blended Lit that still writes depth, so overlapping parts sort while fading.
        static Material Fade(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWriteControl", 1);
            m.SetFloat("_ZWrite", 1);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Additive(string name, Color color, Texture2D texture)
        {
            var m = Unlit(name, color);
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 2);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Open cone, apex at the origin, unit radius at z = 1; v runs from the apex (0) to the rim (1).
        static Mesh Cone()
        {
            var path = $"{Root}/Materials/Beam.asset";
            AssetDatabase.DeleteAsset(path);
            const int sides = 32;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                vertices.Add(Vector3.zero);
                uvs.Add(new Vector2(i / (float)sides, 0));
                vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1));
                uvs.Add(new Vector2(i / (float)sides, 1));
                if (i == sides) break;
                int k = i * 2;
                triangles.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
            }
            var mesh = new Mesh { name = "Beam" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
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

        static Camera NewRoom(Pebble pebble, Demon demon, NetworkPrefabsList prefabs, Vector2 half, bool walls,
            float beltZ, float beltStart, float beltEnd)
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
            RenderSettings.sun = sun;

            Prim(PrimitiveType.Cube, "Floor", null, new Vector3(0, -0.1f, 0), new Vector3(half.x * 2, 0.2f, half.y * 2), Paper("Floor", new Color(0.3f, 0.35f, 0.37f), tiles: true), collider: true);
            float wx = half.x + 0.25f, wz = half.y + 0.25f;
            if (walls)
            {
                Prim(PrimitiveType.Cube, "WallBack", null, new Vector3(0, 0.75f, wz), new Vector3(half.x * 2, 1.5f, 0.5f), Paper("Wall", new Color(0.66f, 0.61f, 0.53f)), collider: true);
                Prim(PrimitiveType.Cube, "WallFront", null, new Vector3(0, 0.2f, -wz), new Vector3(half.x * 2, 0.4f, 0.5f), Paper("Wall", new Color(0.66f, 0.61f, 0.53f)), collider: true);
                float gapLow = beltZ - 0.8f, gapHigh = beltZ + 0.8f, top = half.y + 0.5f, bottom = -half.y - 0.5f;
                foreach (var x in new[] { -wx, wx })
                {
                    Prim(PrimitiveType.Cube, "WallSide", null, new Vector3(x, 0.75f, (bottom + gapLow) / 2), new Vector3(0.5f, 1.5f, gapLow - bottom), Paper("Wall", new Color(0.66f, 0.61f, 0.53f)), collider: true);
                    Prim(PrimitiveType.Cube, "WallSide", null, new Vector3(x, 0.75f, (gapHigh + top) / 2), new Vector3(0.5f, 1.5f, top - gapHigh), Paper("Wall", new Color(0.66f, 0.61f, 0.53f)), collider: true);
                }
            }

            var beltGo = new GameObject("Belt");
            beltGo.transform.position = new Vector3(0, 0, beltZ);
            beltGo.AddComponent<NetworkObject>();
            var belt = beltGo.AddComponent<Belt>();
            Prim(PrimitiveType.Cube, "Conveyor", beltGo.transform, new Vector3((beltStart + beltEnd) / 2, 0.25f, 0), new Vector3(beltEnd - beltStart + 0.4f, 0.5f, 1), Paper("Belt", new Color(0.14f, 0.13f, 0.16f), slats: true), collider: true);
            Set(belt, "start", Point("Start", beltGo.transform, new Vector3(beltStart, 0.75f, 0)));
            Set(belt, "end", Point("End", beltGo.transform, new Vector3(beltEnd, 0.75f, 0)));
            Set(belt, "pebblePrefab", pebble);

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

        // Every other desk gets the second drawing; counted per room.
        static int desksBuilt;

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
                    Prim(PrimitiveType.Cylinder, "Pot", visual, new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.45f, 1.1f), Paper("Pot", new Color(0.22f, 0.2f, 0.24f)), collider: true);
                    st.Slot = Point("Slot", go.transform, new Vector3(0, 1.05f, 0));
                    break;
                case StationKind.Hammer:
                    Prim(PrimitiveType.Cube, "Anvil", visual, new Vector3(0, 0.4f, 0), new Vector3(1.1f, 0.8f, 1.1f), Paper("Anvil", new Color(0.45f, 0.43f, 0.46f)), collider: true);
                    Prim(PrimitiveType.Cube, "Head", visual, new Vector3(0.35f, 1.7f, 0), new Vector3(0.5f, 0.25f, 0.25f), Mat("Nose", Gray(0.1f)));
                    Prim(PrimitiveType.Cube, "Handle", visual, new Vector3(0.35f, 1.3f, 0), new Vector3(0.08f, 0.6f, 0.08f), Mat("Nose", Gray(0.1f)));
                    st.Slot = Point("Slot", go.transform, new Vector3(0, 1.05f, 0));
                    break;
                default:
                    // The drawing's footprint: blocks demons and casts the desk's shadow. Shallow, so a demon can step a
                    // little behind the desk and be partly hidden by it.
                    var top = Prim(PrimitiveType.Cube, "Top", visual, new Vector3(0, 0.385f, -0.3f), new Vector3(1.7f, 0.77f, 0.4f), Mat("Desk", Gray(0.72f)), collider: true);
                    top.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    // Billboard drawing standing on the footprint's front edge.
                    var art = Point("Art", visual, new Vector3(0, 0, -0.5f)).gameObject.AddComponent<SpriteRenderer>();
                    art.sprite = Art(desksBuilt++ % 2 == 0 ? "Station_Blank" : "Station_Desk2");
                    art.transform.rotation = cam.transform.rotation;
                    art.transform.localScale = Vector3.one * 1.8f / art.sprite.bounds.size.x;
                    art.color = new Color(0.86f, 0.81f, 0.72f);
                    art.sharedMaterial = Resources.Load<Material>("Fx/Card");
                    // The thought sits on the middle of the drawn desk top (measured off the art: 71% up the plain desk, 73%
                    // up the legged one), raised a tenth of its height so it rests on the top. It stays in the drawing's
                    // plane so a moving camera can't slide it off; the card draws at its feet's depth, behind the thought.
                    var bounds = art.sprite.bounds;
                    var on = art.transform.TransformPoint(new Vector3(0, bounds.min.y + bounds.size.y * (art.sprite.name == "Station_Desk2" ? 0.727f : 0.709f), 0));
                    st.Slot = Point("Slot", go.transform, go.transform.InverseTransformPoint(on + art.transform.up * 0.05f));
                    var phone = Prim(PrimitiveType.Cube, "Phone", visual, new Vector3(0.45f, 0.95f, -0.15f), new Vector3(0.35f, 0.18f, 0.22f), Mat("Phone", new Color(0.98f, 0.8f, 0.08f)));
                    phone.SetActive(false);
                    Set(st, "phone", phone);
                    break;
            }
            if (kind != StationKind.Desk) Label($"<b>{kind}</b>", visual, new Vector3(0, 0.5f, -0.7f), cam);
            Set(st, "visual", visual.gameObject);
            return st;
        }

        // Alpha-clipped Lit used by invisible cards so drawings cast their own silhouettes.
        static void BuildSpriteShadow()
        {
            var path = $"{Root}/Resources/SpriteShadow.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", Art("Devil_Front").texture);
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", 0);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
        }

        static Sprite Art(string name)
        {
            var path = $"{Root}/Resources/Art/{name}.png";
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        static void FloorArrow(Vector3 pos, float dir)
        {
            var mat = Unlit("Arrow", Color.white);
            var arrow = new GameObject("Arrow").transform;
            arrow.position = pos + Vector3.up * 0.01f;
            arrow.rotation = Quaternion.Euler(0, dir > 0 ? 0 : 180, 0);
            Prim(PrimitiveType.Cube, "Shaft", arrow, Vector3.zero, new Vector3(0.12f, 0.02f, 1.2f), mat);
            foreach (var side in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "Head", arrow, new Vector3(side * 0.2f, 0, 0.4f), new Vector3(0.12f, 0.02f, 0.6f), mat)
                    .transform.localRotation = Quaternion.Euler(0, -side * 45, 0);
        }

        static void FloorLabel(string text, Transform parent, Vector3 pos, float size = 0.115f, Font font = null)
        {
            var t = Point("FloorLabel", parent, new Vector3(pos.x, 0.02f, pos.z));
            t.rotation = Quaternion.Euler(90, 0, 0);
            // Info labels lead with a bold heading line.
            int br = text.IndexOf('\n');
            if (!font && br > 0 && !text.StartsWith("best")) text = $"<b>{text[..br]}</b>{text[br..]}";
            var tm = TextLabel(t, text, font);
            tm.characterSize = size;
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

        static Font Plex => AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Resources/UI/Fonts/IBMPlexSerif-Regular.ttf");
        static Font Jolly => AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Resources/UI/Fonts/JollyLodger-Regular.ttf");

        static TextMesh TextLabel(Transform t, string text, Font font = null)
        {
            var tm = t.gameObject.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.12f;
            tm.fontSize = 48;
            tm.color = Gray(0.05f);
            tm.font = font ? font : Plex;
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

        // Paper with a silhouette outline instead of face edges, for the round thoughts and their shells.
        static Material Ink(string name, Color color)
        {
            var m = Paper(name, color);
            m.SetFloat("_Outline", 0);
            m.SetFloat("_Silhouette", 1);
            m.SetFloat("_DepthPull", 0.12f);
            return m;
        }

        // The inked-paper look for the office's own geometry; see Haunted/Paper.
        static Material Paper(string name, Color color, bool tiles = false, bool slats = false)
        {
            var m = Mat(name, color, "Haunted/Paper");
            m.shader = Shader.Find("Haunted/Paper");
            m.SetFloat("_Tiles", tiles ? 1 : 0);
            m.SetFloat("_Slats", slats ? 1 : 0);
            return m;
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
