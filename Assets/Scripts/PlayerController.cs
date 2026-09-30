using System;
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

    [SerializeField] private Transform raycast;

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

        if(Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
        }

        Vector2 input = new Vector2(h, v).normalized;

        //Enviamos la entrada de control al servidor por un ServerRpc
        SubmitInputServerRpc(input);

        if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) {
            JumpServerRpc();
        }


    }
    /// <summary>
    /// ServerRpc: Metodo ejecutado Exclusivamente en el servidor tras ser invocado por el CLiente
    /// </summary>
    
    

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 input)
    {
        //El servidor recibe la direccion pedida por el cliente
        inputVector = input;
    }

    [ServerRpc]
    private void JumpServerRpc()
    {
        RaycastHit hit;

        if(Physics.Raycast(raycast.position, Vector3.down, out hit, groundDistance))
        {
            if (hit.collider.CompareTag("Floor")) //detectamos si el raycast esta tocando el suelo
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); //añadimos una fuerza
            }
        }
    }


    private void FixedUpdate()
    {
        if (!IsServer) return; //"Los cambios reales de física los decide y aplica únicamente el servidor;
                               //los clientes no deben hacerlo por su cuenta."

        //Aplicar la velocidad en el server usando Rb
        Vector3 moveDirection = new Vector3(inputVector.x, 0f, inputVector.y);

        if(moveDirection != Vector3.zero)
        {
            transform.LookAt(transform.position - moveDirection); //Usamos lookAt para que los personajes coloquen su orientacion segun hacia donde se esten moviendo
        }

        //Conservamos la velocidad vertical que haya y modificamos x/z
        Vector3 targetVelocity = moveDirection * speed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    
}
