using Unity.VisualScripting;
using UnityEngine;


[CreateAssetMenu(menuName = "Player Movement")]
public class PlayerMovementStats : ScriptableObject
{
    [Header("Walk")]
    [Range(0f, 1f)] public float moveThreshold = 0.25f;
    [Range(1f, 100f)] public float maxWalkSpeed = 12.5f;
    [Range(0.25f, 50f)] public float groundAcceleration = 5f;
    [Range(0.25f, 50f)] public float groundDeceleration = 20f;
    [Range(0.25f, 50f)] public float airAcceleration = 5f;
    [Range(0.25f, 50f)] public float airDeceleration = 5f;
    [Range(0.25f, 50f)] public float wallJumpMoveAcceleration = 5f;
    [Range(0.25f, 50f)] public float wallJumpMoveDeceleration = 5f;


    [Header("Run")]
    [Range(1f, 100f)] public float maxRunSpeed = 20f;


    [Header("Grounded/Collision Checks")]
    public LayerMask GroundLayer;
    public float groundDetectionRayLength = 0.02f;
    public float headDetectionRayLength = 0.02f;
    [Range(0f, 1f)] public float headWidth = 0.75f;
    public float wallDetectionRayLength = 0.125f;
    [Range(0.01f, 2f)] public float wallDetectionRayHeightMultiplier = 0.9f;


    [Header("Jump")]
    public float jumpHeight = 6.5f;
    [Range(1f, 1.1f)] public float jumpHeightCompensationFactor = 1.054f;
    public float timeTillJumpApex = 0.35f;
    [Range(0.01f, 5f)] public float gravityOnReleaseMultiplier = 2f;
    public float maxFallSpeed = 26f;
    [Range(1, 5)] public int numberOfJumpsAllowed = 2;


    [Header("Reset Jump Options")]
    public bool resetJumpsOnWallSlide = true;


    [Header("Jump Cut")]
    [Range(0.02f, 0.3f)] public float timeForUpwardsCancel = 0.027f;


    [Header("Jump Apex")]
    [Range(0.5f, 1f)] public float apexThreshold = 0.97f;
    [Range(0.01f, 1f)] public float apexHangTime = 0.075f;


    [Header("Jump Buffer")]
    [Range(0f, 1f)] public float jumpBufferTime = 0.125f;


    [Header("Jump Coyote Time")]
    [Range(0f, 1f)] public float jumpCoyoteTime = 0.1f;


    [Header("Wall Slide")]
    [Min(0.01f)] public float wallSlideSpeed = 5f;
    [Range(0.25f, 50f)] public float wallSlideDecelerationSpeed = 50f;


    [Header("Wall Jump")]
    public Vector2 wallJumpDirection = new Vector2(-20f, 6.5f);
    [Range(0f, 1f)] public float wallJumpPostBufferTime = 0.125f;
    [Range(0.01f, 5f)] public float wallJumpGravityOnReleaseMultiplier = 1f;


    [Header("Dash")]
    [Range(0f, 1f)] public float dashTime = 0.11f;
    [Range(1f, 200f)] public float dashSpeed = 40f;
    [Range(0f, 1f)] public float timeBtwDashesOnGround = 0.225f;
    public bool resetDashOnWallSlide = true;
    [Range(0, 5)] public int numberOfDashes = 2;
    [Range(0f, 0.5f)] public float dashDiagonallyBias = 0.4f;


    [Header("Dash Cancel Time")]
    [Range(0.01f, 5f)] public float dashGravityOnReleaseMultiplier = 1f;
    [Range(0.02f, 0.3f)] public float dashTimeForUpwardsCancel = 0.027f;


    [Header("Jump Visualization Tool")]
    public bool showWalkJumpArc = false;
    public bool showRunJumpArc = false;
    public bool stopOnCollision = true;
    public bool drawRight = true;
    [Range(5, 100)] public int arcResolution = 20;
    [Range(0, 500)] public int visualizationSteps = 90;


    public readonly Vector2[] dashDirections = new Vector2[]
    {
        new Vector2(0,0), //nothing
        new Vector2(1,0), //right
        new Vector2(1,1).normalized, //top-right
        new Vector2(0,1), //up
        new Vector2(-1,1).normalized, //top-left
        new Vector2(-1,0), //left
        new Vector2(-1,-1).normalized, //bottom-left
        new Vector2(0,-1), //down
        new Vector2(1,-1).normalized //bottom-right
    };


    //Jump
    public float gravity { get; private set; }
    public float initialJumpVelocity {  get; private set; }
    public float adjustedJumpHeight { get; private set; }


    //Wall Jump
    public float wallJumpGravity { get; private set; }
    public float initialWallJumpVelocity { get; private set; }
    public float adjustedWallJumpHeight { get; private set; }


    private void OnValidate()
    {
        calculateValues();
    }

    private void OnEnable()
    {
        calculateValues();
    }

    private void calculateValues()
    {
        //jump
        adjustedJumpHeight = jumpHeight * jumpHeightCompensationFactor;
        gravity = -(2f * adjustedJumpHeight) / Mathf.Pow(timeTillJumpApex, 2f);
        initialJumpVelocity = Mathf.Abs(gravity) * timeTillJumpApex;


        //wall jump
        adjustedWallJumpHeight = wallJumpDirection.y * jumpHeightCompensationFactor;
        wallJumpGravity = -(2f * adjustedWallJumpHeight) / Mathf.Pow(timeTillJumpApex, 2f);
        initialWallJumpVelocity = Mathf.Abs(wallJumpGravity) * timeTillJumpApex;
     }
}  
