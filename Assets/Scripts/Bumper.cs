using UnityEngine;
using Unity.Netcode;

public class Bumper : NetworkBehaviour
{
    [SerializeField] private float pushForce = 8f;

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Bumper: colisión con " + collision.gameObject.name);

        if (!IsServer)
        {
            Debug.Log("No soy el servidor");
            return;
        }

        if (!collision.gameObject.TryGetComponent<NetworkObject>(out var networkObject))
        {
            Debug.Log("El objeto no tiene NetworkObject");
            return;
        }

        if (!collision.gameObject.TryGetComponent<Rigidbody>(out var playerRb))
        {
            Debug.Log("El objeto no tiene Rigidbody");
            return;
        }

        Vector3 direction = playerRb.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        direction.Normalize();

        playerRb.AddForce(
            direction * pushForce,
            ForceMode.Impulse
        );

        Debug.Log("¡Bumper activado!");
    }
}