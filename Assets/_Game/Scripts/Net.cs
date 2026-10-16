using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public static class Net
    {
        public const ulong None = ulong.MaxValue;

        public static T Find<T>(ulong id) where T : Component =>
            id != None
            && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)
            && obj.TryGetComponent(out T c)
                ? c
                : null;
    }
}
