using UnityEngine;

/// <summary>
// Sound system for electric wheelchair
/// Only startup and loop based on user input
/// </summary>
public class Sounds : MonoBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("AudioSource for motor sound (continuous loop)")]
    public AudioSource motorAudioSource;
    
    [Tooltip("AudioSource for one-shot sounds (startup, clicks, collisions)")]
    public AudioSource effectsAudioSource;

    [Header("Motor Sounds")]
    [Tooltip("Startup sound (2 seconds)")]
    public AudioClip startupSound;
    
    [Tooltip("Continuous motor sound (loop)")]
    public AudioClip loopSound;

    [Header("Interface Sound")]
    [Tooltip("Click sound when changing modes or direction")]
    public AudioClip clickSound;

    [Header("Collision Sounds")]
    [Tooltip("Front/rear collision sound")]
    public AudioClip frontCollisionSound;
    
    [Tooltip("Side collision sound (sliding)")]
    public AudioClip sideCollisionSound;
    
    [Tooltip("Minimum collision velocity to play sound")]
    public float minCollisionVelocity = 0.5f;
    
    [Tooltip("Collision volume")]
    [Range(0f, 1f)]
    public float collisionVolume = 0.7f;

    [Header("Motor Settings")]
    [Tooltip("Startup sound volume")]
    [Range(0f, 1f)]
    public float startupVolume = 0.7f;
    
    [Tooltip("Base motor loop volume")]
    [Range(0f, 1f)]
    public float loopVolume = 0.5f;
    
    [Tooltip("Fade out speed (seconds)")]
    [Range(0.5f, 5f)]
    public float fadeOutSpeed = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool isAccelerating = false;
    [SerializeField] private bool loopStarted = false;
    [SerializeField] private float acceleratingTime = 0f;

    void Start()
    {
        SetupAudioSources();
    }

    void Update()
    {
        CheckAccelerationInput();
        UpdateMotorLoop();
        ApplyFadeOut();
    }

    /// <summary>
    /// Configures audio sources with proper settings
    /// </summary>
    private void SetupAudioSources()
    {
        if (motorAudioSource == null)
        {
            motorAudioSource = gameObject.AddComponent<AudioSource>();
        }
        motorAudioSource.loop = true;
        motorAudioSource.volume = 0f;
        motorAudioSource.playOnAwake = false;
        
        if (effectsAudioSource == null)
        {
            effectsAudioSource = gameObject.AddComponent<AudioSource>();
        }
        effectsAudioSource.loop = false;
        effectsAudioSource.playOnAwake = false;
    }

    /// <summary>
    /// Checks user input and manages acceleration state
    /// </summary>
    private void CheckAccelerationInput()
    {
        float verticalInput = Input.GetAxis("Vertical");
        bool acceleratingNow = Mathf.Abs(verticalInput) > 0.1f;
        
        if (acceleratingNow && !isAccelerating)
        {
            StartAcceleration();
        }
        else if (!acceleratingNow && isAccelerating)
        {
            StopAcceleration();
        }
    }

    /// <summary>
    /// Updates motor loop timing
    /// </summary>
    private void UpdateMotorLoop()
    {
        if (isAccelerating)
        {
            acceleratingTime += Time.deltaTime;
            
            if (acceleratingTime >= 2f && !loopStarted)
            {
                StartLoop();
            }
        }
    }

    /// <summary>
    /// Applies fade out effect when not accelerating
    /// </summary>
    private void ApplyFadeOut()
    {
        if (!isAccelerating && motorAudioSource.volume > 0.01f)
        {
            motorAudioSource.volume = Mathf.Lerp(motorAudioSource.volume, 0f, Time.deltaTime / fadeOutSpeed);
            
            if (motorAudioSource.volume < 0.01f)
            {
                motorAudioSource.Stop();
                motorAudioSource.volume = 0f;
            }
        }
    }

    /// <summary>
    /// Starts acceleration - plays startup sound
    /// </summary>
    private void StartAcceleration()
    {
        isAccelerating = true;
        acceleratingTime = 0f;
        loopStarted = false;
        
        if (startupSound != null && effectsAudioSource != null)
        {
            effectsAudioSource.PlayOneShot(startupSound, startupVolume);
        }
    }

    /// <summary>
    /// Stops acceleration - starts fade out
    /// </summary>
    private void StopAcceleration()
    {
        isAccelerating = false;
        loopStarted = false;
        acceleratingTime = 0f;
    }

    /// <summary>
    /// Starts motor loop after 2 seconds
    /// </summary>
    private void StartLoop()
    {
        if (loopSound != null && motorAudioSource != null)
        {
            loopStarted = true;
            motorAudioSource.clip = loopSound;
            motorAudioSource.volume = loopVolume;
            motorAudioSource.Play();
        }
    }

    /// <summary>
    /// Detects collisions and plays appropriate sounds
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        float impactVelocity = collision.relativeVelocity.magnitude;

        if (impactVelocity >= minCollisionVelocity && effectsAudioSource != null)
        {
            Vector3 contactNormal = collision.GetContact(0).normal;
            float angle = Vector3.Angle(transform.forward, -contactNormal);
            
            AudioClip collisionSound = null;
            
            if (angle < 45f || angle > 135f)
            {
                collisionSound = frontCollisionSound;
            }
            else
            {
                collisionSound = sideCollisionSound;
            }
            
            if (collisionSound != null)
            {
                effectsAudioSource.PlayOneShot(collisionSound, collisionVolume);
            }
        }
    }

    /// <summary>
    /// Plays click sound - called by WheelController when changing steering type
    /// </summary>
    public void PlayClick()
    {
        if (clickSound != null && effectsAudioSource != null)
        {
            effectsAudioSource.PlayOneShot(clickSound, 0.5f);
        }
    }
}