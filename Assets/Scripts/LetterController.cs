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
        const float StompTolerance = 0.05f;
        const float DeathHopShare = 0.5f;
        // Landings slower than this (stepping off a low ledge, settling after a lift) make no sound.
        const float QuietLandingSpeed = 3f;
        const float BumpSoundGap = 0.2f;

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

        [Header("Slam")]
        [Tooltip("Turning A in the air stops the letter for this long before it dives.")]
        public float slamHangSeconds = 0.08f;
        [Tooltip("Units per second straight down at the start of the dive.")]
        public float slamSpeed = 18f;
        [Tooltip("Share of the sideways speed the dive keeps.")]
        public float slamKeptX = 0.2f;
        [Tooltip("Enemies standing within this many units to either side of the landing are knocked out.")]
        public float shockRadius = 1.6f;
        [Tooltip("How far an enemy's feet may be above or below the letter's feet and still be hit.")]
        public float shockHeight = 0.3f;

        public event Action<LetterForm> FormChanged;
        public event Action GrowDenied;
        public event Action Jumped;
        public event Action<float> Landed;
        public event Action Launched;
        public event Action Smashed;
        public event Action Slotted;
        public event Action Burned;
        public event Action SlamStarted;
        /// <summary>A slam reached the ground. The number is how many enemies the shockwave knocked out.</summary>
        public event Action<int> Slammed;

        public LetterForm Form { get; private set; } = LetterForm.Lowercase;
        public bool IsGrounded { get; private set; }
        public bool IsSlotted { get; private set; }
        public bool IsCapsLocked { get; private set; }
        public bool IsClinging { get; private set; }
        public bool IsFallingOff { get; private set; }
        /// <summary>From turning A in the air until the letter lands, bounces or grabs something.</summary>
        public bool IsSlamming { get; private set; }
        public bool IsHanging => hook != null;
        public Vector2 HookAnchor => hook != null ? hook.WorldCentre : Vector2.zero;
        public WordBlock Surface => surface;
        public Vector2 Velocity => rb.linearVelocity;
        public FormSettings Current => Form == LetterForm.Lowercase ? lowercase : uppercase;
        public float Facing => facing;
        public Vector2 BodyCentre => rb.position + Vector2.up * (Current.colliderSize.y * 0.5f);

        Rigidbody2D rb;
        BoxCollider2D box;
        ContactFilter2D solidFilter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        readonly Collider2D[] overlaps = new Collider2D[1];
        readonly System.Collections.Generic.List<Collider2D> shockHits = new System.Collections.Generic.List<Collider2D>();

        float moveInput;
        bool jumpHeld;
        bool shiftHeld;
        bool caseSwapped; // Set by touching a case swapper: Shift works the other way round, like Caps Lock.
        float jumpBufferTimer;
        float coyoteTimer;
        bool risingFromJump;
        bool growWasDenied;
        float facing = 1f;
        float previousVerticalSpeed;
        float lastBumpSound = float.NegativeInfinity;
        float slamHangTimer;
        float slamCarryX;
        bool wasCapsLocked;
        bool wantedUppercase;
        // UPPERCASE was last asked for by a Caps Lock zone or a case swapper, not the player's Shift. Kept until the
        // letter grows, because a ceiling can hold the growing back for a while.
        bool upperFromCaps;
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

        /// <summary>Turns Shift around until the next swap or the next life, as if Caps Lock had been pressed.</summary>
        public void SwapCase() => caseSwapped = !caseSwapped;

        public void PressJump() => jumpBufferTimer = Current.jumpBuffer;

        public void SlotInto(Vector2 feetPosition)
        {
            if (IsSlotted) return;
            hook = null;
            IsSlamming = false;
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
            IsSlamming = false;
            IsFallingOff = false;
            box.enabled = true;
            IsGrounded = false;
            surface = null;
            risingFromJump = false;
            jumpBufferTimer = 0f;
            previousVerticalSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.position = feetPosition;
            transform.position = feetPosition;
        }

        public void Burn()
        {
            if (!IsFallingOff) Burned?.Invoke();
        }

        /// <summary>The death animation: a small hop, then straight down through the level with nothing to touch.</summary>
        public void FallOffScreen()
        {
            if (IsSlotted || IsFallingOff) return;
            IsFallingOff = true;
            IsSlamming = false;
            hook = null;
            IsGrounded = false;
            IsClinging = false;
            surface = null;
            box.enabled = false;
            rb.linearVelocity = Vector2.up * (Current.JumpVelocity * DeathHopShare);
        }

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

            IsCapsLocked = (pendingCapsZone != null && pendingCapsZone.IsActive) != caseSwapped;
            bool capsChanged = IsCapsLocked != wasCapsLocked;
            wasCapsLocked = IsCapsLocked;
            if (WantsUppercase && !wantedUppercase) upperFromCaps = capsChanged;
            wantedUppercase = WantsUppercase;
            pendingCapsZone = null;
            WordBlock lift = pendingLift;
            pendingLift = null;
            regrabTimer -= dt;

            if (IsFallingOff)
            {
                rb.linearVelocity = Vector2.up * (velocity.y - Current.Gravity * dt);
                return;
            }

            if (hook != null)
            {
                IsSlamming = false;
                IsGrounded = false;
                IsClinging = false;
                StepHanging(dt);
                return;
            }

            UpdateGround(velocity);
            if (IsSlamming && IsGrounded) LandSlam();
            if (IsGrounded && surface != null) surface.OnStood(this, dt);
            if (IsFallingOff) return; // A hot word can burn the letter just above, and the hop must survive this step.
            ResolveForm(ref velocity);
            if (Form == LetterForm.Uppercase) SmashAhead(velocity, dt);
            Run(ref velocity, dt);
            Cling(ref velocity);
            Spring(ref velocity);
            Jump(ref velocity, dt);
            ApplyGravity(ref velocity, dt, lift);
            if (IsSlamming) StepSlam(ref velocity, dt, lift);
            BumpAbove(velocity, dt);

            rb.linearVelocity = velocity;
            previousVerticalSpeed = velocity.y;
        }

        // Stay first arrives one physics step after contact, too late to tell whether the letter came from above.
        void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null) TouchEnemy(enemy);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                TouchEnemy(enemy);
                return;
            }

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

        // ---------------------------------------------------------------- enemies

        void TouchEnemy(Enemy enemy)
        {
            if (IsSlotted) return;

            switch (enemy.Touch(this, CameFromAbove(enemy)))
            {
                case EnemyTouch.Stomped:
                    Sfx.Play(Sound.EnemyStomp);
                    BounceOffEnemy();
                    Smashed?.Invoke();
                    break;
                case EnemyTouch.Hurts:
                    if (!IsFallingOff) Sfx.Play(Sound.DeathEnemy);
                    Burn();
                    break;
            }
        }

        /// <summary>
        /// Compares the feet one physics step ago with the enemy's top, because a fast fall can already be
        /// deep inside the enemy by the first contact. Walking into it or jumping up into it both fail this.
        /// </summary>
        bool CameFromAbove(Enemy enemy)
        {
            if (previousVerticalSpeed >= 0f) return false;
            float feetLastStep = rb.position.y - previousVerticalSpeed * Time.fixedDeltaTime;
            return feetLastStep >= enemy.TopY - StompTolerance;
        }

        void BounceOffEnemy()
        {
            Vector2 velocity = rb.linearVelocity;
            velocity.y = Current.JumpVelocity;
            rb.linearVelocity = velocity;
            previousVerticalSpeed = velocity.y;
            IsSlamming = false;
            IsGrounded = false;
            coyoteTimer = 0f;
            risingFromJump = false;
            Jumped?.Invoke();
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

            if (IsGrounded && !wasGrounded)
            {
                // A slam lands with its own, bigger sound.
                if (-previousVerticalSpeed > QuietLandingSpeed && !IsSlamming) Sfx.Play(Sound.Land);
                Landed?.Invoke(-previousVerticalSpeed);
            }
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

            bool slam = form == LetterForm.Uppercase && !IsGrounded && !upperFromCaps;
            if (form == LetterForm.Lowercase && IsSlamming)
            {
                // Letting go mid-dive glides like any other fall, but no faster than a can fall.
                IsSlamming = false;
                velocity.y = Mathf.Max(velocity.y, -settings.maxFallSpeed);
            }

            if (slam)
            {
                StartSlam(ref velocity);
            }
            else if (!IsGrounded && velocity.y < 0f)
            {
                float direction = Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput)
                    : Mathf.Abs(velocity.x) > 0.1f ? Mathf.Sign(velocity.x)
                    : facing;
                velocity.x += direction * -velocity.y * settings.fallToForward;
                velocity.y *= settings.fallKept;
            }

            Sfx.Play(form == LetterForm.Uppercase ? Sound.ToUpper : Sound.ToLower);
            FormChanged?.Invoke(form);
            if (slam) SlamStarted?.Invoke();
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

        /// <summary>Looks ahead along the upward move, because the physics step will have stopped it by the next look.</summary>
        void BumpAbove(Vector2 velocity, float dt)
        {
            if (velocity.y <= 0f) return;

            int count = box.Cast(Vector2.up, solidFilter, hits, velocity.y * dt + GroundProbe);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].normal.y > -0.7f) continue;
                WordBlock block = hits[i].collider.GetComponent<WordBlock>();
                bool power = block != null && block.kind == WordKind.Power && !block.IsSpent;
                if (block != null) block.HitFromBelow(this);
                // A power block plays its own sound.
                if (!power && Time.time - lastBumpSound > BumpSoundGap) Sfx.Play(Sound.BumpBlock);
                lastBumpSound = Time.time;
            }
        }

        // ---------------------------------------------------------------- slam

        /// <summary>Turning A in the air: a short stop, then a dive almost straight down.</summary>
        void StartSlam(ref Vector2 velocity)
        {
            IsSlamming = true;
            slamHangTimer = slamHangSeconds;
            slamCarryX = velocity.x * slamKeptX;
            velocity = Vector2.zero;
            coyoteTimer = 0f;
            risingFromJump = false;
            jumpBufferTimer = 0f;
        }

        void StepSlam(ref Vector2 velocity, float dt, WordBlock lift)
        {
            if (lift != null || IsClinging)
            {
                IsSlamming = false;
                return;
            }

            if (slamHangTimer > 0f)
            {
                slamHangTimer -= dt;
                velocity = slamHangTimer > 0f ? Vector2.zero : new Vector2(slamCarryX, -slamSpeed);
                return;
            }

            if (velocity.y > 0f) IsSlamming = false;
        }

        /// <summary>
        /// The landing knocks out the enemies standing close by at the same height, like a rolling o does:
        /// anything that can be stomped or shot, so spikes too. Fire and the cursor are left alone.
        /// </summary>
        void LandSlam()
        {
            IsSlamming = false;
            Vector2 feet = rb.position;
            var filter = new ContactFilter2D { useTriggers = true };
            Physics2D.OverlapBox(feet + Vector2.up * 0.5f, new Vector2(shockRadius * 2f, 1f + shockHeight * 2f), 0f,
                filter, shockHits);

            Sfx.Play(Sound.Slam);
            int knockedOut = 0;
            foreach (Collider2D hit in shockHits)
            {
                if (hit == null || !hit.enabled) continue;
                Enemy enemy = hit.GetComponent<Enemy>();
                if (enemy == null || Mathf.Abs(enemy.transform.position.y - feet.y) > shockHeight) continue;
                if (!enemy.stompable && !enemy.shootable) continue;
                enemy.Kill();
                knockedOut++;
            }

            if (knockedOut > 0)
            {
                Sfx.Play(Sound.EnemyStomp);
                Smashed?.Invoke();
            }
            Slammed?.Invoke(knockedOut);
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
            bool wasClinging = IsClinging;
            IsClinging = false;
            if (IsGrounded || Mathf.Abs(moveInput) < 0.01f) return;

            int count = box.Cast(new Vector2(Mathf.Sign(moveInput), 0f), solidFilter, hits, GroundProbe);
            for (int i = 0; i < count; i++)
            {
                WordBlock block = hits[i].collider.GetComponent<WordBlock>();
                if (block == null || block.kind != WordKind.Sticky) continue;
                IsClinging = true;
                velocity.x = 0f;
                if (!wasClinging) Sfx.Play(Sound.WallCling);
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
            Sfx.Play(Sound.Bounce);
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
                Sfx.Play(Sound.Jump);
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
            Sfx.Play(Sound.HookGrab);
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
            Sfx.Play(Sound.HookRelease);
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
