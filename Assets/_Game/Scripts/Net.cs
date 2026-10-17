using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public static class Net
    {
        public const ulong None = ulong.MaxValue;

        // Game time: network time minus every pause.
        public static double Now => GameState.Instance && GameState.Instance.IsSpawned
            ? GameState.Instance.Now
            : NetworkManager.Singleton.ServerTime.Time;

        public static T Find<T>(ulong id) where T : Component =>
            id != None
            && NetworkManager.Singleton
            && NetworkManager.Singleton.SpawnManager != null
            && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)
            && obj.TryGetComponent(out T c)
                ? c
                : null;
    }
}
