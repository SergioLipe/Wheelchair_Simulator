using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Testes de seguranca do joystick por hand tracking.
/// </summary>
public class HandVirtualJoystickTests
{
    private GameObject root;
    private Transform anchor;
    private Transform hand;
    private HandVirtualJoystick joystick;

    private static readonly MethodInfo UpdateMethod =
        typeof(HandVirtualJoystick).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("JoystickTest");
        anchor = new GameObject("Anchor").transform;
        anchor.SetParent(root.transform, false);
        hand = new GameObject("Hand").transform;
        hand.SetParent(root.transform, false);

        joystick = root.AddComponent<HandVirtualJoystick>();
        joystick.joystickAnchor = anchor;
        joystick.handTransform = hand;
        joystick.grabRadius = 0.12f;
        joystick.releaseRadius = 0.20f;
        joystick.maxOffset = 0.10f;
        joystick.maxInputToEngage = 0.3f;
        joystick.staleHandTime = 10f; // em EditMode o tempo nao avanca de forma fiavel
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    // Move ligeiramente a mao em cada frame, como faz uma mao real (nunca fica exatamente parada)
    private int frame;
    private void Step(Vector3 handLocalPos)
    {
        frame++;
        hand.localPosition = handLocalPos + new Vector3(0f, frame * 1e-5f, 0f);
        UpdateMethod.Invoke(joystick, null);
    }

    [Test]
    public void HandAtCentre_Grabs_ThenPushForward_GivesForwardInput()
    {
        Step(Vector3.zero);
        Assert.IsTrue(joystick.IsActive);

        Step(new Vector3(0f, 0f, 0.10f));
        Assert.AreEqual(1f, joystick.Output.y, 0.01f);
    }

    [Test]
    public void HandEntersGrabZoneOffCentre_DoesNotGrab()
    {
        // 11 cm a frente: dentro do raio de agarrar mas ja seria ~100% de acelerador
        Step(new Vector3(0f, 0f, 0.11f));
        Assert.IsFalse(joystick.IsActive, "A cadeira nao pode arrancar de repente ao agarrar descentrado");
        Assert.AreEqual(Vector2.zero, joystick.Output);
    }

    [Test]
    public void TrackingLost_HandDeactivated_ReleasesJoystick()
    {
        Step(Vector3.zero);
        Step(new Vector3(0f, 0f, 0.10f));
        Assert.Greater(joystick.Output.y, 0.9f);

        hand.gameObject.SetActive(false);
        UpdateMethod.Invoke(joystick, null);

        Assert.IsFalse(joystick.IsActive);
        Assert.AreEqual(Vector2.zero, joystick.Output, "Sem mao, a cadeira nao pode continuar a andar");
    }

    [Test]
    public void FrozenHandPose_ReleasesJoystick()
    {
        joystick.staleHandTime = 0f; // qualquer frame sem movimento conta como congelado

        Step(Vector3.zero);
        Step(new Vector3(0f, 0f, 0.10f));
        Assert.IsTrue(joystick.IsActive);

        // Pose exatamente igual (tracking perdido: o Transform deixa de ser atualizado)
        UpdateMethod.Invoke(joystick, null);

        Assert.IsFalse(joystick.IsActive);
        Assert.AreEqual(Vector2.zero, joystick.Output);
    }

    [Test]
    public void HandMovesAway_ReleasesJoystick()
    {
        Step(Vector3.zero);
        Step(new Vector3(0f, 0.25f, 0f));
        Assert.IsFalse(joystick.IsActive);
    }
}
