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
            rb.isKinematic = true;//En el cliente, el rb es cinematico para que la fisica local
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
        if(playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }
    }

    private void OnPlayerNameChanged(FixedString32Bytes previousName, FixedString32Bytes newName)
    {
        Debug.Log("El jugador ha cambiado su nombre a :" + newName);

    }

    void Update()
    {
        if (!IsOwner) return; //Solo el cliente owner (IsOwner) puede leer sus propias entradas de teclado

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

        Vector2 input = new Vector2(h, v).normalized;

        //Enviamos la entrada de control al servidor por un ServerRpc
        SubmitInputServerRpc(input);

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            JumpServerRpc();
        }

        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            ChangeColorServerRpc();
        }

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

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // Movimiento
        Vector3 moveDirection = new Vector3(inputVector.x, 0f, inputVector.y);

        if (moveDirection != Vector3.zero)
        {
            transform.LookAt(transform.position - moveDirection);
        }

        Vector3 targetVelocity = moveDirection * speed;

        rb.linearVelocity = new Vector3(
            targetVelocity.x,
            rb.linearVelocity.y,
            targetVelocity.z
        );

        // Salto
        if (jumpRequested)
        {
            jumpRequested = false;

            if (Physics.Raycast(raycast.position, Vector3.down, out RaycastHit hit, groundDistance))
            {
                if (hit.collider.CompareTag("Floor"))
                {
                    rb.AddForce(
                        Vector3.up * jumpForce, ForceMode.Impulse
                    );

                    PlayJumpSoundClientRpc();
                }
            }
        }
    }


}
