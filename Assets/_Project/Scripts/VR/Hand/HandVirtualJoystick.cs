using UnityEngine;

/// <summary>
/// Joystick virtual controlado por hand tracking.
/// Comporta-se como o manípulo físico no apoio de braço da cadeira.
/// Output é um Vector2 com a mesma forma de um thumbstick (-1..1).
/// </summary>
public class HandVirtualJoystick : MonoBehaviour
{
    [Header("=== Referências ===")]
    [Tooltip("Transform da mão (ex: RightWhiteHand). Tem de seguir a mão real via hand tracking.")]
    public Transform handTransform;

    [Tooltip("Ponto âncora do joystick. Coloca como filho da cadeira, no sítio do apoio de braço.")]
    public Transform joystickAnchor;

    [Tooltip("Opcional: visual de uma alavanca que se inclina conforme a mão empurra.")]
    public Transform stickVisual;

    [Header("=== Ativação (agarrar/largar) ===")]
    [Tooltip("Distância à âncora a partir da qual a mão 'agarra' o joystick.")]
    public float grabRadius = 0.12f;

    [Tooltip("Distância de libertação (maior que a de agarrar para evitar flicker).")]
    public float releaseRadius = 0.20f;

    [Tooltip("Se ativo, ignora a distância e está sempre ligado. Útil para testar sem hand tracking.")]
    public bool alwaysActive = false;

    [Header("=== Geometria do joystick ===")]
    [Tooltip("Deslocamento máximo da mão (em metros) que mapeia para input máximo. ~10cm é natural.")]
    public float maxOffset = 0.10f;

    [Tooltip("Inclinação visual da alavanca no offset máximo (graus).")]
    public float maxStickTilt = 30f;

    [Header("=== Segurança ===")]
    [Tooltip("Só agarra se, nesse momento, a mão estiver perto do centro (input abaixo deste valor). " +
             "Evita que a cadeira arranque de repente quando a mão entra na zona de agarrar descentrada.")]
    [Range(0.05f, 1f)]
    public float maxInputToEngage = 0.3f;

    [Tooltip("Se a pose da mão não mudar nada durante este tempo (s), considera-se tracking perdido " +
             "(a pose fica congelada quando o tracking falha).")]
    public float staleHandTime = 0.2f;

    [Tooltip("Opcional: tracker da mão. Se vazio, é procurado no handTransform.")]
    public SimpleHandTracker handTracker;

    [Header("=== Debug ===")]
    [SerializeField] private Vector2 output = Vector2.zero;
    [SerializeField] private bool isGrabbing = false;

    public Vector2 Output => output;
    public bool IsActive => isGrabbing || alwaysActive;

    private Vector3 lastHandPos;
    private float unchangedTime;

    void Start()
    {
        if (handTracker == null && handTransform != null)
            handTracker = handTransform.GetComponentInParent<SimpleHandTracker>();
    }

    /// <summary>
    /// False se a mão deixou de ser seguida. Nesse caso a pose do handTransform fica congelada e,
    /// sem esta verificação, a cadeira continuava a andar sozinha com o último input.
    /// </summary>
    private bool IsHandReliable()
    {
        if (!handTransform.gameObject.activeInHierarchy) return false;
        if (handTracker != null && handTracker.isActiveAndEnabled && !handTracker.IsTracked) return false;

        Vector3 p = handTransform.position;
        bool unchanged = p == lastHandPos;
        lastHandPos = p;

        if (!unchanged)
        {
            unchangedTime = 0f;
            return true;
        }

        unchangedTime += Time.unscaledDeltaTime;
        return unchangedTime < staleHandTime;
    }

    private Vector2 ComputeInput()
    {
        Vector3 worldOffset = handTransform.position - joystickAnchor.position;
        float forwardAmount = Vector3.Dot(worldOffset, joystickAnchor.forward);
        float rightAmount   = Vector3.Dot(worldOffset, joystickAnchor.right);
        return new Vector2(
            Mathf.Clamp(rightAmount / maxOffset, -1f, 1f),
            Mathf.Clamp(forwardAmount / maxOffset, -1f, 1f));
    }

    void Update()
{
    if (handTransform == null || joystickAnchor == null)
    {
        output = Vector2.zero;
        return;
    }

    // Histerese de agarrar/largar (distância real no mundo)
    float dist = Vector3.Distance(handTransform.position, joystickAnchor.position);

    if (alwaysActive)
        isGrabbing = true;
    else if (!IsHandReliable())
        isGrabbing = false;
    else if (!isGrabbing && dist <= grabRadius && ComputeInput().magnitude <= maxInputToEngage)
        isGrabbing = true;
    else if (isGrabbing && dist > releaseRadius)
        isGrabbing = false;

    if (!isGrabbing)
    {
        output = Vector2.zero;
        if (stickVisual != null)
            stickVisual.localRotation = Quaternion.Slerp(
                stickVisual.localRotation, Quaternion.identity, Time.deltaTime * 10f);
        return;
    }

    // Offset em WORLD space (metros reais), imune a scale
    Vector3 worldOffset = handTransform.position - joystickAnchor.position;

    // Projetar em eixos do pivot (sem escala) usando os seus vetores direcionais
    float forwardAmount = Vector3.Dot(worldOffset, joystickAnchor.forward);
    float rightAmount   = Vector3.Dot(worldOffset, joystickAnchor.right);

    float x = Mathf.Clamp(rightAmount / maxOffset, -1f, 1f);
    float y = Mathf.Clamp(forwardAmount / maxOffset, -1f, 1f);

    output = new Vector2(x, y);

    // Feedback visual: a alavanca inclina-se
    if (stickVisual != null)
    {
        float tiltX = -y * maxStickTilt;
        float tiltZ = -x * maxStickTilt;
        stickVisual.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
    }
}

    // Desenha os raios no editor para alinhares a âncora visualmente
    void OnDrawGizmosSelected()
    {
        if (joystickAnchor == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(joystickAnchor.position, grabRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(joystickAnchor.position, releaseRadius);
        Gizmos.color = Color.cyan;
        Gizmos.matrix = joystickAnchor.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(maxOffset * 2, 0.01f, maxOffset * 2));
    }
}