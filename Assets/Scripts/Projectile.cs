using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;

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

        if(rb != null)
        {
            rb.linearVelocity = direction.normalized * speed;
        }

        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        Debug.Log("El proyectil ha tocado: " + other.name);

        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        // Ignorar al jugador que ha disparado.
        if (player != null && player.OwnerClientId == shooterClientId)
            return;

        // Cambiar el color si ha golpeado a otro jugador.
        if (player != null)
        {
            player.ChangeColorFromProjectile();
        }

        // Destruir el proyectil en la red.
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
