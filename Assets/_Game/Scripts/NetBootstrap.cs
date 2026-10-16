using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    [RequireComponent(typeof(NetworkManager))]
    public class NetBootstrap : MonoBehaviour
    {
        void Start() => GetComponent<NetworkManager>().StartHost();
    }
}
