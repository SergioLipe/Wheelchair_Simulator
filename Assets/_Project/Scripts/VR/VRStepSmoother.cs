using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// [CONFORTO VR] Suaviza na vista as mudancas bruscas de altura da cadeira.
/// O CharacterController sobe degraus/passeios (stepOffset) num unico frame, e a camara
/// "salta" com ele - um dos gatilhos classicos de enjoo em VR.
/// Este script desloca temporariamente o Camera Offset do XR Origin no sentido contrario
/// e devolve-o a zero de forma suave. A fisica da cadeira nao e alterada.
/// Adicionado automaticamente pelo MovementVR (opcao "smoothStepUps").
/// </summary>
[DisallowMultipleComponent]
public class VRStepSmoother : MonoBehaviour
{
    [Tooltip("Mudanca de altura da cadeira num so frame (m) a partir da qual conta como degrau. Rampas ficam bem abaixo disto.")]
    public float stepThreshold = 0.015f;

    [Tooltip("Tempo (s) que a vista demora a apanhar a cadeira depois de um degrau")]
    public float smoothTime = 0.12f;

    [Tooltip("Deslocamento maximo da vista (m)")]
    public float maxOffset = 0.15f;

    [Tooltip("Mudancas maiores que isto (m) sao teletransportes/resets e nao sao suavizadas")]
    public float teleportThreshold = 0.5f;

    private Transform target;
    private float lastY;
    private float offset;          // em metros (mundo)
    private float appliedLocal;    // o que foi somado ao localPosition.y no frame anterior
    private float velocity;

    private void Start()
    {
        XROrigin origin = GetComponentInChildren<XROrigin>(true);
        if (origin != null)
        {
            target = origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform
                : origin.transform;
        }

        if (target == null || target == transform || !target.IsChildOf(transform))
        {
            Debug.LogWarning("[VRStepSmoother] XR Origin não encontrado dentro da cadeira. Suavização de degraus desativada.");
            enabled = false;
            return;
        }

        lastY = transform.position.y;
    }

    private void LateUpdate()
    {
        float y = transform.position.y;
        float dy = y - lastY;
        lastY = y;

        float absDy = Mathf.Abs(dy);
        if (absDy > stepThreshold && absDy < teleportThreshold)
        {
            // Mantem a vista onde estava e deixa-a seguir a cadeira suavemente
            offset = Mathf.Clamp(offset - dy, -maxOffset, maxOffset);
        }

        offset = Mathf.SmoothDamp(offset, 0f, ref velocity, smoothTime);
        if (Mathf.Abs(offset) < 0.0005f && Mathf.Abs(velocity) < 0.001f)
        {
            offset = 0f;
            velocity = 0f;
        }

        // Converter metros do mundo para o espaco local do pai (caso o rig tenha escala)
        float parentScaleY = target.parent != null ? target.parent.lossyScale.y : 1f;
        float local = offset / Mathf.Max(Mathf.Abs(parentScaleY), 0.0001f);

        // Aplicacao aditiva: so mexe na diferenca, para nao apagar ajustes de outros scripts
        // (XR Origin / calibracao do assento) feitos ao Camera Offset.
        if (!Mathf.Approximately(local, appliedLocal))
        {
            Vector3 p = target.localPosition;
            p.y += local - appliedLocal;
            target.localPosition = p;
            appliedLocal = local;
        }
    }

    private void OnDisable()
    {
        // Devolver o Camera Offset ao valor original
        if (target != null && appliedLocal != 0f)
        {
            Vector3 p = target.localPosition;
            p.y -= appliedLocal;
            target.localPosition = p;
        }
        appliedLocal = 0f;
        offset = 0f;
        velocity = 0f;
    }

    private void OnEnable()
    {
        lastY = transform.position.y;
    }
}
