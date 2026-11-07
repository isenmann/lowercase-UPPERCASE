using System;
using UnityEngine;

namespace LowercaseUppercase
{
    public enum LetterForm { Lowercase, Uppercase }

    /// <summary>
    /// The player. The transform's origin is at the letter's feet, so forms grow upward.
    /// Input arrives through SetInput and PressJump, which keeps it independent of devices.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class LetterController : MonoBehaviour
    {
        const float EdgeRadius = 0.03f;
        const float GroundProbe = 0.05f;
        const float OverlapSkin = 0.02f;
        const float StickToGroundSpeed = -1f;
        const float LiftAcceleration = 60f;

        public FormSettings lowercase;
        public FormSettings uppercase;
        public LayerMask levelMask;

        [Header("Comma swing")]
        public float hookMinRadius = 0.9f;
        public float swingGravityScale = 0.5f;
        public float swingPump = 20f;
        public float swingDamping = 0.25f;
        public float maxSwingAngle = 1.45f;
        public float maxEntrySwingSpeed = 6f;
        [Tooltip("Share of the jump speed added upward when jumping off a comma.")]
        public float hookReleaseBoost = 0.55f;
        public float regrabDelay = 0.35f;

        public event Action<LetterForm> FormChanged;
        public event Action GrowDenied;
        public event Action Jumped;
        public event Action<float> Landed;
        public event Action Launched;
        public event Action Smashed;
        public event Action Slotted;
        public event Action Burned;

        public LetterForm Form { get; private set; } = LetterForm.Lowercase;
        public bool IsGrounded { get; private set; }
        public bool IsSlotted { get; private set; }
        public bool IsCapsLocked { get; private set; }
        public bool IsClinging { get; private set; }
        public bool IsHanging => hook != null;
        public Vector2 HookAnchor => hook != null ? hook.WorldCentre : Vector2.zero;
        public WordBlock Surface => surface;
        public Vector2 Velocity => rb.linearVelocity;
        public FormSettings Current => Form == LetterForm.Lowercase ? lowercase : uppercase;

        Rigidbody2D rb;
        BoxCollider2D box;
        ContactFilter2D solidFilter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        readonly Collider2D[] overlaps = new Collider2D[1];

        float moveInput;
        bool jumpHeld;
        bool shiftHeld;
        float jumpBufferTimer;
        float coyoteTimer;
        bool risingFromJump;
        bool growWasDenied;
        float facing = 1f;
        float previousVerticalSpeed;
        WordBlock surface;

        WordBlock hook;
        WordBlock lastHook;
        float regrabTimer;
        float hookRadius;
        float swingAngle;
        float swingSpeed;

        // Trigger callbacks run after the physics step, so they are read at the start of the next one.
        WordBlock pendingLift;
        CapsLockZone pendingCapsZone;

        bool WantsUppercase => shiftHeld != IsCapsLocked;

        public void SetInput(float move, bool jump, bool shift)
        {
            moveInput = Mathf.Clamp(move, -1f, 1f);
            jumpHeld = jump;
            shiftHeld = shift;
        }

        public void PressJump() => jumpBufferTimer = Current.jumpBuffer;

        public void SlotInto(Vector2 feetPosition)
        {
            if (IsSlotted) return;
            hook = null;
            IsSlotted = true;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            transform.position = feetPosition;
            Slotted?.Invoke();
        }

        /// <summary>Puts the letter back on safe ground without reloading the scene.</summary>
        public void Respawn(Vector2 feetPosition)
        {
            hook = null;
            IsGrounded = false;
            surface = null;
            risingFromJump = false;
            jumpBufferTimer = 0f;
            previousVerticalSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.position = feetPosition;
            transform.position = feetPosition;
        }

        public void Burn() => Burned?.Invoke();

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            box.sharedMaterial = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };

            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(levelMask);

            ApplyBody(lowercase);
        }

        void FixedUpdate()
        {
            if (IsSlotted) return;

            Vector2 velocity = rb.linearVelocity;
            float dt = Time.fixedDeltaTime;

            IsCapsLocked = pendingCapsZone != null && pendingCapsZone.IsActive;
            pendingCapsZone = null;
            WordBlock lift = pendingLift;
            pendingLift = null;
            regrabTimer -= dt;

            if (hook != null)
            {
                IsGrounded = false;
                IsClinging = false;
                StepHanging(dt);
                return;
            }

            UpdateGround(velocity);
            if (IsGrounded && surface != null) surface.OnStood(this, dt);
            ResolveForm(ref velocity);
            if (Form == LetterForm.Uppercase) SmashAhead(velocity, dt);
            Run(ref velocity, dt);
            Cling(ref velocity);
            Spring(ref velocity);
            Jump(ref velocity, dt);
            ApplyGravity(ref velocity, dt, lift);

            rb.linearVelocity = velocity;
            previousVerticalSpeed = velocity.y;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var zone = other.GetComponent<CapsLockZone>();
            if (zone != null)
            {
                pendingCapsZone = zone;
                return;
            }

            var block = other.GetComponent<WordBlock>();
            if (block == null) return;

            if (block.kind == WordKind.Up)
            {
                pendingLift = block;
            }
            else if (block.kind == WordKind.Comma && !IsSlotted)
            {
                if (Form == LetterForm.Uppercase) block.Smash();
                else TryGrab(block);
            }
        }

        // ---------------------------------------------------------------- ground and form

        void UpdateGround(Vector2 velocity)
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = false;
            surface = null;

            if (velocity.y <= 0.01f)
            {
                int count = box.Cast(Vector2.down, solidFilter, hits, GroundProbe);
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].normal.y < 0.7f) continue;
                    IsGrounded = true;
                    surface = hits[i].collider.GetComponent<WordBlock>();
                    break;
                }
            }

            if (IsGrounded && !wasGrounded) Landed?.Invoke(-previousVerticalSpeed);
        }

        void ResolveForm(ref Vector2 velocity)
        {
            if (WantsUppercase && Form == LetterForm.Lowercase)
            {
                if (TryFindRoomToGrow(out float nudge))
                {
                    rb.position += new Vector2(nudge, 0f);
                    EnterForm(LetterForm.Uppercase, ref velocity);
                    growWasDenied = false;
                }
                else if (!growWasDenied)
                {
                    growWasDenied = true;
                    GrowDenied?.Invoke();
                }
            }
            else if (!WantsUppercase)
            {
                growWasDenied = false;
                if (Form == LetterForm.Uppercase) EnterForm(LetterForm.Lowercase, ref velocity);
            }
        }

        /// <summary>
        /// Looks for room for the UPPERCASE body. Beside a wall the bigger body would overlap it,
        /// so sideways nudges up to the width difference are allowed. Ceilings still block.
        /// </summary>
        bool TryFindRoomToGrow(out float nudge)
        {
            Vector2 size = uppercase.colliderSize;
            float maxNudge = (size.x - lowercase.colliderSize.x) * 0.5f + OverlapSkin;
            float[] candidates = { 0f, maxNudge * 0.5f, -maxNudge * 0.5f, maxNudge, -maxNudge };
            Vector2 testSize = size - Vector2.one * OverlapSkin;

            foreach (float candidate in candidates)
            {
                Vector2 centre = rb.position + new Vector2(candidate, size.y * 0.5f);
                if (Physics2D.OverlapBox(centre, testSize, 0f, solidFilter, overlaps) == 0)
                {
                    nudge = candidate;
                    return true;
                }
            }

            nudge = 0f;
            return false;
        }

        void EnterForm(LetterForm form, ref Vector2 velocity)
        {
            Form = form;
            FormSettings settings = Current;
            ApplyBody(settings);

            if (!IsGrounded && velocity.y < 0f)
            {
                float direction = Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput)
                    : Mathf.Abs(velocity.x) > 0.1f ? Mathf.Sign(velocity.x)
                    : facing;
                velocity.x += direction * -velocity.y * settings.fallToForward;
                velocity.y *= settings.fallKept;
            }

            FormChanged?.Invoke(form);
        }

        void ApplyBody(FormSettings settings)
        {
            Vector2 size = settings.colliderSize;
            box.edgeRadius = EdgeRadius;
            box.size = size - Vector2.one * (EdgeRadius * 2f);
            box.offset = new Vector2(0f, size.y * 0.5f);
            rb.mass = settings.mass;
        }

        void SmashAhead(Vector2 velocity, float dt)
        {
            float direction = Mathf.Abs(velocity.x) > 0.1f ? Mathf.Sign(velocity.x)
                : Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput)
                : 0f;

            if (direction != 0f)
                SmashAlong(new Vector2(direction, 0f), Mathf.Abs(velocity.x) * dt + GroundProbe);
            if (velocity.y < StickToGroundSpeed)
                SmashAlong(Vector2.down, -velocity.y * dt + GroundProbe);
        }

        void SmashAlong(Vector2 direction, float distance)
        {
            int count = box.Cast(direction, solidFilter, hits, distance);
            for (int i = 0; i < count; i++)
            {
                WordBlock block = hits[i].collider.GetComponent<WordBlock>();
                if (block == null || block.kind != WordKind.Cracked) continue;
                block.Smash();
                Smashed?.Invoke();
            }
        }

        // ---------------------------------------------------------------- movement

        void Run(ref Vector2 velocity, float dt)
        {
            FormSettings s = Current;
            bool hasInput = Mathf.Abs(moveInput) > 0.01f;
            if (hasInput) facing = Mathf.Sign(moveInput);

            bool onWord = IsGrounded && surface != null;
            float grip = onWord ? surface.grip : 1f;
            float runSpeed = s.runSpeed * (onWord ? surface.moveFactor : 1f);
            float target = moveInput * runSpeed;
            float rate;

            if (!hasInput)
            {
                rate = IsGrounded ? s.groundDeceleration * grip : s.airDeceleration;
            }
            else if (Mathf.Sign(velocity.x) == Mathf.Sign(moveInput) && Mathf.Abs(velocity.x) > runSpeed)
            {
                rate = IsGrounded ? s.overspeedDecayGround * grip : s.overspeedDecayAir;
            }
            else
            {
                rate = IsGrounded ? s.groundAcceleration * grip : s.airAcceleration;
                bool turning = Mathf.Abs(velocity.x) > 0.01f && Mathf.Sign(velocity.x) != Mathf.Sign(moveInput);
                if (turning) rate += IsGrounded ? s.groundDeceleration * grip : s.airDeceleration;
            }

            velocity.x = Mathf.MoveTowards(velocity.x, target, rate * dt);
        }

        /// <summary>Pressing into the side of a sticky word holds the letter there, and it can jump from there.</summary>
        void Cling(ref Vector2 velocity)
        {
            IsClinging = false;
            if (IsGrounded || Mathf.Abs(moveInput) < 0.01f) return;

            int count = box.Cast(new Vector2(Mathf.Sign(moveInput), 0f), solidFilter, hits, GroundProbe);
            for (int i = 0; i < count; i++)
            {
                WordBlock block = hits[i].collider.GetComponent<WordBlock>();
                if (block == null || block.kind != WordKind.Sticky) continue;
                IsClinging = true;
                velocity.x = 0f;
                return;
            }
        }

        void Spring(ref Vector2 velocity)
        {
            if (!IsGrounded || surface == null || surface.kind != WordKind.Spring) return;

            velocity.y = Current.VelocityToReach(Current.springHeight);
            IsGrounded = false;
            coyoteTimer = 0f;
            risingFromJump = false;
            surface.Bounce();
            Launched?.Invoke();
        }

        void Jump(ref Vector2 velocity, float dt)
        {
            coyoteTimer = IsGrounded || IsClinging ? Current.coyoteTime : coyoteTimer - dt;
            jumpBufferTimer -= dt;

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                float factor = IsGrounded && surface != null ? surface.jumpFactor : 1f;
                velocity.y = Current.JumpVelocity * factor;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                IsGrounded = false;
                IsClinging = false;
                risingFromJump = true;
                Jumped?.Invoke();
            }

            if (risingFromJump && !jumpHeld && velocity.y > 0f)
            {
                velocity.y *= Current.jumpCutMultiplier;
                risingFromJump = false;
            }
            else if (velocity.y <= 0f)
            {
                risingFromJump = false;
            }
        }

        void ApplyGravity(ref Vector2 velocity, float dt, WordBlock lift)
        {
            FormSettings s = Current;

            if (IsGrounded)
            {
                // A small downward speed keeps the body on the surface instead of hovering at the probe distance.
                velocity.y = StickToGroundSpeed;
                return;
            }

            if (lift != null)
            {
                float speed = Form == LetterForm.Lowercase ? lift.liftSpeedLower : lift.liftSpeedUpper;
                velocity.y = Mathf.MoveTowards(velocity.y, speed, LiftAcceleration * dt);
                return;
            }

            if (IsClinging && velocity.y <= 0f)
            {
                velocity.y = 0f;
                return;
            }

            float gravity = s.Gravity;
            if (jumpHeld && Mathf.Abs(velocity.y) < s.apexHangThreshold) gravity *= s.apexHangGravityMultiplier;
            else if (velocity.y < 0f) gravity *= s.fallGravityMultiplier;

            velocity.y = Mathf.Max(velocity.y - gravity * dt, -s.maxFallSpeed);
        }

        // ---------------------------------------------------------------- comma swing

        void TryGrab(WordBlock block)
        {
            if (hook != null || IsGrounded) return;
            if (block == lastHook && regrabTimer > 0f) return;

            Vector2 fromAnchor = rb.position - block.WorldCentre;
            hook = block;
            hookRadius = Mathf.Max(fromAnchor.magnitude, hookMinRadius);
            swingAngle = Mathf.Atan2(fromAnchor.x, -fromAnchor.y);
            swingSpeed = Mathf.Clamp(Vector2.Dot(rb.linearVelocity, SwingTangent()) / hookRadius,
                -maxEntrySwingSpeed, maxEntrySwingSpeed);
            jumpBufferTimer = 0f;
            risingFromJump = false;
        }

        void StepHanging(float dt)
        {
            if (WantsUppercase)
            {
                // Too fragile for capitals: the comma snaps and the letter falls, then grows on the next step.
                WordBlock snapped = hook;
                ReleaseHook(0f);
                snapped.Smash();
                return;
            }

            jumpBufferTimer -= dt;
            if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer = 0f;
                ReleaseHook(hookReleaseBoost);
                Jumped?.Invoke();
                return;
            }

            float gravity = Current.Gravity * swingGravityScale;
            float acceleration = -gravity / hookRadius * Mathf.Sin(swingAngle) + moveInput * swingPump
                - swingDamping * swingSpeed;
            swingSpeed += acceleration * dt;
            swingAngle += swingSpeed * dt;

            if (Mathf.Abs(swingAngle) > maxSwingAngle)
            {
                swingAngle = Mathf.Sign(swingAngle) * maxSwingAngle;
                swingSpeed = 0f;
            }

            Vector2 target = hook.WorldCentre + new Vector2(Mathf.Sin(swingAngle), -Mathf.Cos(swingAngle)) * hookRadius;
            rb.linearVelocity = (target - rb.position) / dt;
        }

        void ReleaseHook(float upwardBoost)
        {
            Vector2 velocity = SwingTangent() * (swingSpeed * hookRadius);
            velocity.y += Current.JumpVelocity * upwardBoost;
            rb.linearVelocity = velocity;
            previousVerticalSpeed = velocity.y;
            lastHook = hook;
            regrabTimer = regrabDelay;
            coyoteTimer = 0f;
            hook = null;
        }

        Vector2 SwingTangent() => new Vector2(Mathf.Cos(swingAngle), Mathf.Sin(swingAngle));
    }
}
