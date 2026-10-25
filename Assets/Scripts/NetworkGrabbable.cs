
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkGrabbable : NetworkBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float throwForce = 10f;
    [SerializeField] private GameObject interactionCanvas;

    private Rigidbody rb;
    private Collider objectCollider;

    // Variable de red que indica si el objeto está siendo agarrado.
    // Todos los clientes pueden leerla, pero solo el servidor puede modificarla.
    private readonly NetworkVariable<bool> isGrabbedNetworked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Permite consultar desde otros scripts si el objeto está agarrado.
    public bool IsGrabbed => isGrabbedNetworked.Value;

    private void Awake()
    {
        // Obtenemos los componentes necesarios del objeto.
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Ocultamos el Canvas al aparecer el objeto en cada cliente.
        SetPromptVisible(false);
    }

    public void SetPromptVisible(bool visible)
    {
        // Mostramos u ocultamos el mensaje de interacción.
        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(visible);
        }
    }

    public bool GrabServer(Transform holdPoint)
    {
        // Solo el servidor puede agarrar el objeto.
        // No permitimos agarrarlo si ya está agarrado o falta el punto de sujeción.
        if (!IsServer || IsGrabbed || holdPoint == null)
        {
            return false;
        }

        // Buscamos el NetworkObject del jugador al que pertenece el holdPoint.
        NetworkObject playerNetworkObject = holdPoint.GetComponentInParent<NetworkObject>();

        if (playerNetworkObject == null)
        {
            return false;
        }

        // Solo el servidor modifica la jerarquía de red.
        // El objeto pasa a ser hijo del jugador.
        if (!NetworkObject.TrySetParent(playerNetworkObject.transform, false))
        {
            return false;
        }

        // Indicamos a todos los clientes que el objeto está agarrado.
        isGrabbedNetworked.Value = true;

        // Ocultamos el mensaje de interacción.
        SetPromptVisible(false);

        // Desactivamos la física mientras se sostiene el objeto.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Evitamos que el objeto choque con el jugador mientras lo sostiene.
        if (objectCollider != null)
        {
            objectCollider.enabled = false;
        }

        // Colocamos y orientamos el objeto en el punto de sujeción.
        transform.position = holdPoint.position;
        transform.rotation = holdPoint.rotation;

        return true;
    }

    public void ThrowServer(Vector3 direction)
    {
        // Solo el servidor puede lanzar un objeto que esté agarrado.
        if (!IsServer || !IsGrabbed)
        {
            return;
        }

        // Separamos el objeto del jugador en todos los clientes.
        if (!NetworkObject.TryRemoveParent())
        {
            return;
        }

        // Indicamos a todos los clientes que el objeto ya no está agarrado.
        isGrabbedNetworked.Value = false;

        // Reactivamos el collider para que el objeto vuelva a tener colisiones.
        if (objectCollider != null)
        {
            objectCollider.enabled = true;
        }

        // Reactivamos la física y reiniciamos las velocidades anteriores.
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Lanzamos el objeto en la dirección recibida.
        rb.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
    }
}
