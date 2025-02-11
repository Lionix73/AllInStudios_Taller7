﻿using System.Collections;
using Unity.Cinemachine;
using Fusion.Addons.KCC;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Fusion;
public class PlayerControllerPhoton : NetworkBehaviour
{

    [Header("KCC")]
    [SerializeField] KCC kcc;
    //[SerializeField] private AudioSource source;
    [Header("Movement Settings")]
    [SerializeField] private Vector3 jumpImpulse = new(0f, 10f, 0f);
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float slideDuration = 3f;
    public float crouchHeight = 0.9f;
    public float normalHeight = 1.8f;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    public int maxJumps = 2;

    [Header("Animator")]
    public Animator animator;

    [Header("Movimiento y Dash")]
    [SerializeField] private float dashImpulse = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCD =2f;
    [SerializeField] private float doubleJumpCD = 2f;

    private bool isDashing = false;
    private bool canDash = true;
    bool rayDash;
    private Vector3 dashDirection;

    [Header("Components")]
    public Rigidbody rb;
    public CapsuleCollider playerCollider;
    public Transform cameraTransform;
    public Transform playerHead;
    [System.Obsolete] public CinemachineCamera freeLookCamera;

    [Header("Camera Settings")]
    [SerializeField] private Transform camTarget;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float currentFOV = 65;
    [SerializeField] private float aimFOV = 55;
    [SerializeField] private float tFOV = 1;
    [SerializeField] private float rotationSpeed = 10f;
    private bool wasAiming;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private float aimInput;
    [Networked] public bool IsRunning { get; set; }
    [Networked] public bool IsCrouching { get; set; }
    [Networked] public bool IsSliding { get; set; }
    [Networked] public bool CanSlide { get; set; }
    [Networked] public bool CanCrouch { get; set; }
    [Networked] public bool CanJump { get; set; }

    private float speedX;
    private float speedY;

    private int jumpCount = 0;
    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; } //Synchronize sound in all clients

    private Vector3 slideDirection;
    private float slideTimer = 0f;

    public float DoubleJumpCDFactor => (DoubleJumpCD.RemainingTime(Runner) ?? 0f) / doubleJumpCD; //Returns the remaining of cooldown in a range of 0 to 1
    [Networked] private TickTimer DashCD { get; set; }
    [Networked] private TickTimer DoubleJumpCD { get;  set;}

    private PlayerInput playerInput;
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;

    public bool IsReady; //Server is the only one who cares about this

    public override void Spawned()
    {
        //kcc.SetGravity(Physics.gravity.y * 2f);
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody>();
        GameObject camera = FindFirstObjectByType<CinemachineCamera>().gameObject;
        freeLookCamera = camera.GetComponent<CinemachineCamera>();
        cameraTransform = camera.transform;
        
            if(HasInputAuthority)
            {
                inputManager = Runner.GetComponent<InputManager>();
                inputManager.LocalPlayer = this;
                freeLookCamera.Target.TrackingTarget = camTarget;
                kcc.Settings.ForcePredictedLookRotation = true;
            Debug.Log("Camara Encontrada");
            }
    }


    public override void FixedUpdateNetwork()
    {

        //adjustFOV();
        HandleAnimations();
        HandleMovement();
        //HandleRotation();
        
    }

    private void HandleMovement()
    {
        if (GetInput(out NetworkInputData input))
        {
            CheckJump(input);
            kcc.AddLookRotation(input.LookDelta * lookSensitivity);
            HandleRotation();

            if (input.Buttons.WasPressed(PreviousButtons,InputButton.Run))
            {
                IsRunning = !IsRunning;
                IsCrouching = false;
            }

            float speed = IsRunning ? runSpeed : walkSpeed;
            
            SetInputDirection(input, speed);
            CheckDash(input);

            PreviousButtons = input.Buttons;
            baseLookRotation = kcc.GetLookRotation();
            if (aimInput > 0.1f)
            {
                //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
    private void CheckJump(NetworkInputData input)
    {
        if(input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (kcc.FixedData.IsGrounded)
            {
                kcc.Jump(jumpImpulse);
                JumpSync++;

            }
            else if(DoubleJumpCD.ExpiredOrNotRunning(Runner))
            {
                kcc.Jump(jumpImpulse);
                DoubleJumpCD = TickTimer.CreateFromSeconds(Runner, doubleJumpCD);
                JumpSync++;
            }
        }
    }
    private void CheckDash(NetworkInputData input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Dash))
        {
            if (IsCrouching || IsSliding) 
                return;
            if (DashCD.ExpiredOrNotRunning(Runner))
            {
                isDashing = true;
                Vector3 worldDirection = kcc.FixedData.TransformRotation * input.Direction.X0Y();
                Debug.Log(worldDirection);
                kcc.Jump(worldDirection * dashImpulse);
                Debug.Log("Dash");
                DashCD = TickTimer.CreateFromSeconds(Runner, dashCD);
            }

        }

    }
    private void SetInputDirection(NetworkInputData input, float speed)
    {
        Vector3 worldDirection = kcc.FixedData.TransformRotation * input.Direction.X0Y()*speed;
        kcc.SetInputDirection(worldDirection);
    }
    public override void Render()
    {
        //HandleAnimations();
        if (kcc.Settings.ForcePredictedLookRotation && HasInputAuthority)
        {
            Vector2 predictedLookRotation = baseLookRotation + inputManager.AccumulatedMouseDelta * lookSensitivity;
            kcc.SetLookRotation(predictedLookRotation);
        }
            HandleRotation();
    }
    private void HandleRotation()
    {
        camTarget.localRotation = Quaternion.Euler(kcc.GetLookRotation().x,0f, 0f);
    }

    private void HandleAnimations()
    {
        bool isMoving = false;
        if (GetInput(out NetworkInputData input))
        { 
            isMoving = input.Direction.sqrMagnitude > 0.1f;
            speedX = input.Direction.x;
            speedY = input.Direction.y;
        }

            animator.SetBool("isMoving", isMoving);
            animator.SetBool("isRunning", IsRunning);
            animator.SetBool("isCrouching", IsCrouching);
            animator.SetBool("isSliding", IsSliding);

            animator.SetFloat("SpeedY", speedY);
            animator.SetFloat("SpeedX", speedX);
    }

    private void ResetColliderHeight()
    {
        playerCollider.height = normalHeight;
        playerCollider.center.Set(0f, 0.9f, 0f);
    }

    /*public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        aimInput = context.ReadValue<float>();
    }*/

    public void adjustFOV()
    {
        if (aimInput > 0.1f)
        {
            freeLookCamera.Lens.FieldOfView = Mathf.Lerp(aimFOV, currentFOV, tFOV * Time.deltaTime);
            wasAiming = true;
            IsRunning = false;
        }
        else if (aimInput == 0 && wasAiming == true)
        {
            freeLookCamera.Lens.FieldOfView = Mathf.Lerp(currentFOV, aimFOV, tFOV * Time.deltaTime);
            wasAiming = false;
        }
    }

    private void Jumped()
    {
        //source.Play();
    }



[Rpc(RpcSources.InputAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)] // The ui update is actually allowed to run locally when the player indicates their readiness
    public void RPC_SetReady()
    {
        IsReady = true;
        if (HasInputAuthority) { }
            //UIManager.Singleton.DidSetReady();
    }
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        kcc.SetPosition(position);
        kcc.SetLookRotation(rotation);
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerName(string name)
    {
        //Name = name;
    }

}