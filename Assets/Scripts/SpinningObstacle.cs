
using Unity.Netcode;
using UnityEngine;

public class SpinningObstacle : NetworkBehaviour
{
    [SerializeField] private float rotationSpeed;

    private void Update()
    {
        // Solo el servidor controla el giro.
        if (!IsServer)
            return;

        transform.Rotate(
            Vector3.up, rotationSpeed * Time.deltaTime
        );
    }
}
