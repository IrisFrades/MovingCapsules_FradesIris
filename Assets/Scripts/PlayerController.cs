using System;
using System.Drawing;
using Unity.Netcode;
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            rb.isKinematic = false; //En el server, el rb es dinamico para simular las fisicas
        }
        else
        {
            rb.isKinematic = true;//En el cliente, el rb es cinematico para que la fisica local
                                  //del cliente no tenga problemas con las posiciones que reciba
                                  //desde el server via NetworkTransforms (lo que tiene cada player como componente)
        }

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


    }

    //private readonly NetworkVariable<Color> playerColour = new NetworkVariable<Color>()
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
