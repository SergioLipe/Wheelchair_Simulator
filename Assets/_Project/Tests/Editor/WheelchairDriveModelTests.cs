using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Testes do modelo de rotacao (tracao diferencial).
/// </summary>
public class WheelchairDriveModelTests
{
    private const float MaxSpeed = 8f / 3.6f;
    private const float Base = 60f;

    private static float Rate(float h, float v, float speed, float highSpeedFactor = 0.5f) =>
        WheelchairDriveModel.TargetTurnRate(h, v, speed, MaxSpeed, Base, highSpeedFactor);

    [Test]
    public void Stationary_FullSideways_TurnsInPlace()
    {
        Assert.AreEqual(Base, Rate(1f, 0f, 0f), 0.001f, "Uma cadeira eletrica real roda sobre si propria");
        Assert.AreEqual(-Base, Rate(-1f, 0f, 0f), 0.001f);
    }

    [Test]
    public void TurnRate_DecreasesWithSpeed()
    {
        float slow = Rate(1f, 1f, 0.3f);
        float fast = Rate(1f, 1f, MaxSpeed);
        Assert.Less(fast, slow);
        Assert.AreEqual(Base * 0.5f, fast, 0.001f);
    }

    [Test]
    public void TurnRate_IsContinuousAroundZeroSpeed()
    {
        // O modelo antigo saltava de 0 para ~90 graus/s quando a velocidade passava 0.1 m/s
        float a = Rate(1f, 1f, 0.09f);
        float b = Rate(1f, 1f, 0.11f);
        Assert.Less(Mathf.Abs(a - b), 1f);
    }

    [Test]
    public void Reversing_InvertsTurnDirection()
    {
        Assert.Greater(Rate(1f, 1f, 0.5f), 0f);
        Assert.Less(Rate(1f, -1f, -0.5f), 0f);
    }

    [Test]
    public void Stationary_JoystickBack_UsesReverseDirection()
    {
        // Parado mas a puxar para tras: o sentido segue a intencao (evita trocar quando a velocidade passa por 0)
        Assert.Less(Rate(1f, -1f, 0f), 0f);
    }

    [Test]
    public void FrontSteering_PivotBehind_TurningRight_MovesOriginRight()
    {
        // Pivot no eixo traseiro (30 cm atras): a frente varre para a direita
        Vector3 d = WheelchairDriveModel.PivotDisplacement(Quaternion.identity, -0.3f, 10f);
        Assert.Greater(d.x, 0f);
        Assert.AreEqual(0f, d.y, 1e-6f);
    }

    [Test]
    public void RearSteering_PivotAhead_TurningRight_MovesOriginLeft()
    {
        // Pivot no eixo dianteiro: a traseira varre para o lado contrario
        Vector3 d = WheelchairDriveModel.PivotDisplacement(Quaternion.identity, 0.3f, 10f);
        Assert.Less(d.x, 0f);
    }

    [Test]
    public void PivotDisplacement_KeepsDistanceToPivot_AndFullTurnReturnsToStart()
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.Euler(0f, 37f, 0f);
        Vector3 pivot = pos + rot * new Vector3(0f, 0f, -0.3f);

        for (int i = 0; i < 36; i++)
        {
            pos += WheelchairDriveModel.PivotDisplacement(rot, -0.3f, 10f);
            rot = rot * Quaternion.Euler(0f, 10f, 0f);
            Assert.AreEqual(0.3f, Vector3.Distance(pos, pivot), 1e-4f, "O eixo motor fica no mesmo sitio");
        }
        Assert.AreEqual(0f, pos.magnitude, 1e-4f);
    }

    [Test]
    public void NoPivotOffset_NoDisplacement()
    {
        Assert.AreEqual(Vector3.zero, WheelchairDriveModel.PivotDisplacement(Quaternion.identity, 0f, 30f));
    }

    [Test]
    public void ComputePivot_UsesRearAxleForFrontSteering_FrontAxleForRearSteering()
    {
        GameObject chair = new GameObject("Chair");
        try
        {
            WheelController wc = chair.AddComponent<WheelController>();
            wc.joint6_FrontLeftWheel = Child(chair, -0.3f, 0.25f);
            wc.joint7_FrontRightWheel = Child(chair, 0.3f, 0.25f);
            wc.joint8_RearLeftWheel = Child(chair, -0.3f, -0.35f);
            wc.joint9_RearRightWheel = Child(chair, 0.3f, -0.35f);
            var settings = new WheelchairTurnSettings();

            Assert.AreEqual(-0.35f, WheelchairDriveModel.ComputePivotLocalZ(chair.transform, wc, WheelController.SteeringType.FrontSteering, settings), 1e-4f);
            Assert.AreEqual(0.25f, WheelchairDriveModel.ComputePivotLocalZ(chair.transform, wc, WheelController.SteeringType.RearSteering, settings), 1e-4f);

            settings.pivotAroundDriveAxle = false;
            Assert.AreEqual(0f, WheelchairDriveModel.ComputePivotLocalZ(chair.transform, wc, WheelController.SteeringType.FrontSteering, settings));
        }
        finally
        {
            Object.DestroyImmediate(chair);
        }
    }

    [Test]
    public void ComputePivot_MissingWheels_UsesFallback()
    {
        var settings = new WheelchairTurnSettings { fallbackAxleOffset = 0.3f };
        Assert.AreEqual(-0.3f, WheelchairDriveModel.ComputePivotLocalZ(null, null, WheelController.SteeringType.FrontSteering, settings), 1e-6f);
        Assert.AreEqual(0.3f, WheelchairDriveModel.ComputePivotLocalZ(null, null, WheelController.SteeringType.RearSteering, settings), 1e-6f);
    }

    [Test]
    public void LegacyModel_MatchesOldBehaviour()
    {
        Assert.AreEqual(0f, WheelchairDriveModel.LegacyRotationMultiplier(true, 0f, MaxSpeed, false), "Antes: tracao traseira nao rodava parada");
        Assert.AreEqual(0f, WheelchairDriveModel.LegacyRotationMultiplier(false, 0f, MaxSpeed, false));
        Assert.AreEqual(1.5f, WheelchairDriveModel.LegacyRotationMultiplier(false, 0f, MaxSpeed, true));
        Assert.AreEqual(1.8f, WheelchairDriveModel.LegacyRotationMultiplier(false, MaxSpeed, MaxSpeed, false), 1e-4f);
    }

    private static Transform Child(GameObject parent, float x, float z)
    {
        Transform t = new GameObject("wheel").transform;
        t.SetParent(parent.transform, false);
        t.localPosition = new Vector3(x, 0.2f, z);
        return t;
    }
}
