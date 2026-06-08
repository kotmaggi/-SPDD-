using NUnit.Framework;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

/// <summary>
/// Unit tests for kinematic movement model in RollerAgent.
/// These tests run in Unity Test Framework (Edit Mode).
/// </summary>
public class KinematicModelTests
{
    private GameObject agentGameObject;
    private RollerAgent agent;
    private Rigidbody rb;

    [SetUp]
    public void Setup()
    {
        // Create a fresh GameObject with required components
        agentGameObject = new GameObject("TestAgent");
        agent = agentGameObject.AddComponent<RollerAgent>();
        rb = agentGameObject.AddComponent<Rigidbody>();
        
        // Manually initialize the agent (calls Initialize)
        agent.Initialize();
        
        // Simulate episode start to reset velocity/position
        agent.OnEpisodeBegin();
        
        // Override parameters for deterministic tests
        // (We'll set them via reflection or public fields; assuming they are public or serialized)
        SetPrivateField(agent, "maxSpeed", 10f);
        SetPrivateField(agent, "accelerationRate", 30f);
        SetPrivateField(agent, "drag", 5f);
        SetPrivateField(agent, "startPosition", Vector3.zero);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(agentGameObject);
    }

    [Test]
    public void Acceleration_ReachesMaxSpeed_WithinTolerance()
    {
        // Disable drag for this test
        SetPrivateField(agent, "drag", 0f);
        
        // Create a full forward action
        ActionBuffers actions = new ActionBuffers();
        actions.ContinuousActions.Array[0] = 1f;
        actions.ContinuousActions.Array[1] = 0f;
        
        // Simulate 100 fixed steps (2 seconds at 0.02 dt)
        for (int i = 0; i < 100; i++)
        {
            agent.OnActionReceived(actions);
        }
        
        float speed = rb.velocity.magnitude;
        float maxSpeed = GetPrivateField<float>(agent, "maxSpeed");
        
        Assert.LessOrEqual(speed, maxSpeed + 0.1f, "Speed exceeded maxSpeed");
        Assert.GreaterOrEqual(speed, maxSpeed * 0.9f, "Speed did not reach near maxSpeed");
    }
    
    [Test]
    public void Drag_ReducesVelocityExponentially()
    {
        // Set initial velocity manually
        rb.velocity = new Vector3(8f, 0f, 0f);
        
        // Zero action
        ActionBuffers zeroAction = new ActionBuffers();
        zeroAction.ContinuousActions.Array[0] = 0f;
        zeroAction.ContinuousActions.Array[1] = 0f;
        
        float drag = GetPrivateField<float>(agent, "drag");
        float dt = Time.fixedDeltaTime;
        // Expected factor per frame: (1 - drag * dt)
        float factor = 1f - drag * dt;
        // After 50 frames (1 second), theoretical speed = 8 * factor^50
        // factor^50 for drag=5, dt=0.02 -> factor=0.9 -> 0.9^50 ≈ 0.00515 => ~0.041 m/s
        int frames = 50;
        for (int i = 0; i < frames; i++)
        {
            agent.OnActionReceived(zeroAction);
        }
        
        Assert.Less(rb.velocity.magnitude, 0.2f, "Velocity did not decay enough");
    }
    
    [Test]
    public void SpeedIsClamped_ToMaxSpeed()
    {
        SetPrivateField(agent, "maxSpeed", 5f);
        SetPrivateField(agent, "drag", 0f);
        SetPrivateField(agent, "accelerationRate", 100f); // very high acceleration
        
        ActionBuffers fullAction = new ActionBuffers();
        fullAction.ContinuousActions.Array[0] = 1f;
        fullAction.ContinuousActions.Array[1] = 0f;
        
        // Run many steps to ensure speed would exceed maxSpeed if not clamped
        for (int i = 0; i < 200; i++)
        {
            agent.OnActionReceived(fullAction);
            Assert.LessOrEqual(rb.velocity.magnitude, 5f + 0.05f, "Clamping failed at step " + i);
        }
    }
    
    [Test]
    public void ZeroAction_ResultsInDeceleration()
    {
        // First accelerate to some speed
        SetPrivateField(agent, "drag", 0f);
        ActionBuffers fullForward = new ActionBuffers();
        fullForward.ContinuousActions.Array[0] = 1f;
        fullForward.ContinuousActions.Array[1] = 0f;
        
        for (int i = 0; i < 50; i++)
            agent.OnActionReceived(fullForward);
        
        float speedBefore = rb.velocity.magnitude;
        Assert.Greater(speedBefore, 5f, "Did not accelerate enough");
        
        // Now set drag and send zero action
        SetPrivateField(agent, "drag", 5f);
        ActionBuffers zeroAction = new ActionBuffers();
        zeroAction.ContinuousActions.Array[0] = 0f;
        zeroAction.ContinuousActions.Array[1] = 0f;
        
        for (int i = 0; i < 30; i++)
            agent.OnActionReceived(zeroAction);
        
        float speedAfter = rb.velocity.magnitude;
        Assert.Less(speedAfter, speedBefore * 0.5f, "Deceleration too weak");
    }
    
    [Test]
    public void ActionNormalization_PreservesDirectionForSmallInput()
    {
        // Very small action (should be considered zero due to ACTION_EPSILON)
        ActionBuffers smallAction = new ActionBuffers();
        smallAction.ContinuousActions.Array[0] = 0.005f;
        smallAction.ContinuousActions.Array[1] = 0f;
        
        SetPrivateField(agent, "drag", 0f);
        agent.OnActionReceived(smallAction);
        
        // Force should be near zero, so velocity remains ~0
        Assert.Less(rb.velocity.magnitude, 0.05f, "Small action caused unintended movement");
        
        // Action just above epsilon
        float eps = GetPrivateField<float>(agent, "ACTION_EPSILON");
        ActionBuffers thresholdAction = new ActionBuffers();
        thresholdAction.ContinuousActions.Array[0] = eps + 0.01f;
        thresholdAction.ContinuousActions.Array[1] = 0f;
        
        for (int i = 0; i < 10; i++)
            agent.OnActionReceived(thresholdAction);
        
        Assert.Greater(rb.velocity.magnitude, 0.1f, "Action above epsilon did not produce movement");
    }
    
    // Helper methods to access private fields (since we use [SerializeField] private)
    private void SetPrivateField<T>(object obj, string fieldName, T value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(obj, value);
        else
            Debug.LogError($"Field {fieldName} not found");
    }
    
    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            return (T)field.GetValue(obj);
        else
            throw new System.Exception($"Field {fieldName} not found");
    }
}