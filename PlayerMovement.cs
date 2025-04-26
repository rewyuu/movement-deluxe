using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public PlayerMovementStats moveStats;
    [SerializeField] private Collider2D _feetColl;
    [SerializeField] private Collider2D _bodyColl;

    private Rigidbody2D _rb;


    //movement vars
    public float horizontalVelocity { get; private set; }
    private bool _isFacingRight;


    //collision check vars
    private RaycastHit2D _groundHit;
    private RaycastHit2D _headHit;
    private RaycastHit2D _wallHit;
    private RaycastHit2D _lastWallHit;
    private bool _isGrounded;
    private bool _bumpedHead;
    private bool _isTouchingWall;
    private bool _wasWallSliding; // Add a flag to track if we were wall sliding


    //jump vars
    public float verticalVelocity { get; private set; }
    private bool _isJumping;
    private bool _isFastFalling;
    private bool _isFalling;
    private float _fastFallTime;
    private float _fastFallReleaseSpeed;
    private int _numberOfJumpsUsed;


    //apex vars
    private float _apexPoint;
    private float _timePastApexThreshold;
    private bool _isPastApexThreshold;


    //jump buffer vars
    private float _jumpBufferTimer;
    private bool _jumpReleasedDuringBuffer;



    //coyote time vars
    private float _coyoteTimer;


    //wall slide vars
    private bool _isWallSliding;
    private bool _isWallSlideFalling;


    //wall jump vars
    private bool _useWallJumpMoveStats;
    private bool _isWallJumping;
    private float _wallJumpTime;
    private bool _isWallJumpFastFalling;
    private bool _isWallJumpFalling;
    private float _wallJumpFastFallTime;
    private float _wallJumpFastFallReleaseSpeed;

    private float _wallJumpPostBufferTimer;
    private bool _wallJumpPerformed; // Add a flag to track if wall jump was performed
    private bool _hasWallJumpedRecently = false;

    private float _wallJumpApexPoint;
    private float _timePastWallJumpApexThreshold;
    private bool _isPastWallJumpApexThreshold;


    //dash vars
    private bool _isDashing;
    private bool _isAirDashing;
    private float _dashTimer;
    private float _dashOnGroundTimer;
    private int _numberOfDashesUsed;
    private Vector2 _dashDirection;
    private bool _isdashFastFalling;
    private float _dashFastFallTime;
    private float _dashFastFallReleaseSpeed;



    private void Awake()
    {
        _isFacingRight = true;
        _rb = GetComponent<Rigidbody2D>();
    }


    private void Update()
    {
        countTimers();
        jumpChecks();
        landCheck();
        wallSlideCheck();
        wallJumpCheck();
        dashCheck();
    }


    private void FixedUpdate()
    {
        collisionChecks();
        jump();
        fall();
        wallSlide();
        wallJump();
        dash();

        if (_isGrounded)
        {
            Move(moveStats.groundAcceleration, moveStats.groundDeceleration, InputManager.Movement);
        }
        else
        {
            //wall jumping
            if (_useWallJumpMoveStats)
            {
                Move(moveStats.wallJumpMoveAcceleration, moveStats.wallJumpMoveDeceleration, InputManager.Movement);
            }
            //airborne
            else
            {
                Move(moveStats.airAcceleration, moveStats.airDeceleration, InputManager.Movement);
            }
        }

        applyVelocity();
    }


    private void applyVelocity()
    {
        //clamp fall speed
        if (!_isDashing)
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity, -moveStats.maxFallSpeed, 50f);
        }
        else
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity, -50f, 50f);
        }
        _rb.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);
    }


    #region Movement

    private void Move(float acceleration, float deceleration, Vector2 moveInput)
    {
        if (!_isDashing)
        {
            if (Mathf.Abs(moveInput.x) >= moveStats.moveThreshold)
            {
                TurnCheck(moveInput);

                float targetVelocity = 0f;

                if (InputManager.runIsHeld)
                {
                    targetVelocity = moveInput.x * moveStats.maxRunSpeed;
                }
                else
                {
                    targetVelocity = moveInput.x * moveStats.maxWalkSpeed;
                }

                horizontalVelocity = Mathf.Lerp(horizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            }

            else if (Mathf.Abs(moveInput.x) < moveStats.moveThreshold)
            {
                horizontalVelocity = Mathf.Lerp(horizontalVelocity, 0f, deceleration * Time.fixedDeltaTime);
            }
        }
    }

    private void TurnCheck(Vector2 moveInput)
    {
        if (_isFacingRight && moveInput.x < 0)
        {
            Turn(false);
        }
        else if (!_isFacingRight && moveInput.x > 0)
        {
            Turn(true);
        }
    }

    private void Turn(bool turnRight)
    {
        if (turnRight)
        {
            _isFacingRight = true;
            transform.Rotate(0f, 180f, 0f);
        }
        else
        {
            _isFacingRight = false;
            transform.Rotate(0f, -180f, 0f);
        }
    }

    #endregion


    #region Land Fall

    private void landCheck()
    {
        //LANDED
        if ((_isJumping || _isFalling || _isWallJumpFalling || _isWallJumping || _isWallSlideFalling || _isWallSliding || _isdashFastFalling) && _isGrounded && verticalVelocity <= 0f)
        {
            resetJumpValues();
            stopWallSlide();
            resetWallJumpValues();
            resetDashes();

            _numberOfJumpsUsed = 0;
            _wallJumpPerformed = false; // Reset wall jump performed flag

            verticalVelocity = Physics2D.gravity.y;

            if (_isdashFastFalling && _isGrounded)
            {
                resetDashValues();
                return;
            }
            resetDashValues();
        }
    }


    private void fall()
    {
        //normal gravity while falling
        if (_isGrounded && !_isJumping && !_isWallSliding && !_isWallJumping && !_isDashing && !_isdashFastFalling)
        {
            if (!_isFalling)
            {
                _isFalling = true;
            }

            verticalVelocity += moveStats.gravity * Time.deltaTime;
        }
    }

    #endregion


    #region Jump

    private void resetJumpValues()
    {
        _isJumping = false;
        _isFalling = false;
        _isFastFalling = false;
        _fastFallTime = 0f;
        _isPastApexThreshold = false;
    }


    private void jumpChecks()
    {
        // Track if we just left a wall
        bool justLeftWall = _wasWallSliding && !_isWallSliding;
        _wasWallSliding = _isWallSliding;

        //WHEN JUMP BUTTON IS PRESSED
        if (InputManager.jumpWasPressed)
        {
            // Only handle wall jump logic if we're actually wall sliding or touching a wall
            if (_isWallSliding || (_isTouchingWall && !_isGrounded))
            {
                // Skip buffer for wall jump - will be handled in wallJumpCheck
                return;
            }

            _jumpBufferTimer = moveStats.jumpBufferTime;
            _jumpReleasedDuringBuffer = false;
        }

        //WHEN JUMP BUTTON IS RELEASED
        if (InputManager.jumpWasReleased)
        {
            if (_jumpBufferTimer > 0f)
            {
                _jumpReleasedDuringBuffer = false;
            }
        }

        if (_isJumping && verticalVelocity > 0f)
        {
            if (_isPastApexThreshold)
            {
                _isPastApexThreshold = false;
                _isFalling = true;
                _fastFallTime = moveStats.timeForUpwardsCancel;
                verticalVelocity = 0f;
            }
            else
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = verticalVelocity;
            }
        }

        //INITIATE JUMP WITH JUMP BUFFER AND COYOTE TIME
        if (_jumpBufferTimer > 0f && !_isJumping && (_isGrounded || _coyoteTimer > 0f) && !_isWallJumping)
        {
            Debug.Log("JUMP TRIGGERED");
            initiateJump(1);

            if (_jumpReleasedDuringBuffer)
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = verticalVelocity;
            }
        }

        //DOUBLE JUMP - Don't allow right after wall jump
        else if (_jumpBufferTimer > 0f &&
                (_isJumping || (_isWallJumping && !justLeftWall) ||
                _isWallSlideFalling || _isAirDashing || _isdashFastFalling) &&
                !_isTouchingWall &&
                _numberOfJumpsUsed < moveStats.numberOfJumpsAllowed &&
                !_wallJumpPerformed) // Don't allow double jump right after wall jump
        {
            _isFastFalling = false;
            _isFalling = false;
            _isPastApexThreshold = false;
            _timePastApexThreshold = 0f;

            initiateJump(1);

            if (_isdashFastFalling)
            {
                _isdashFastFalling = false;
            }
        }

        //AIR JUMP AFTER COYOTE TIME FINISHED
        else if (_jumpBufferTimer > 0f && _isFalling && !_isWallSlideFalling &&
                _numberOfJumpsUsed < moveStats.numberOfJumpsAllowed - 1 &&
                !_wallJumpPerformed) // Don't allow air jump right after wall jump
        {
            initiateJump(2);
            _isFastFalling = false;
        }
    }



    private void initiateJump(int numberOfJumpsUsed)
    {
        if (!_isJumping)
        {
            _isJumping = true;
        }

        // Don't reset wall jump values during a regular jump
        // This was causing conflicts between jump types
        if (!_isWallJumping)
        {
            resetWallJumpValues();
        }

        _jumpBufferTimer = 0f;
        _numberOfJumpsUsed += numberOfJumpsUsed;

        _isFalling = false;
        _isFastFalling = false;
        _isPastApexThreshold = false;
        _timePastApexThreshold = 0f;
        verticalVelocity = moveStats.initialJumpVelocity;
        Debug.Log($"Jump Velocity Applied: {verticalVelocity}");
    }


    private void jump()
    {
        //apply gravity while jumping
        if (_isJumping)
        {
            //check for head bump
            if (_bumpedHead)
            {
                _isFastFalling = true;
            }

            //gravity on ascending
            if (verticalVelocity >= 0f)
            {
                //apex controls
                _apexPoint = Mathf.InverseLerp(moveStats.initialJumpVelocity, 0f, verticalVelocity);

                if (_apexPoint > moveStats.apexThreshold)
                {
                    if (!_isPastApexThreshold)
                    {
                        _isPastApexThreshold = true;
                        _timePastApexThreshold = 0f;
                    }

                    if (_isPastApexThreshold)
                    {
                        _timePastApexThreshold += Time.fixedDeltaTime;

                        if (_timePastApexThreshold < moveStats.apexHangTime)
                        {
                            verticalVelocity = 0f;
                        }
                        else
                        {
                            verticalVelocity = -0.01f;
                        }
                    }
                }

                //gravity on ascending but not past apex threshold
                else if (!_isFastFalling)
                {
                    verticalVelocity += moveStats.gravity * Time.fixedDeltaTime;

                    if (_isPastApexThreshold)
                    {
                        _isPastApexThreshold = false;
                    }
                }
            }

            //gravity on descending
            else if (!_isFastFalling)
            {
                verticalVelocity += moveStats.gravity * moveStats.gravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }

            else if (verticalVelocity < 0f)
            {
                if (!_isFalling)
                {
                    _isFalling = true;
                }
            }
        }

        //jump cut
        if (_isFastFalling)
        {
            if (_fastFallTime >= moveStats.timeForUpwardsCancel)
            {
                verticalVelocity += moveStats.gravity * moveStats.gravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_fastFallTime < moveStats.timeForUpwardsCancel)
            {
                verticalVelocity = Mathf.Lerp(_fastFallReleaseSpeed, 0f, (_fastFallTime / moveStats.timeForUpwardsCancel));
            }
            _fastFallTime += Time.fixedDeltaTime;
        }
    }

    #endregion


    #region Wall slide

    private void wallSlideCheck()
    {
        // Only initiate wall slide if we're actually touching a wall
        if (_isTouchingWall && !_isGrounded && !_isDashing)
        {
            if (verticalVelocity < 0f && !_isWallSliding)
            {
                resetJumpValues();
                resetWallJumpValues();
                resetDashValues();

                if (moveStats.resetDashOnWallSlide)
                {
                    resetDashes();
                }

                _isWallSlideFalling = false;
                _isWallSliding = true;

                if (moveStats.resetJumpsOnWallSlide)
                {
                    _numberOfJumpsUsed = 0;
                }
            }
        }
        // Handle leaving the wall
        else if (_isWallSliding && (!_isTouchingWall || _isGrounded) && !_isWallSlideFalling)
        {
            _isWallSlideFalling = true;
            stopWallSlide();
        }
        else if (!_isTouchingWall && !_isGrounded)
        {
            stopWallSlide();
        }
    }


    private void stopWallSlide()
    {
        if (_isWallSliding)
        {
            // Don't increment jumps when stopping wall slide
            // _numberOfJumpsUsed++; - Removing this line

            _isWallSliding = false;
        }
    }

    private void wallSlide()
    {
        if (_isWallSliding)
        {
            verticalVelocity = Mathf.Lerp(verticalVelocity, -moveStats.wallSlideSpeed, moveStats.wallSlideDecelerationSpeed * Time.fixedDeltaTime);
        }
    }

    #endregion


    #region Wall Jump


    private void wallJumpCheck()
    {
       
        // Only apply post wall jump buffer if we actually left a wall
        if (shouldApplyPostWallJumpBuffer())
        {
            // Only refresh the buffer if needed
            if (_wallJumpPostBufferTimer <= 0f)
            {
                _wallJumpPostBufferTimer = moveStats.wallJumpPostBufferTime;
            }
        }
        else
        {
            // If we're grounded or touching a wall again, reset the buffer
            if (_isGrounded || _isTouchingWall)
            {
                _wallJumpPostBufferTimer = 0f;
            }
        }

        //wall jump fast falling
        if (InputManager.jumpWasReleased && !_isWallSliding && !_isTouchingWall && _isWallJumping)
        {
            if (verticalVelocity > 0f)
            {
                if (_isPastWallJumpApexThreshold)
                {
                    _isPastWallJumpApexThreshold = false;
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallTime = moveStats.timeForUpwardsCancel;

                    verticalVelocity = 0f;
                }
                else
                {
                    _isWallJumpFastFalling = true;
                    _wallJumpFastFallReleaseSpeed = verticalVelocity;
                }
            }
        }

        // Wall jump can happen either when directly on wall or within buffer
        if (InputManager.jumpWasPressed)
        {
            bool canWallJump = (_isWallSliding || (_isTouchingWall && !_isGrounded)) ||
                              (_wallJumpPostBufferTimer > 0f && !_isGrounded && !_isTouchingWall);

            // Only allow wall jump if not already wall jumping and hasn't just performed one
            if (canWallJump && !_isWallJumping && !_hasWallJumpedRecently)
            {
                initiateWallJump();
                _hasWallJumpedRecently = true;
            }
        }

        // Reset the flag when appropriate conditions are met
        // This could be in your update method or movement handling code
        if (_isGrounded || (!_isTouchingWall && _hasWallJumpedRecently))
        {
            _hasWallJumpedRecently = false;
        }
    }


    private void initiateWallJump()
    {
        // Completely reset previous jump and wall jump states
        resetJumpValues();

        // Set up new wall jump
        _isWallJumping = true;
        _useWallJumpMoveStats = true;
        _wallJumpPerformed = true; // Set the flag for tracking wall jumps

        stopWallSlide();
        _wallJumpTime = 0f;
        _wallJumpPostBufferTimer = 0f; // Reset buffer after using it

        // Reset all fast fall and apex states for the wall jump
        _isWallJumpFastFalling = false;
        _isWallJumpFalling = false;
        _isPastWallJumpApexThreshold = false;
        _timePastWallJumpApexThreshold = 0f;

        verticalVelocity = moveStats.initialWallJumpVelocity;

        int dirMultiplier = 0;
        if (_lastWallHit.collider != null)
        {
            Vector2 hitpoint = _lastWallHit.collider.ClosestPoint(_bodyColl.bounds.center);

            if (hitpoint.x > transform.position.x)
            {
                dirMultiplier = -1;
            }
            else
            {
                dirMultiplier = 1;
            }
        }
        else
        {
            Debug.LogWarning("No wall hit detected for wall jump! Defaulting direction.");
            // Use facing direction as fallback
            dirMultiplier = _isFacingRight ? -1 : 1;
        }

        horizontalVelocity = Mathf.Abs(moveStats.wallJumpDirection.x) * dirMultiplier;

        // Make sure we're not in normal jump state
        _isJumping = false;
    }


    private void wallJump()
    {
        //apply wall jump gravity
        if (_isWallJumping)
        {
            //time to take over movement controls while wall jumping
            _wallJumpTime += Time.fixedDeltaTime;
            if (_wallJumpTime >= moveStats.timeTillJumpApex)
            {
                _useWallJumpMoveStats = false;
            }

            //hit head
            if (_bumpedHead)
            {
                _isWallJumpFastFalling = true;
                _useWallJumpMoveStats = false;
            }

            //gravity in ascending
            if (verticalVelocity >= 0f)
            {
                //apex controls
                _wallJumpApexPoint = Mathf.InverseLerp(moveStats.initialWallJumpVelocity, 0f, verticalVelocity);

                if (_wallJumpApexPoint > moveStats.apexThreshold)
                {
                    if (!_isPastWallJumpApexThreshold)
                    {
                        _isPastWallJumpApexThreshold = true;
                        _timePastWallJumpApexThreshold = 0f;
                    }

                    if (_isPastWallJumpApexThreshold)
                    {
                        _timePastWallJumpApexThreshold += Time.fixedDeltaTime;
                        if (_timePastWallJumpApexThreshold < moveStats.apexHangTime)
                        {
                            verticalVelocity = 0f;
                        }
                        else
                        {
                            verticalVelocity = -0.01f;
                        }
                    }
                }

                //gravity in ascending but not past apex threshold
                else if (!_isWallJumpFastFalling)
                {
                    verticalVelocity += moveStats.wallJumpGravity * Time.fixedDeltaTime;

                    if (_isPastWallJumpApexThreshold)
                    {
                        _isPastWallJumpApexThreshold = false;
                    }
                }
            }

            //gravity on descending
            else if (!_isWallJumpFastFalling)
            {
                verticalVelocity += moveStats.wallJumpGravity * Time.fixedDeltaTime;
            }
            else if (verticalVelocity < 0f)
            {
                if (!_isWallJumpFalling)
                {
                    _isWallJumpFalling = true;
                }
            }
        }

        //handle wall jump cut time
        if (_isWallJumpFastFalling)
        {
            if (_wallJumpFastFallTime >= moveStats.timeForUpwardsCancel)
            {
                verticalVelocity += moveStats.wallJumpGravity * moveStats.wallJumpGravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_wallJumpFastFallTime < moveStats.timeForUpwardsCancel)
            {
                verticalVelocity = Mathf.Lerp(_wallJumpFastFallReleaseSpeed, 0f, (_wallJumpFastFallTime / moveStats.timeForUpwardsCancel));
            }

            _wallJumpFastFallTime += Time.fixedDeltaTime;
        }
    }


    private bool shouldApplyPostWallJumpBuffer()
    {
        // Only apply buffer if we were recently wall sliding or touching a wall
        // but are no longer touching a wall (and not grounded)
        if (!_isGrounded && !_isTouchingWall && (_wasWallSliding || _isWallSlideFalling))
        {
            return true;
        }
        return false;
    }


    private void resetWallJumpValues()
    {
        _isWallSlideFalling = false;
        _useWallJumpMoveStats = false;
        _isWallJumping = false;
        _isWallJumpFastFalling = false;
        _isWallJumpFalling = false;
        _isPastWallJumpApexThreshold = false;

        _wallJumpFastFallTime = 0f;
        _wallJumpTime = 0f;
        // Note: We do NOT reset _wallJumpPerformed here - it needs to persist until landing
    }

    #endregion


    #region Dash


    private void dashCheck()
    {
        if (InputManager.dashWasPressed)
        {
            //ground dash
            if (_isGrounded && _dashOnGroundTimer < 0 && !_isDashing)
            {
                initiateDash();
            }

            //air dash
            else if (!_isGrounded && !_isDashing && _numberOfDashesUsed < moveStats.numberOfDashes)
            {
                _isAirDashing = true;
                initiateDash();

                //you left a wallside but dashed within the wall jump post buffer timer
                if(_wallJumpPostBufferTimer > 0f)
                {
                    _numberOfJumpsUsed--;

                    if (_numberOfJumpsUsed < 0f)
                    {
                        _numberOfJumpsUsed = 0;
                    }
                }
            }
        }
    }


    private void initiateDash()
    {
        _dashDirection = InputManager.Movement;

        Vector2 closestDirection = Vector2.zero;
        float minDistance = Vector2.Distance(_dashDirection, moveStats.dashDirections[0]);

        for (int i = 0; i < moveStats.dashDirections.Length; i++)
        {
            //skip if we hit it bang on
            if (_dashDirection == moveStats.dashDirections[i])
            {
                closestDirection = _dashDirection;
                break;
            }

            float distance = Vector2.Distance(_dashDirection, moveStats.dashDirections[i]);

            //check if this is diagonal direction and apply bias
            bool isDiagonal = (Mathf.Abs(moveStats.dashDirections[i].x) == 1 && Mathf.Abs(moveStats.dashDirections[i].y) == 1);
            if (isDiagonal)
            {
                distance -= moveStats.dashDiagonallyBias;
            }
            else if (distance < minDistance)
            {
                minDistance = distance;
                closestDirection = moveStats.dashDirections[i];
            }
        }

        // handle directions with NO input
        if (closestDirection == Vector2.zero)
        {
            if (_isFacingRight)
            {
                closestDirection = Vector2.right;
            }
            else
            {
                closestDirection = Vector2.left;
            }
        }

        _dashDirection = closestDirection;
        _numberOfDashesUsed++;
        _isDashing = true;
        _dashTimer = 0f;
        _dashOnGroundTimer = moveStats.timeBtwDashesOnGround;

        resetJumpValues();
        resetWallJumpValues();
        stopWallSlide();
    }
    private void dash()
    {
        if (_isDashing)
        {
            //stop the dash after the timer
            _dashTimer += Time.fixedDeltaTime;
            if (_dashTimer >= moveStats.dashTime)
            {
                if (_isGrounded)
                {
                    resetDashes();
                }

                _isAirDashing = false;
                _isDashing = false;

                if (!_isJumping && !_isWallJumping)
                {
                    _dashFastFallTime = 0f;
                    _dashFastFallReleaseSpeed = verticalVelocity;

                    if (!_isGrounded)
                    {
                        _isdashFastFalling = true;
                    }
                }

                return;
            }

            horizontalVelocity = moveStats.dashSpeed * _dashDirection.x;

            if (_dashDirection.y != 0f || _isAirDashing)
            {
                verticalVelocity = moveStats.dashSpeed * _dashDirection.y;
            }
        }

        //handle dash cut time
        else if (_isdashFastFalling)
        {
            if (verticalVelocity > 0f)
            {
                if (_dashFastFallTime < moveStats.dashTimeForUpwardsCancel)
                {
                    verticalVelocity = Mathf.Lerp(_dashFastFallReleaseSpeed, 0f, (_dashFastFallTime / moveStats.dashTimeForUpwardsCancel));
                }
                else if (_dashFastFallTime >= moveStats.dashTimeForUpwardsCancel)
                {
                    verticalVelocity += moveStats.gravity * moveStats.dashGravityOnReleaseMultiplier * Time.fixedDeltaTime;
                }

                _dashFastFallTime += Time.fixedDeltaTime;
            }

            else
            {
                verticalVelocity += moveStats.gravity * moveStats.dashGravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
        }
    }

    private void resetDashValues()
    {
        _isdashFastFalling = false;
        _dashOnGroundTimer = -0.01f;
    }


    private void resetDashes()
    {
        _numberOfDashesUsed = 0;
    }

    #endregion


    #region Collision checks

    private void isGrounded()
    {
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _feetColl.bounds.min.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, moveStats.groundDetectionRayLength);

        _groundHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.down, moveStats.groundDetectionRayLength, moveStats.GroundLayer);
        if (_groundHit.collider != null)
        {
            _isGrounded = true;
        }
        else
        {
            _isGrounded = false;
        }
    }

    private void bumpedHead()
    {
        Vector2 boxcastOrigin = new Vector2(_feetColl.bounds.center.x, _bodyColl.bounds.max.y);
        Vector2 boxcastSize = new Vector2(_feetColl.bounds.size.x * moveStats.headWidth, moveStats.headDetectionRayLength);

        _headHit = Physics2D.BoxCast(boxcastOrigin, boxcastSize, 0f, Vector2.up, moveStats.headDetectionRayLength, moveStats.GroundLayer);

        if (_headHit.collider != null)
        {
            _bumpedHead = true;
        }
        else
        {
            _bumpedHead = false;
        }
    }


    private void isTouchingWall()
    {
        float originEndPoint = 0f;

        if (_isFacingRight)
        {
            originEndPoint = _bodyColl.bounds.max.x;
        }
        else
        {
            originEndPoint = _bodyColl.bounds.min.x;
        }

        float adjustedHeight = _bodyColl.bounds.size.y * moveStats.wallDetectionRayHeightMultiplier;

        Vector2 boxCastOrigin = new Vector2(originEndPoint, _bodyColl.bounds.center.y);
        Vector2 boxCastSize = new Vector2(moveStats.wallDetectionRayLength, adjustedHeight);

        // Only check for walls in the direction we're facing
        _wallHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, transform.right, moveStats.wallDetectionRayLength, moveStats.GroundLayer);
        if (_wallHit.collider != null)
        {
            _lastWallHit = _wallHit;
            _isTouchingWall = true;
        }
        else
        {
            _isTouchingWall = false;
        }
    }

    private void collisionChecks()
    {
        isGrounded();
        bumpedHead();
        isTouchingWall();
    }

    #endregion


    #region Timers

    private void countTimers()
    {
        //jump buffer
        _jumpBufferTimer -= Time.deltaTime;

        //jump coyote time
        if (!_isGrounded)
        {
            _coyoteTimer -= Time.deltaTime;
        }
        else
        {
            _coyoteTimer = moveStats.jumpCoyoteTime;
        }

        //wall jump buffer timer - only count down if not touching a wall
        if (!_isTouchingWall && !_isGrounded)
        {
            _wallJumpPostBufferTimer -= Time.deltaTime;
        }

        //dash timer
        if(_isGrounded)
        {
            _dashOnGroundTimer -= Time.deltaTime;
        }
    }   

    #endregion
}