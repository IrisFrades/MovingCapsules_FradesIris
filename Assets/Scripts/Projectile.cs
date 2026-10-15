using UnityEngine;
using Unity.Netcode;

public class Projectile : NetworkBehaviour
{
    [SerializeField] private float speed = 15f;

    private Rigidbody rb;
    private ulong shooterClientId;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(ulong shooterId, Vector3 direction)
    {
        shooterClientId = shooterId;

        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * speed;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Solo el servidor gestiona los impactos.
        if (!IsServer)
            return;

        Debug.Log("El proyectil ha tocado: " + other.name);

        // Comprobar si hemos golpeado a un jugador.
        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        // Ignorar al jugador que ha disparado.
        if (player != null &&
            player.OwnerClientId == shooterClientId)
        {
            return;
        }

        // Cambiar el color del jugador alcanzado.
        if (player != null)
        {
            player.ChangeColorFromProjectile();

            Debug.Log("¡Jugador alcanzado! Cambiando su color.");
        }

        // Destruir el proyectil para todos los clientes.
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}