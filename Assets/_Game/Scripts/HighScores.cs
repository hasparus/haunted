using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Demons
{
    public class HighScores : MonoBehaviour
    {
        const string Url = "https://my-demons-scores.hasparus.workers.dev/scores";

        public static string Top { get; private set; } = "";

        [System.Serializable]
        class Entry
        {
            public float score;
            public int players;
        }

        [System.Serializable]
        class Board
        {
            public Entry[] scores;
        }

        static HighScores instance;

        static HighScores Instance
        {
            get
            {
                if (instance) return instance;
                instance = new GameObject("HighScores").AddComponent<HighScores>();
                DontDestroyOnLoad(instance.gameObject);
                return instance;
            }
        }

        public static void Fetch() => Instance.StartCoroutine(Send(UnityWebRequest.Get(Url)));

        public static void Submit(float score, int players)
        {
            var body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Entry { score = score, players = players }));
            var req = new UnityWebRequest(Url, "POST") { uploadHandler = new UploadHandlerRaw(body), downloadHandler = new DownloadHandlerBuffer() };
            req.SetRequestHeader("content-type", "application/json");
            Instance.StartCoroutine(Send(req));
        }

        static IEnumerator Send(UnityWebRequest req)
        {
            using (req)
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                var board = JsonUtility.FromJson<Board>(req.downloadHandler.text);
                if (board?.scores == null) yield break;
                var sb = new StringBuilder();
                for (int i = 0; i < Mathf.Min(5, board.scores.Length); i++)
                    sb.AppendLine($"{i + 1}.  {board.scores[i].score:0}   ({board.scores[i].players} demon{(board.scores[i].players == 1 ? "" : "s")})");
                Top = sb.ToString().TrimEnd();
            }
        }
    }
}
