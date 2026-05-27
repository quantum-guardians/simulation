using UnityEngine;

/// <summary>
/// Parameters for the social force model (Helbing, Farkas, Vicsek, 2000).
/// Serializable so it can be tweaked in the Unity Inspector.
/// </summary>
[System.Serializable]
public class SocialForceParameters
{
    [Header("Agent Physics")]
    [Tooltip("Default mass in kg.")]
    public float Mass = 80f;

    [Tooltip("Relaxation time tau in seconds. Lower = faster acceleration toward desired velocity.")]
    public float RelaxationTime = 0.5f;

    [Header("Forces")] [Tooltip("Social repulsion strength A (N). Paper 2000; lowered for narrow roads.")]
    public float SocialRepulsionStrength = 0;//20f;

    [Tooltip("Social repulsion range B (m). Paper 0.08; widened for softer decay on narrow roads.")]
    public float SocialRepulsionRange = 0.5f;

    [Tooltip("Body force stiffness k (kg/s^2). Contact spring constant that prevents overlap.")]
    public float BodyForceStiffness = 1.2e5f;

    [Tooltip("Sliding friction coefficient kappa (kg/(m*s)). Resists tangential motion during body contact.")]
    public float SlidingFriction = 2.4e5f;

    [Header("Spatial Hash")]
    [Tooltip("Cell size for agent-agent neighbor search. Should be >= max(2*radius + 3*B, 1.0m).")]
    public float NeighborSearchRadius = 1.0f;

    [Header("Integration Safety")]
    [Tooltip("Maximum speed clamp (m/s). Prevents numeric blowup from deep overlaps.")]
    public float MaxSpeed = 3.0f;

    [Tooltip("Maximum acceleration clamp (m/s^2).")]
    public float MaxAcceleration = 80f;

    [Tooltip("Maximum distance (m) at which wall forces are considered.")]
    public float WallForceDistance = 0.8f;

    [Header("Injury")]
    [Tooltip("Contact pressure threshold (N/m) above which injury accumulates.")]
    public float InjuryPressureThreshold = 1600f;

    [Tooltip("Duration (s) above threshold before agent becomes injured.")]
    public float InjuryPressureDuration = 0.25f;

    public float SafeRelaxationTime => Mathf.Max(0.01f, RelaxationTime);
    public float SafeSocialRepulsionRange => Mathf.Max(0.001f, SocialRepulsionRange);
    public float SafeNeighborSearchRadius => Mathf.Max(0.1f, NeighborSearchRadius);
}
