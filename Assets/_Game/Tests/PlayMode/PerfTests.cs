using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demons.PlayTests
{
    public class PerfTests
    {
        UnityEngine.Rendering.RenderPipelineAsset pipeline;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            QualitySettings.renderPipeline = pipeline;
            if (Object.FindAnyObjectByType<Bots>() is { } bots) Object.Destroy(bots.gameObject);
            if (Unity.Netcode.NetworkManager.Singleton) Object.Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
            LocalPlayers.Clear();
            yield return null;
        }

        static readonly (ProfilerCategory category, string name)[] Counters =
        {
            (ProfilerCategory.Internal, "Main Thread"),
            (ProfilerCategory.Memory, "GC Allocated In Frame"),
            (ProfilerCategory.Memory, "GC Allocation In Frame Count"),
            (ProfilerCategory.Render, "Draw Calls Count"),
            (ProfilerCategory.Render, "SetPass Calls Count"),
            (ProfilerCategory.Scripts, "BehaviourUpdate"),
            (ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
        };

        [UnityTest, Explicit, Timeout(300000)]
        public IEnumerator WeekOfficeFourBots()
        {
            float seconds = float.TryParse(System.Environment.GetEnvironmentVariable("PERF_SECONDS"), out var s) ? s : 40;
            Random.InitState(1);
            pipeline = QualitySettings.renderPipeline;
#if UNITY_EDITOR
            QualitySettings.renderPipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
#endif
            Progression.NextMode = GameMode.Week;
            SceneManager.LoadScene("Office");
            float waited = 0;
            while ((!GameState.Instance || !GameState.Instance.IsSpawned) && waited < 10)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(GameState.Instance && GameState.Instance.IsSpawned, "Office never came online");
            int bots = int.TryParse(System.Environment.GetEnvironmentVariable("PERF_BOTS"), out var n) ? n : 4;
            for (int i = 0; i < bots; i++) Bots.Add().Auto = true;
            var target = Application.isBatchMode ? new RenderTexture(1280, 720, 24) : null;
            if (target) Camera.main.targetTexture = target;
            float warm = Time.realtimeSinceStartup + 5;
            while (Time.realtimeSinceStartup < warm) yield return null;
#if UNITY_EDITOR
            if (System.Environment.GetEnvironmentVariable("PERF_PROFILE") == "1")
            {
                yield return Attribute();
                yield break;
            }
#endif

            var recorders = Counters.Select(c => ProfilerRecorder.StartNew(c.category, c.name)).ToArray();
            var samples = Counters.Select(_ => new List<long>()).ToArray();
            var frames = new List<float>();
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            while (Time.realtimeSinceStartup < end && GameState.Instance && !GameState.Instance.IsLobby)
            {
                frames.Add(Time.unscaledDeltaTime * 1000);
                for (int i = 0; i < recorders.Length; i++)
                    if (recorders[i].Valid) samples[i].Add(recorders[i].LastValue);
                yield return null;
            }

            var report = new System.Text.StringBuilder($"[perf] frames {frames.Count}, demons {Demon.All.Count}, pebbles {Pebble.All.Count}, graphics {SystemInfo.graphicsDeviceType}, pipeline {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name}");
            report.Append($"\n[perf] frame ms {Stats(frames.Select(f => (double)f).ToList())}");
            for (int i = 0; i < Counters.Length; i++)
            {
                bool time = recorders[i].Valid && recorders[i].UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
                var values = samples[i].Select(v => time ? v / 1e6 : v).ToList();
                report.Append($"\n[perf] {Counters[i].name}{(time ? " ms" : "")} {(recorders[i].Valid ? Stats(values) : "unavailable")}");
                recorders[i].Dispose();
            }
            report.Append($"\n[perf] GC KB/s {samples[1].Sum() / 1024.0 / frames.Sum() * 1000:0.#}");
            Debug.Log(report.ToString());
            if (target) target.Release();
        }

#if UNITY_EDITOR
        static IEnumerator Attribute()
        {
            UnityEditorInternal.ProfilerDriver.ClearAllFrames();
            UnityEditorInternal.ProfilerDriver.enabled = true;
            UnityEngine.Profiling.Profiler.enabled = true;
            for (int i = 0; i < 400; i++) yield return null;
            UnityEditorInternal.ProfilerDriver.enabled = false;
            int last = UnityEditorInternal.ProfilerDriver.lastFrameIndex, first = Mathf.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex, last - 250);
            var self = new Dictionary<string, double>();
            const int gc = UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory;
            int counted = 0;
            for (int f = first; f <= last; f++)
            {
                using var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, gc, false);
                if (!view.valid) continue;
                counted++;
                void Walk(int id, string path)
                {
                    var kids = new List<int>();
                    view.GetItemChildren(id, kids);
                    double total = view.GetItemColumnDataAsDouble(id, gc), inKids = 0;
                    foreach (var k in kids)
                    {
                        inKids += view.GetItemColumnDataAsDouble(k, gc);
                        var name = view.GetItemName(k);
                        Walk(k, path.Length == 0 || name.Contains("()") || name.Contains("Invoke") ? name : path);
                    }
                    if (total - inKids > 0) self[path] = self.GetValueOrDefault(path) + total - inKids;
                }
                Walk(view.GetRootItemID(), "");
            }
            var top = self.OrderByDescending(kv => kv.Value).Take(40).Select(kv => $"[alloc] {kv.Value / counted,8:0} B/frame  {kv.Key}");
            Debug.Log($"[alloc] over {counted} frames\n" + string.Join("\n", top));
        }
#endif

        static string Stats(List<double> v)
        {
            if (v.Count == 0) return "no samples";
            var sorted = v.OrderBy(x => x).ToList();
            double P(double q) => sorted[Mathf.Clamp((int)(q * sorted.Count), 0, sorted.Count - 1)];
            return $"mean {v.Average():0.###} p50 {P(0.5):0.###} p95 {P(0.95):0.###} p99 {P(0.99):0.###} max {sorted[^1]:0.###}";
        }
    }
}
