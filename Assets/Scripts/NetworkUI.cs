using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    void OnGUI()
    {
        // Comprobar que el NetworkManager sigue existiendo
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null)
            return;

        if (!manager.IsClient && !manager.IsServer)
        {
            if (GUI.Button(new Rect(10, 10, 100, 30), "Host"))
            {
                manager.StartHost();
            }

            if (GUI.Button(new Rect(10, 50, 100, 30), "Client"))
            {
                manager.StartClient();
            }
        }
    }
}