using UnityEngine;

/// <summary>
/// Definicoes do modelo de rotacao (no Inspector do MovementPC / MovementVR).
/// </summary>
[System.Serializable]
public class WheelchairTurnSettings
{
    [Tooltip("Modelo realista de cadeira eletrica (tracao diferencial): roda parada sobre si propria, " +
             "a rotacao abranda com a velocidade e roda a volta do eixo motor. Desligar = modelo antigo.")]
    public bool realisticModel = true;

    [Tooltip("Rotacao com o joystick todo para o lado e a cadeira parada (graus/s). Usa o 'rotationSpeed' se for 0.")]
    public float inPlaceTurnRate = 0f;

    [Tooltip("Fracao da rotacao que sobra a velocidade maxima (os controladores reais reduzem a rotacao " +
             "a alta velocidade para a cadeira nao virar). 1 = igual parado e em andamento.")]
    [Range(0.2f, 1f)] public float highSpeedTurnFactor = 0.5f;

    [Tooltip("Rodar a volta do eixo das rodas motoras em vez do centro da cadeira. " +
             "Direcao frontal: a frente varre (eixo traseiro). Direcao traseira: a traseira varre (eixo dianteiro).")]
    public bool pivotAroundDriveAxle = true;

    [Tooltip("Distancia (m) do centro da cadeira ao eixo, usada se nao for possivel medir pelas rodas do modelo")]
    public float fallbackAxleOffset = 0.3f;
}

/// <summary>
/// Modelo de rotacao da cadeira, partilhado pelo MovementPC e pelo MovementVR
/// (antes o codigo de rotacao estava copiado nos dois).
///
/// Modelo realista (tracao diferencial com joystick):
///  - Roda sobre si propria com a cadeira parada (como uma cadeira eletrica real).
///  - A velocidade de rotacao diminui com a velocidade de marcha.
///  - Em marcha-atras a rotacao inverte (puxar o joystick para tras-direita leva a traseira para a direita).
///  - Roda a volta do eixo das rodas motoras: a diferenca real entre tracao traseira e dianteira
///    e qual das pontas da cadeira "varre" nas curvas.
/// </summary>
public static class WheelchairDriveModel
{
    /// <summary>Velocidade de rotacao pretendida (graus/s, positivo = direita).</summary>
    public static float TargetTurnRate(float horizontalInput, float verticalInput, float currentSpeed,
                                       float maxSpeed, float baseTurnRate, float highSpeedTurnFactor)
    {
        float speed01 = maxSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxSpeed) : 0f;
        float rate = horizontalInput * baseTurnRate * Mathf.Lerp(1f, highSpeedTurnFactor, speed01);

        // Parado, o sentido vem do joystick (evita trocar de sentido quando a velocidade passa por zero)
        bool reversing = currentSpeed < -0.05f || (Mathf.Abs(currentSpeed) <= 0.05f && verticalInput < -0.1f);
        return reversing ? -rate : rate;
    }

    /// <summary>
    /// Deslocamento da origem da cadeira ao rodar deltaYawDeg a volta de um ponto no eixo
    /// local Z (pivotLocalZ). Deve ser somado ao CharacterController.Move para respeitar colisoes.
    /// </summary>
    public static Vector3 PivotDisplacement(Quaternion rotation, float pivotLocalZ, float deltaYawDeg)
    {
        if (Mathf.Approximately(pivotLocalZ, 0f) || Mathf.Approximately(deltaYawDeg, 0f)) return Vector3.zero;

        Vector3 forward = rotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) return Vector3.zero;
        forward.Normalize();

        Vector3 originFromPivot = -forward * pivotLocalZ;
        Vector3 rotated = Quaternion.Euler(0f, deltaYawDeg, 0f) * originFromPivot;
        return rotated - originFromPivot;
    }

    /// <summary>
    /// Posicao (Z local) do eixo a volta do qual a cadeira roda.
    /// Direcao frontal -> eixo traseiro; direcao traseira -> eixo dianteiro. Medido nas rodas do modelo.
    /// </summary>
    public static float ComputePivotLocalZ(Transform chair, WheelController wheels, WheelController.SteeringType steering,
                                           WheelchairTurnSettings settings)
    {
        if (settings == null || !settings.pivotAroundDriveAxle) return 0f;

        bool frontSteering = steering == WheelController.SteeringType.FrontSteering;
        float fallback = frontSteering ? -settings.fallbackAxleOffset : settings.fallbackAxleOffset;
        if (wheels == null || chair == null) return fallback;

        Transform a = frontSteering ? wheels.joint8_RearLeftWheel : wheels.joint6_FrontLeftWheel;
        Transform b = frontSteering ? wheels.joint9_RearRightWheel : wheels.joint7_FrontRightWheel;
        if (a == null || b == null) return fallback;

        Vector3 mid = (a.position + b.position) * 0.5f;
        float z = chair.InverseTransformPoint(mid).z * chair.lossyScale.z;

        // Valores absurdos (modelo mal configurado) -> usar o valor por defeito
        if (Mathf.Abs(z) > 1f || (frontSteering ? z > 0f : z < 0f)) return fallback;
        return z;
    }

    /// <summary>
    /// Modelo antigo (antes desta versao), mantido para comparar: multiplicador da rotacao.
    /// </summary>
    public static float LegacyRotationMultiplier(bool isRearSteering, float currentSpeed, float maxSpeedNormal, bool rotationInPlace)
    {
        bool isStationary = Mathf.Abs(currentSpeed) < 0.1f;
        float normalizedSpeed = maxSpeedNormal > 0f ? Mathf.Abs(currentSpeed) / maxSpeedNormal : 0f;

        if (isRearSteering)
        {
            if (isStationary) return 0f;
            float multiplier = 2.5f * Mathf.Lerp(0.2f, 1f, normalizedSpeed);
            return currentSpeed < 0 ? multiplier * -0.8f : multiplier;
        }

        if (isStationary) return rotationInPlace ? 1.5f : 0f;
        float m = 1f + normalizedSpeed * 0.8f;
        return currentSpeed < 0 ? -m : m;
    }
}
