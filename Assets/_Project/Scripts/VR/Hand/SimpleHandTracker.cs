using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using System.Collections.Generic;

/// <summary>
/// Faz um Transform seguir o pulso de uma mão tracked pelo XR Hands.
/// Coloca este componente no RightHandModel (ou num GameObject pai do visual).
/// </summary>
public class SimpleHandTracker : MonoBehaviour
{
    public enum Handedness { Left, Right }

    [Tooltip("Qual mão seguir")]
    public Handedness handedness = Handedness.Right;

    [Tooltip("Esconder o visual quando a mão não está tracked")]
    public bool hideWhenNotTracked = true;

    [Tooltip("Objeto visual a esconder (se vazio, usa este GameObject)")]
    public GameObject visualRoot;

    private XRHandSubsystem handSubsystem;

    /// <summary>True enquanto a mao esta a ser seguida. Quando false, a pose deste Transform esta congelada.</summary>
    public bool IsTracked { get; private set; }

    // Sem visualRoot, esconde so os renderers: desativar o proprio GameObject parava este Update
    // e a mao nunca mais voltava a aparecer.
    private Renderer[] ownRenderers;
    private bool rendererVisible = true;

    void OnEnable()
    {
        TryGetSubsystem();
    }

    void OnDisable()
    {
        IsTracked = false;
    }

    private void SetVisible(bool visible)
    {
        if (visualRoot != null && visualRoot != gameObject)
        {
            if (visualRoot.activeSelf != visible) visualRoot.SetActive(visible);
            return;
        }

        if (rendererVisible == visible) return;
        if (ownRenderers == null) ownRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in ownRenderers)
        {
            if (r != null) r.enabled = visible;
        }
        rendererVisible = visible;
    }

    void TryGetSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            handSubsystem = subsystems[0];
    }

    void Update()
    {
        if (handSubsystem == null)
        {
            TryGetSubsystem();
            if (handSubsystem == null) return;
        }

        XRHand hand = (handedness == Handedness.Right)
            ? handSubsystem.rightHand
            : handSubsystem.leftHand;

        bool tracked = hand.isTracked;
        IsTracked = tracked;

        if (!tracked)
        {
            if (hideWhenNotTracked) SetVisible(false);
            return;
        }

        if (hideWhenNotTracked) SetVisible(true);

        // Pose do pulso no espaço do XR Origin
        var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
        if (wristJoint.TryGetPose(out Pose pose))
        {
            // Aplicar como local em relação ao XR Origin (pai)
            transform.localPosition = pose.position;
            transform.localRotation = pose.rotation;
        }
    }
}