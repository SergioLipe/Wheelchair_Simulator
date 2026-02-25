using UnityEngine;
using System.Collections; // Required to use Coroutines (timers)

public class WalkAndTurn : MonoBehaviour
{
    [Tooltip("Time in seconds the character walks before turning.")]
    public float walkDuration = 5.0f; 
    
    [Tooltip("Time it takes for the turn animation to finish. (1.5s is safe for Mixamo)")]
    public float turnAnimationTime = 1.5f;

    private float timer;
    private Animator anim;
    
    // We will store the mathematically perfect angle here
    private float perfectRotationY;

    void Start()
    {
        anim = GetComponent<Animator>();
        timer = walkDuration;
        
        // Save the exact starting rotation to use as our baseline
        perfectRotationY = transform.eulerAngles.y;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            // 1. Trigger the animation
            anim.SetTrigger("shouldTurn");
            
            // 2. Calculate the next perfect 180-degree angle
            perfectRotationY += 180f;
            
            // 3. Start a routine to wait and then snap the rotation perfectly
            StartCoroutine(SnapRotation());
            
            // 4. Reset the timer (we add the turn time so it doesn't count while turning)
            timer = walkDuration + turnAnimationTime; 
        }
    }

    // A Coroutine is a function that can pause its own execution
    private IEnumerator SnapRotation()
    {
        // Wait exactly the amount of time the turn animation takes
        yield return new WaitForSeconds(turnAnimationTime);
        
        // The animation is done. Now, force the character into the mathematically perfect angle.
        // This completely eliminates "Mixamo Drift".
        transform.rotation = Quaternion.Euler(0, perfectRotationY, 0);
    }
}