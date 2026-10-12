using UnityEngine;
using Unity.Netcode;

public class Bumper : NetworkBehaviour
{
    [SerializeField] private float pushForce = 8f;

    private void OnCollisionEnter(Collision collision)
    {
        if(!IsServer)
            return;

        if (!collision.gameObject.TryGetComponent<NetworkObject>(out var networkObject))
            return;

        if (!collision.gameObject.TryGetComponent<Rigidbody>(out var playerRb))
            return;

        Vector3 direction = playerRb.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        direction.Normalize();

        playerRb.AddForce(direction * pushForce, ForceMode.Impulse);
    }
}
