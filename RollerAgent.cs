using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

/// <summary>
/// Kinematic movement model for ML-Agents roller ball.
/// Implements acceleration, drag, and speed limit.
/// </summary>
public class RollerAgent : Agent
{
    [Header("Kinematic Movement")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float accelerationRate = 20f;
    [SerializeField] private float drag = 5f;
    [SerializeField] private Vector3 startPosition = new Vector3(0f, 0.5f, 0f);

    private Rigidbody rb;
    private const float ACTION_EPSILON = 0.02f;   // Minimum action magnitude to consider as non-zero

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("RollerAgent: Rigidbody component is missing!");
        }
    }

    public override void OnEpisodeBegin()
    {
        // Reset physical state
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = startPosition;
        
        // If you have additional reset logic (like collected cubes counter), call it here
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Read continuous actions and clamp to [-1,1] (safety)
        float actionX = Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f);
        float actionZ = Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, 1f);
        
        Vector3 action = new Vector3(actionX, 0f, actionZ);
        
        // Normalize action only if it has meaningful magnitude
        if (action.magnitude > ACTION_EPSILON)
            action = action.normalized;
        
        // Desired velocity in world coordinates
        Vector3 desiredVelocity = action * maxSpeed;
        
        // Compute force using proportional controller (P-regulator)
        Vector3 force = (desiredVelocity - rb.velocity) * accelerationRate * rb.mass;
        
        // Safety check for invalid force
        if (float.IsNaN(force.x) || float.IsNaN(force.y) || float.IsNaN(force.z))
        {
            Debug.LogWarning("RollerAgent: force contains NaN, skipping AddForce");
            return;
        }
        
        // Apply force to Rigidbody (respects collisions and physics materials)
        rb.AddForce(force, ForceMode.Force);
        
        // Apply linear drag (air resistance / friction)
        rb.velocity *= (1f - drag * Time.fixedDeltaTime);
        
        // Clamp speed to maximum allowed value
        rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxSpeed);
        
        // Existing reward logic (step penalty, etc.) can be kept here
        // Example: AddReward(-0.01f);  // step penalty (optional)
    }

    /// <summary>
    /// Manual control for debugging and testing.
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
    }
    
    // NOTE: The following methods (CollectObservations, OnTriggerEnter, OnCollisionEnter)
    // should remain as they were in the original file. They are not shown here for brevity,
    // but you must keep your existing implementation of reward logic and observations.
}