using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    private Vector2 inputVector; //Vector de  input enviado por el cliente y almacenado en el Server

    private Rigidbody rb;

    public float jumpForce = 5f;
    public float speed = 5f;

    public float groundDistance = 0.3f;

    [SerializeField] private AudioSource jumpAudio;

    [SerializeField] private Transform raycast;
    private bool jumpRequested;

    /// <summary>
    /// Llevamos el dato por red gracias a la networkVariable, en cambio para que visualmente se vea lo hace el OnPlayerColorChanged
    /// </summary>

    //Color jugador

    private readonly NetworkVariable<Color> playerColour = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    //nombre jugador

    private readonly NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes("Jugador"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Renderer playerRenderer;

    

    [SerializeField] private GameObject projectilePrefab; //Prefab del proyetil
    [SerializeField] private Transform projectileSpawnPoint; //Donde va a aparecer el proyectil
    private float knockbackTimer = 0f;

    [Header("Grab System")]
    [SerializeField] private Transform holdPoint; //Punto donde se colocaran las cajas que se cojan
    [SerializeField] private float grabDistance = 2f; //distancia del circulo que tiene alrededor el personaje

    private NetworkGrabbable nearbyGrabbable; //Para guardar la caja que pueda cojer 
    private NetworkGrabbable grabbedObject; //Donde se actualiza que caja tiene

    private NetworkGrabbable promptTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerRenderer = rb.GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerColour.OnValueChanged += OnPlayerColorChanged; //Cuando el color del player cambie, se tiene que ejecutar OnPlayerColorChanged
        playerName.OnValueChanged += OnPlayerNameChanged; //Cuando el nombre del player cambie, se tiene que ejecutar OnPlayerNameChanged

        if (IsServer)
        {
            playerColour.Value = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.7f, 1f);

            playerName.Value = new FixedString32Bytes($"Jugador {OwnerClientId}");

            rb.isKinematic = false; //En el server, el rb es dinamico para simular las fisicas
        }
        else
        {
            rb.isKinematic = true; //En el cliente, el rb es cinematico para que la fisica local
                                   //del cliente no tenga problemas con las posiciones que reciba
                                   //desde el server via NetworkTransforms (lo que tiene cada player como componente)
        }

    }

    private void OnPlayerColorChanged(Color previousColor, Color newColor)
    {
        ApplyColor(newColor);
    }

    private void ApplyColor(Color color)
    {
        if (playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }
    }

    private void OnPlayerNameChanged(FixedString32Bytes previousName, FixedString32Bytes newName)
    {
        Debug.Log("El jugador ha cambiado su nombre a :" + newName);

    }

    private void Update()
    {
        if (!IsOwner) return; //Solo el cliente owner (IsOwner) puede leer sus propias entradas de teclado

        UpdateNearbyPrompt();

        //Lectura del input local del cliente dueño
        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
        }

        if (Keyboard.current != null &&
            Keyboard.current.fKey.wasPressedThisFrame)
        {
            ShootProjectileServerRpc();
        }

        Vector2 input = new Vector2(h, v).normalized;

        //Enviamos la entrada de control al servidor por un ServerRpc
        SubmitInputServerRpc(input);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) //Si se ha presionado la tecla SPACE
        {
            JumpServerRpc();
        }

        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) //Si se ha presionado la tecla C
        {
            ChangeColorServerRpc();
        }

        FindNearbyGrabbable();

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) //Si se ha presionado la tecla E
        {
            GrabOrThrowServerRpc();
        }

    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // Para que durante ese tiempo el movimiento normal del jugador no sobrescriba la velocidad que le ha dado el bumper
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
        }
        else
        {
            // Movimiento normal del jugador
            Vector3 moveDirection = new Vector3(inputVector.x, 0f, inputVector.y);

            if (moveDirection != Vector3.zero)
            {
                transform.LookAt(transform.position - moveDirection);
            }

            Vector3 targetVelocity = moveDirection * speed;

            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }

        // Salto normal
        if (jumpRequested)
        {
            jumpRequested = false;

            if (Physics.Raycast(raycast.position, Vector3.down, out RaycastHit hit, groundDistance))
            {
                if (hit.collider.CompareTag("Floor"))
                {
                    rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

                    PlayJumpSoundClientRpc();
                }
            }
        }
    }

    public void ChangeColorFromProjectile()
    {
        if (!IsServer)
            return;

        playerColour.Value = Random.ColorHSV(
            0f, 1f,
            0.7f, 1f,
            0.7f, 1f
        );
    }

    /// <summary>
    /// ServerRpc es un mensaje que va del cliente al servidor (mandar algo al servidor)
    /// ClientRpc es un mensaje que va del servidor al cliente(mandar algo a los clientes)
    /// </summary>

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 input)
    {
        //El servidor recibe la direccion pedida por el cliente
        inputVector = input;
    }

    [ServerRpc] //Cliente: Servidor quiero saltar -> Servidor: vale comprobamos si puedes
    private void JumpServerRpc()
    {
        jumpRequested = true;
    }

    [ClientRpc] //Servidor: Clientes reproducimos salto -> Clientes: boing
    private void PlayJumpSoundClientRpc()
    {
        jumpAudio.Play();
    }

    [ServerRpc]
    private void ShootProjectileServerRpc()
    {
        if (projectilePrefab == null || projectileSpawnPoint == null) //Comprobamos si existen el prefab y el spawn point
            return;

        Vector3 spawnPosition = projectileSpawnPoint.position;
        Quaternion spawnRotation = projectileSpawnPoint.rotation;

        GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, spawnRotation); //Crea una instancia del prefab del proyectil

        //Aqui busca si el prefab tiene NetworkObject, DEBEN tener ese componente

        NetworkObject networkObject = projectileObject.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Destroy(projectileObject);
            return;
        }

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Initialize(OwnerClientId, projectileSpawnPoint.forward);
        }

        networkObject.Spawn(); //Se registra el proyectil como objeto de red.
    }

    /// <summary>
    /// Netcode se encarga de enviar el nuevo valor a los demas clientes
    /// </summary>

    [ServerRpc]
    private void ChangeColorServerRpc() //Aqui se cambia la netWork Variable, por ejem: de rojo a azul
    {
        playerColour.Value = Random.ColorHSV(
            0f, 1f,
            0.7f, 1f,
            0.7f, 1f
        );
    }

    [ServerRpc]
    private void GrabOrThrowServerRpc()
    {

        if (grabbedObject != null) //si ya tiene una caja
        {
            grabbedObject.ThrowServer(holdPoint.forward); //la soltamos
            grabbedObject = null; //ya no tenemos ninguna caja
            return; 
        }

        if (holdPoint == null) //si el personaje tiene algun punto donde pueda cojer una caja y colocarla (en su cuerpo como si tuviera manos)
            return;

        // El servidor busca objetos cercanos.
        Collider[] hits = Physics.OverlapSphere(transform.position,grabDistance); //Mira si hay algun objeto con collider dentro de la esfera

        foreach (Collider hit in hits) //se revisa la lista de objetos y que tengan el objeto NetworkGrabbable
        {
            NetworkGrabbable grabbable = hit.GetComponentInParent<NetworkGrabbable>();

            if (grabbable == null)
                continue;

            if (grabbable.GrabServer(holdPoint))
            {
                grabbedObject = grabbable;
                return;
            }
        }
    }

    //Metodo que busca si hay algún objeto que se pueda agarrar cerca del jugador y guarda el primero que encuentra en nearbyGrabbable
    private void FindNearbyGrabbable()
    {
        nearbyGrabbable = null;

        if (holdPoint == null)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, grabDistance);

        foreach (Collider hit in hits)
        {
            NetworkGrabbable grabbable = hit.GetComponentInParent<NetworkGrabbable>();

            if (grabbable != null)
            {
                nearbyGrabbable = grabbable;
                return;
            }
        }
    }

    //Metodo que busca una caja cercana que no esté cojida para decidir si debe mostrar el canva
    private void UpdateNearbyPrompt()
    {
        NetworkGrabbable newTarget = null;

        if (holdPoint != null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, grabDistance);

            foreach (Collider hit in hits)
            {
                NetworkGrabbable grabbable = hit.GetComponentInParent<NetworkGrabbable>();

                if (grabbable == null)
                    continue;

                if (grabbable.IsGrabbed)
                    continue;

                newTarget = grabbable;
                break;
            }
        }

        // Ocultar el aviso anterior cuando dejamos de mirar
        // una caja válida o nos alejamos.
        if (promptTarget != newTarget)
        {
            if (promptTarget != null)
                promptTarget.SetPromptVisible(false);

            promptTarget = newTarget;
        }

        // Solo mostrarlo si la caja sigue libre.
        if (promptTarget != null)
        {
            if (promptTarget.IsGrabbed)
            {
                promptTarget.SetPromptVisible(false);
                promptTarget = null;
            }
            else
            {
                promptTarget.SetPromptVisible(true);
            }
        }
    }

    //el que llama el bumper cuando golpea al jugador
    public void ApplyKnockback(Vector3 impulse)
    {
        if (!IsServer) return;

        // Evitamos que el movimiento normal anule el impulso
        knockbackTimer = 0.3f;

        rb.AddForce(impulse, ForceMode.Impulse);
    }


    

}
