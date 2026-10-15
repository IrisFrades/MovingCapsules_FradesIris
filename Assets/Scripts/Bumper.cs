using UnityEngine;
using Unity.Netcode;

public class Bumper : NetworkBehaviour
{
    [SerializeField] private float pushForce; //Fuerza horizontal
    [SerializeField] private float upwardForce; //Fuerza que hace que el jugador vaya hacia arriba

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (!collision.gameObject.TryGetComponent<NetworkObject>( out var networkObject)) //Comprobamos si el objeto tiene networkObject
            return;

        if (!collision.gameObject.TryGetComponent<PlayerController>( out var player)) //Comprobamos si el player es un jugador
            return;

        Vector3 direction = collision.transform.position - transform.position; //Calcular la direccion de empuje

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        direction.Normalize();

        Vector3 impulse = direction * pushForce + Vector3.up * upwardForce; //Lo impulsamos

        player.ApplyKnockback(impulse); //aplica el impulso al Rigidbody

    }
}