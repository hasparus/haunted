using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    [RequireComponent(typeof(NetworkManager))]
    public class NetBootstrap : MonoBehaviour
    {
        void Start()
        {
            var net = GetComponent<NetworkManager>();
            if (NetworkManager.Singleton != net) Destroy(gameObject);
            else net.StartHost();
        }
    }
}
