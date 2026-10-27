
using Unity.Netcode;
using Unity.Netcode.Components;
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

    private NetworkTransform networkTransform;

    // Variable de red que indica si el objeto está siendo agarrado
    // Todos los clientes pueden leerla, pero solo el servidor puede modificarla
    private readonly NetworkVariable<bool> isGrabbedNetworked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Permite consultar desde otros scripts si el objeto está agarrado
    public bool IsGrabbed => isGrabbedNetworked.Value;

    private void Awake()
    {
        // Obtenemos los componentes necesarios del objeto
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();
        networkTransform = GetComponent<NetworkTransform>();    
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Ocultamos el Canvas al aparecer el objeto en cada cliente
        SetPromptVisible(false);
    }

    public void SetPromptVisible(bool visible)
    {
        // Mostramos u ocultamos el mensaje de interacción
        if (interactionCanvas != null)
        {
            interactionCanvas.SetActive(visible);
        }
    }

    public bool GrabServer(Transform holdPoint)
    {
        if (!IsServer || IsGrabbed || holdPoint == null)
            return false;

        NetworkObject playerNetworkObject = holdPoint.GetComponentInParent<NetworkObject>();

        if (playerNetworkObject == null)
            return false;

        // Detenemos la física antes de cambiar la posición
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Conservamos inicialmente la posición mundial al cambiar el padre
        if (!NetworkObject.TrySetParent(playerNetworkObject.transform, true))
        {
            rb.isKinematic = false;
            return false;
        }

        // Colocamos la caja en el punto de sujeción del servidor
        transform.SetPositionAndRotation(holdPoint.position, holdPoint.rotation);

        // hacemos que la posicion anterior no interpole la actual y actualizamos en red la actual
        if (networkTransform != null)
        {
            networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        if (objectCollider != null)
            objectCollider.enabled = false;

        isGrabbedNetworked.Value = true;
        SetPromptVisible(false);

        return true;
    }

    public void ThrowServer(Vector3 direction)
    {
        // Solo el servidor puede lanzar un objeto que esté agarrado
        if (!IsServer || !IsGrabbed)
        {
            return;
        }

        // Separamos el objeto del jugador en todos los clientes
        if (!NetworkObject.TryRemoveParent())
        {
            return;
        }

        // Indicamos a todos los clientes que el objeto ya no está agarrado
        isGrabbedNetworked.Value = false;

        // Reactivamos el collider para que el objeto vuelva a tener colisiones
        if (objectCollider != null)
        {
            objectCollider.enabled = true;
        }

        // Reactivamos la física y reiniciamos las velocidades anteriores
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Lanzamos el objeto en la dirección recibida
        rb.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
    }
}
