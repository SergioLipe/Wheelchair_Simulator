using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main electric wheelchair movement controller
/// Responsible for: input, speed, acceleration, rotation and physics
/// </summary>
public class Movement : MonoBehaviour
{

    [Header("=== Interface Settings ===")]
    [Tooltip("Show the debug controls on screen?")]
    public bool showInterface = true;


    [Header("=== Speed Settings ===")]
    [Tooltip("Maximum speed in normal mode (km/h)")]
    public float maxSpeedNormal = 6f;

    [Tooltip("Maximum speed in slow/interior mode (km/h)")]
    public float maxSpeedSlow = 3f;

    [Tooltip("Reverse speed (km/h)")]
    public float reverseSpeed = 2f;

    [Header("=== Acceleration Settings ===")]
    [Tooltip("Time to reach maximum speed (seconds)")]
    public float accelerationTime = 2f;

    [Tooltip("Time to stop completely (seconds)")]
    public float brakingTime = 1.5f;

    [Header("=== Rotation Settings ===")]
    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 90f;

    [Tooltip("Can rotate without moving forward/backward? (Only works with front steering)")]
    public bool rotationInPlace = false;

    [Header("=== Level Start Settings ===")]
    [Tooltip("The speed mode this specific level should start with.")]
    public SpeedMode startingSpeedMode = SpeedMode.Slow;

    [Tooltip("The steering mode this specific level should start with.")]
    public WheelController.SteeringType startingSteeringMode = WheelController.SteeringType.FrontSteering;

    [Header("=== Driving Modes ===")]
    [Tooltip("Current speed mode")]
    public SpeedMode currentMode = SpeedMode.Normal;

    [Header("=== Effect Sounds (One-Shot) ===")]
    [Tooltip("Audio launcher for short effects (clicks, collisions)")]
    public AudioSource effectsAudio;

    [Tooltip("Sound to play when changing speed mode (keys 1, 2)")]
    public AudioClip modeChangeSound;

    [Tooltip("Sound to play when changing steering type (key T)")]
    public AudioClip steeringChangeSound;

    [Tooltip("Sound to play when hitting hard")]
    public AudioClip hardCollisionSound;

    [Tooltip("Sound to play when starting to slide on wall")]
    public AudioClip slideStartSound;

    [Tooltip("Minimum speed (in m/s) for collision sound to play (optional)")]
    public float minCollisionSpeed = 0.8f;

    [Header("=== Physics and Limits ===")]
    [Tooltip("Maximum slope it can climb (degrees)")]
    public float maxSlope = 10f;

    [Tooltip("Applied gravity")]
    public float gravity = -9.81f;

    [Header("=== Current State (Debug) ===")]
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float targetSpeed = 0f;
    [SerializeField] private bool emergencyBrake = false;
    [SerializeField] private string currentSteeringType = "Frontal";
    [SerializeField] private float rotationEfficiency = 100f;

    // Components
    private CharacterController controller;
    private Vector3 movementVelocity;
    private WheelController wheelController;
    private CollisionSystem collisionSystem;

    // Smoothed input system
    private float smoothedVerticalInput = 0f;
    private float smoothedHorizontalInput = 0f;

    // Rear steering - feedback
    private float tryingToTurnTime = 0f;

    // Public variable for sound script to know if player is accelerating
    [HideInInspector]
    public bool playerIsAccelerating = false;

    // Cache for sounds (to not repeat)
    private bool slidingCache = false;
    private string steeringTypeCache = "Frontal";
    private bool inCollisionCache = false;

    // Sound cooldown system
    private float lastCollisionSoundTime = 0f;
    private float lastSlideSoundTime = 0f;
    private float soundCooldown = 1.0f; // 1 second cooldown


    public enum SpeedMode
    {
        Slow,
        Normal,
        Off
    }

    void Start()
    {
        SetupCharacterController();
        SetupComponents();
        ConvertSpeeds();
        InitializeCache();
        InitializeLevelSettings();
    }

    /// <summary>
    /// Applies the starting speed and steering modes defined in the Inspector for this specific level.
    /// </summary>
    private void InitializeLevelSettings()
    {
        // 1. Set the initial speed mode (Slow / Normal / Off)
        currentMode = startingSpeedMode;

        // 2. Set the initial steering mode (Front / Rear)
        if (wheelController != null)
        {
            wheelController.SetSteeringType(startingSteeringMode);
        }
        else
        {
            Debug.LogWarning("WheelController is missing! Cannot set initial steering type.");
        }
    }

    /// <summary>
    /// Configures CharacterController with optimized values for realistic contact
    /// </summary>
    private void SetupCharacterController()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        // Optimized values for realistic obstacle contact
        controller.height = 0.8f;
        controller.radius = 0.17f;
        controller.center = new Vector3(0, 0.4f, 0);
        controller.skinWidth = 0.0001f;
        controller.minMoveDistance = 0.0f;
        controller.stepOffset = 0.08f;

        // Slightly elevate at start to avoid floor collision
        transform.position += Vector3.up * 0.1f;
    }

    /// <summary>
    /// Initializes references to necessary components
    /// </summary>
    private void SetupComponents()
    {
        wheelController = GetComponent<WheelController>();

        collisionSystem = GetComponent<CollisionSystem>();
        if (collisionSystem == null)
        {
            collisionSystem = gameObject.AddComponent<CollisionSystem>();
        }
        collisionSystem.Initialize(controller, transform);
    }

    /// <summary>
    /// Converts speeds from km/h to m/s (internally used format)
    /// </summary>
    private void ConvertSpeeds()
    {
        maxSpeedNormal = maxSpeedNormal / 3.6f;
        maxSpeedSlow = maxSpeedSlow / 3.6f;
        reverseSpeed = reverseSpeed / 3.6f;
    }

    /// <summary>
    /// Initializes cache values for state change detection
    /// </summary>
    private void InitializeCache()
    {
        if (wheelController != null)
        {
            steeringTypeCache = wheelController.GetSteeringType().ToString();
        }
    }

    void Update()
    {
        UpdateSteeringState();
        collisionSystem.Update();
        ProcessSoundEffects();
        UpdateTimers();

        ManageModes();

        if (currentMode != SpeedMode.Off)
        {
            ProcessRealisticInput();
            ApplyRealisticMovement();
        }
        else
        {
            EmergencyStop();
            ApplyVerticalMovement();
        }

        ApplyGravity();
    }

    /// <summary>
    /// Updates current steering type for reference
    /// </summary>
    private void UpdateSteeringState()
    {
        if (wheelController != null)
        {
            currentSteeringType = wheelController.GetSteeringType().ToString();
        }
    }

    /// <summary>
    /// Processes and plays sound effects based on state changes
    /// </summary>
    private void ProcessSoundEffects()
    {
        float currentTime = Time.time;

        // Steering change sound
        if (currentSteeringType != steeringTypeCache)
        {
            PlaySound(steeringChangeSound);
            steeringTypeCache = currentSteeringType;
        }

        // Determina estados atuais
        bool slidingNow = collisionSystem.IsWallSliding;
        bool inCollisionNow = (collisionSystem.IsInCollision ||
                               collisionSystem.IsFrontBlocked ||
                               collisionSystem.IsBackBlocked);

        // PRIORIDADE: Se está a deslizar, NÃO toca som de colisão
        if (slidingNow)
        {
            // Toca som de deslizamento se começou agora E respeitando cooldown
            if (!slidingCache && currentTime - lastSlideSoundTime > soundCooldown)
            {
                PlaySound(slideStartSound);
                lastSlideSoundTime = currentTime;
            }

            // Marca colisão como falsa para não tocar som de colisão
            inCollisionCache = inCollisionNow; // Atualiza cache sem tocar som
        }
        else if (inCollisionNow)
        {
            // Só toca som de colisão se NÃO está a deslizar
            if (!inCollisionCache && currentTime - lastCollisionSoundTime > soundCooldown)
            {
                PlaySound(hardCollisionSound);
                lastCollisionSoundTime = currentTime;
            }
        }

        // Atualiza caches
        slidingCache = slidingNow;
        if (!slidingNow) // Só atualiza cache de colisão se não está a deslizar
        {
            inCollisionCache = inCollisionNow;
        }
    }

    /// <summary>
    /// Updates warning and feedback timers
    /// </summary>
    private void UpdateTimers()
    {
        if (tryingToTurnTime > 0)
        {
            tryingToTurnTime -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Manages speed mode switching through keyboard input
    /// </summary>
    void ManageModes()
    {
        // Key 1: Slow Mode
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentMode = SpeedMode.Slow;
            PlaySound(modeChangeSound);
        }
        // Key 2: Normal Mode
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentMode = SpeedMode.Normal;
            PlaySound(modeChangeSound);
        }
        // Space: Emergency brake
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            currentMode = SpeedMode.Off;
            emergencyBrake = true;
        }
        // Release space: Return to normal mode
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            currentMode = SpeedMode.Normal;
            emergencyBrake = false;
        }
    }

    /// <summary>
    /// Processes player input applying smoothing and realistic physics
    /// </summary>
    void ProcessRealisticInput()
    {
        // Get player input
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        // Update acceleration flag for sound system
        playerIsAccelerating = (Mathf.Abs(verticalInput) > 0.1f);

        // Apply input smoothing
        float smoothing = 3f;
        smoothedVerticalInput = Mathf.Lerp(smoothedVerticalInput, verticalInput, smoothing * Time.deltaTime);
        smoothedHorizontalInput = Mathf.Lerp(smoothedHorizontalInput, horizontalInput, smoothing * Time.deltaTime);

        // Determine maximum speed based on mode
        float maxSpeed = currentMode == SpeedMode.Slow ?
                         maxSpeedSlow : maxSpeedNormal;

        // Apply realistic collision blocking system
        ApplyCollisionBlocking(ref verticalInput, ref maxSpeed);

        // Calculate acceleration and deceleration
        ApplyAccelerationDeceleration(maxSpeed);

        // Process rotation
        ProcessRotation(smoothedHorizontalInput);
    }

    /// <summary>
    /// Applies movement blocking based on detected collisions
    /// </summary>
    private void ApplyCollisionBlocking(ref float verticalInput, ref float maxSpeed)
    {
        // Front blocking - prevents forward movement
        if (collisionSystem.IsFrontBlocked && smoothedVerticalInput > 0)
        {
            smoothedVerticalInput = 0;
            targetSpeed = 0;

            // Apply small pushback if trying to force
            if (verticalInput > 0.5f)
            {
                currentSpeed = Mathf.Max(currentSpeed - 0.5f * Time.deltaTime, -0.05f);
            }
        }
        // Back blocking - prevents reverse
        else if (collisionSystem.IsBackBlocked && smoothedVerticalInput < 0)
        {
            smoothedVerticalInput = 0;
            targetSpeed = 0;
        }
        // Normal movement when not blocked
        else
        {
            // Use reverse speed when input is negative
            if (smoothedVerticalInput < 0)
            {
                maxSpeed = reverseSpeed;
            }

            targetSpeed = smoothedVerticalInput * maxSpeed;
        }
    }

    /// <summary>
    /// Applies gradual acceleration or deceleration based on current state
    /// </summary>
    private void ApplyAccelerationDeceleration(float maxSpeed)
    {
        bool notBlocked = !collisionSystem.IsFrontBlocked && !collisionSystem.IsBackBlocked;
        bool accelerating = Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed);

        if (notBlocked && accelerating)
        {
            // Gradual acceleration
            float acceleration = maxSpeed / accelerationTime;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Gradual deceleration
            float deceleration = maxSpeed / brakingTime;

            if (collisionSystem.IsFrontBlocked || collisionSystem.IsBackBlocked)
            {
                // Stop immediately if blocked
                currentSpeed = 0;
            }
            else if (collisionSystem.IsInCollision)
            {
                // Faster deceleration in collision
                deceleration *= 2f;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }
            else
            {
                // Normal deceleration
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Processes wheelchair rotation based on steering type and speed
    /// </summary>
    void ProcessRotation(float horizontalInput)
    {
        float rotationMultiplier = 1f;
        bool isRearSteering = false;
        rotationEfficiency = 100f;

        // Check steering type
        if (wheelController != null)
        {
            isRearSteering = wheelController.GetSteeringType() == WheelController.SteeringType.RearSteering;

            if (isRearSteering)
            {
                rotationMultiplier = 2.5f; // Rear steering turns more easily
            }
        }

        bool isStationary = Mathf.Abs(currentSpeed) < 0.1f;

        if (isRearSteering)
        {
            ProcessRearRotation(isStationary, horizontalInput, ref rotationMultiplier);
        }
        else
        {
            ProcessFrontRotation(isStationary, ref rotationMultiplier);
        }

        // Apply calculated rotation
        float rotation = horizontalInput * rotationSpeed * rotationMultiplier * Time.deltaTime;
        transform.Rotate(0, rotation, 0);
    }

    /// <summary>
    /// Processes rotation logic specific to rear steering
    /// </summary>
    private void ProcessRearRotation(bool isStationary, float horizontalInput, ref float multiplier)
    {
        if (isStationary)
        {
            // Rear steering doesn't allow stationary rotation
            rotationEfficiency = 0f;

            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                tryingToTurnTime = 1f;
            }

            multiplier = 0f;
        }
        else
        {
            float normalizedSpeed = Mathf.Abs(currentSpeed) / maxSpeedNormal;
            float baseEfficiency = Mathf.Lerp(0.2f, 1f, normalizedSpeed);
            multiplier *= baseEfficiency;

            if (currentSpeed < 0)
            {
                // In reverse rotation is inverted and less efficient
                multiplier *= -0.8f;
                rotationEfficiency = baseEfficiency * 80f;
            }
            else
            {
                rotationEfficiency = baseEfficiency * 100f;
            }
        }
    }

    /// <summary>
    /// Processes rotation logic specific to front steering
    /// </summary>
    private void ProcessFrontRotation(bool isStationary, ref float multiplier)
    {
        if (isStationary && !rotationInPlace)
        {
            // No rotation when stationary (unless rotationInPlace is active)
            rotationEfficiency = 0f;
            multiplier = 0f;
        }
        else if (isStationary && rotationInPlace)
        {
            // Facilitated rotation when stationary
            multiplier *= 1.5f;
            rotationEfficiency = 100f;
        }
        else
        {
            // Rotation slightly increases with speed
            float normalizedSpeed = Mathf.Abs(currentSpeed) / maxSpeedNormal;
            multiplier *= (1f + normalizedSpeed * 0.8f);
            rotationEfficiency = 100f;
        }
    }

    /// <summary>
    /// Applies calculated movement to CharacterController including sliding
    /// </summary>
    void ApplyRealisticMovement()
    {
        Vector3 movementDirection = Vector3.zero;

        // Use slide direction if sliding on wall
        if (collisionSystem.IsWallSliding && collisionSystem.SlideDirection != Vector3.zero)
        {
            movementDirection = collisionSystem.SlideDirection * Mathf.Abs(currentSpeed) * 0.5f;
        }
        else
        {
            movementDirection = transform.forward * currentSpeed;
        }

        // Preserve vertical movement (gravity)
        movementDirection.y = movementVelocity.y;

        controller.Move(movementDirection * Time.deltaTime);
    }

    /// <summary>
    /// Applies only vertical movement (used when in off mode)
    /// </summary>
    void ApplyVerticalMovement()
    {
        Vector3 verticalMovement = new Vector3(0, movementVelocity.y, 0);
        controller.Move(verticalMovement * Time.deltaTime);
    }

    /// <summary>
    /// Applies gravity to vertical movement
    /// </summary>
    void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            // Small downward force to keep on ground
            movementVelocity.y = -0.5f;
        }
        else
        {
            // Accumulate fall velocity
            movementVelocity.y += gravity * Time.deltaTime;

            // Limit maximum fall speed
            movementVelocity.y = Mathf.Max(movementVelocity.y, -20f);
        }
    }

    /// <summary>
    /// Completely stops the wheelchair (emergency brake)
    /// </summary>
    void EmergencyStop()
    {
        currentSpeed = 0;
        targetSpeed = 0;

        collisionSystem.ClearSlide();

        if (wheelController != null)
        {
            wheelController.StopWheels();
        }
    }

    /// <summary>
    /// Unity callback when CharacterController collides with objects
    /// Delegates processing to collision system
    /// </summary>
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        collisionSystem.ProcessCollision(hit, currentSpeed, ref currentSpeed);
    }

    // ===== PUBLIC METHODS =====

    /// <summary>
    /// Returns normalized current speed (0-1) based on normal maximum speed
    /// </summary>
    public float GetNormalizedSpeed()
    {
        return currentSpeed / maxSpeedNormal;
    }

    /// <summary>
    /// Checks if wheelchair is moving
    /// </summary>
    public bool IsMoving()
    {
        return Mathf.Abs(currentSpeed) > 0.1f;
    }

    /// <summary>
    /// Reduces current speed by a multiplier
    /// </summary>
    public void ReduceSpeed(float multiplier)
    {
        currentSpeed *= multiplier;
    }

    /// <summary>
    /// Plays an effect sound through the effects AudioSource
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        if (effectsAudio != null && clip != null)
        {
            effectsAudio.PlayOneShot(clip);
        }
    }

    // ===== GRAPHICAL INTERFACE =====

    void OnGUI()
    {

        //Show interface
        if (!showInterface || Time.timeScale == 0) return;

        // Modern styling with gradient-like semi-transparent background
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.18f, 0.22f, 0.75f));
        boxStyle.border = new RectOffset(8, 8, 8, 8);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.normal.textColor = Color.white;

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(0.5f, 0.95f, 1f, 1f);

        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 14;
        valueStyle.fontStyle = FontStyle.Bold;

        // ===== LEFT - INFO =====
        GUI.Box(new Rect(15, 15, 240, 110), "", boxStyle);

        // Header
        GUI.Label(new Rect(30, 22, 200, 25), "CADEIRA DE RODAS", headerStyle);

        // Elegant separator line with glow effect
        GUI.color = new Color(0.5f, 0.95f, 1f, 0.6f);
        GUI.DrawTexture(new Rect(30, 48, 195, 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Mode
        string modeText = currentMode == SpeedMode.Slow ? "Interior" :
                         (currentMode == SpeedMode.Off ? "Desligado" : "Exterior");
        Color modeColor = currentMode == SpeedMode.Slow ? new Color(1f, 0.9f, 0.5f, 1f) :
                         (currentMode == SpeedMode.Off ? new Color(1f, 0.6f, 0.6f, 1f) : new Color(0.6f, 1f, 0.7f, 1f));

        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 58, 90, 22), "Modo:", labelStyle);
        valueStyle.normal.textColor = modeColor;
        GUI.Label(new Rect(120, 58, 120, 22), modeText, valueStyle);

        // Speed
        float maxDisplaySpeed = currentMode == SpeedMode.Slow ? 3f : 6f;
        string speedText = $"{(currentSpeed * 3.6f):F1}/{maxDisplaySpeed:F0} km/h";
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 78, 90, 22), "Veloc:", labelStyle);
        valueStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(120, 78, 120, 22), speedText, valueStyle);

        // Steering (with more bottom padding)
        string steeringText = currentSteeringType.Contains("Rear") ? "Traseira" : "Frontal";
        Color steeringColor = currentSteeringType.Contains("Rear") ? new Color(1f, 0.75f, 1f, 1f) : new Color(0.65f, 0.95f, 1f, 1f);
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.Label(new Rect(30, 96, 90, 22), "Direção:", labelStyle);
        valueStyle.normal.textColor = steeringColor;
        GUI.Label(new Rect(120, 96, 120, 22), steeringText, valueStyle);

        // ===== EMERGENCY BRAKE (CENTER TOP) =====
        if (emergencyBrake)
        {
            GUIStyle emergencyBoxStyle = new GUIStyle(GUI.skin.box);
            emergencyBoxStyle.normal.background = MakeTex(2, 2, new Color(0.9f, 0.2f, 0.2f, 0.85f));

            GUI.Box(new Rect(Screen.width / 2 - 150, 15, 300, 40), "", emergencyBoxStyle);

            GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.fontSize = 16;
            warningStyle.fontStyle = FontStyle.Bold;
            warningStyle.alignment = TextAnchor.MiddleCenter;
            warningStyle.normal.textColor = Color.white;

            GUI.Label(new Rect(Screen.width / 2 - 150, 22, 300, 26), "⚠ TRAVÃO DE EMERGÊNCIA ⚠", warningStyle);
        }

        // ===== RIGHT - CONTROLS =====
        float rightX = Screen.width - 240 - 15;

        GUI.Box(new Rect(rightX, 15, 240, 138), "", boxStyle);

        headerStyle.normal.textColor = new Color(0.6f, 1f, 0.7f, 1f);
        GUI.Label(new Rect(rightX + 15, 22, 200, 25), "CONTROLOS", headerStyle);

        // Elegant separator line
        GUI.color = new Color(0.6f, 1f, 0.7f, 0.6f);
        GUI.DrawTexture(new Rect(rightX + 15, 48, 210, 2), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle keyStyle = new GUIStyle(GUI.skin.label);
        keyStyle.fontSize = 13;
        keyStyle.fontStyle = FontStyle.Bold;
        keyStyle.normal.textColor = new Color(1f, 0.95f, 0.7f, 1f);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label);
        descStyle.fontSize = 13;
        descStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        int y = 58;
        int lineH = 18;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "WASD/Setas", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Mover", descStyle);
        y += lineH;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "1", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Modo Interior", descStyle);
        y += lineH;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "2", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Modo Exterior", descStyle);
        y += lineH;

        GUI.Label(new Rect(rightX + 20, y, 100, 18), "T", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Mudar Direção", descStyle);
        y += lineH;

        keyStyle.normal.textColor = new Color(1f, 0.7f, 0.7f, 1f);
        GUI.Label(new Rect(rightX + 20, y, 100, 18), "ESPAÇO", keyStyle);
        GUI.Label(new Rect(rightX + 125, y, 110, 18), "Travão", descStyle);
    }

    // Helper to create colored textures for GUI backgrounds
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

}