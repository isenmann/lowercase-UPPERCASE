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
        const float CarrierCatchUp = 6f; // Most speed a floating word's rider uses to close the gap to its top.

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

        [Header("Wind")]
        [Tooltip("How quickly the wind changes a's sideways speed in the air when no key is held, units per second squared.")]
        public float windAcceleration = 20f;
        [Tooltip("How much of the wind's push a holds off by walking against it. Below 1, a is still pushed back.")]
        [Range(0f, 1f)] public float headwindHold = 0.5f;
        [Tooltip("How much of A's run speed each unit of wind speed takes away when A walks against it.")]
        public float upperHeadwindDrag = 0.05f;

        [Header("Ink")]
        [Tooltip("How fast a rises to the surface and A sinks to the bottom, units per second.")]
        public float inkRiseSpeed = 2.5f;
        public float inkSinkSpeed = 1.8f;
        [Tooltip("How quickly the ink changes the letter's vertical speed, units per second squared.")]
        public float inkAcceleration = 25f;
        [Tooltip("How firmly a settles at the surface: speed per unit of depth.")]
        public float inkSettle = 8f;
        [Tooltip("How close to floating half under a has to be to jump out, units.")]
        public float inkFloatReach = 0.15f;
        [Tooltip("Share of the run speed left in ink.")]
        public float inkMoveFactor = 0.6f;
        [Tooltip("How fast the letter has to fall into ink to splash, units per second.")]
        public float splashSpeed = 1.5f;

        [Header("Pushing")]
        [Tooltip("How close the side of a heavy word has to be for A walking into it to push it, units.")]
        public float pushReach = 0.06f;

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
        /// <summary>The letter fell into ink: where it broke the surface, and how fast it was falling.</summary>
        public event Action<Vector2, float> Splashed;

        public LetterForm Form { get; private set; } = LetterForm.Lowercase;
        public bool IsGrounded { get; private set; }
        public bool IsSlotted { get; private set; }
        public bool IsCapsLocked { get; private set; }
        public bool IsClinging { get; private set; }
        public bool IsFallingOff { get; private set; }
        /// <summary>From turning A in the air until the letter lands, bounces or grabs something.</summary>
        public bool IsSlamming { get; private set; }
        /// <summary>A walked into the side of a heavy word this physics step and is pushing it.</summary>
        public bool IsPushing { get; private set; }
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
        FloatingWord carrier;   // The floating word under the letter, if it stands on one.
        float groundVelocityY;  // How fast the ground under the letter moves up or down.

        WordBlock hook;
        WordBlock lastHook;
        float regrabTimer;
        float hookRadius;
        float swingAngle;
        float swingSpeed;

        // Trigger callbacks run after the physics step, so they are read at the start of the next one.
        WordBlock pendingLift;
        CapsLockZone pendingCapsZone;
        Wind pendingWind;
        Wind wind; // The wind the letter is in this step, if any.
        InkPool pendingInk;
        InkPool ink; // The ink the letter is in this step, if any.
        bool wasInInk;

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
            carrier = null;
            wind = null;
            pendingWind = null;
            ink = null;
            pendingInk = null;
            wasInInk = false;
            groundVelocityY = 0f;
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
            wind = pendingWind;
            pendingWind = null;
            ink = pendingInk;
            pendingInk = null;
            // Falling into ink splashes; drifting in sideways or bobbing at the surface doesn't.
            if (ink != null && !wasInInk && velocity.y < -splashSpeed)
            {
                Sfx.Play(Sound.Splash);
                Splashed?.Invoke(new Vector2(rb.position.x, ink.SurfaceY), -velocity.y);
            }
            wasInInk = ink != null;
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
            IsPushing = Form == LetterForm.Uppercase && PushAhead(ref velocity);
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

            var blowing = other.GetComponent<Wind>();
            if (blowing != null)
            {
                pendingWind = blowing;
                return;
            }

            var pool = other.GetComponent<InkPool>();
            if (pool != null)
            {
                pendingInk = pool;
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

            // Measured against the ground's own speed, so a rising floating word still counts as ground.
            if (velocity.y - groundVelocityY <= 0.01f)
            {
                // Riding a floating word, look as far as it moves in a step, or the letter flickers off it.
                float reach = wasGrounded && carrier != null ? Mathf.Abs(groundVelocityY) * Time.fixedDeltaTime * 2f : 0f;
                int count = box.Cast(Vector2.down, solidFilter, hits, GroundProbe + reach);
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].normal.y < 0.7f) continue;
                    IsGrounded = true;
                    surface = hits[i].collider.GetComponent<WordBlock>();
                    break;
                }
            }

            if (surface == null || surface.kind != WordKind.Float) carrier = null;
            else if (carrier == null || carrier.gameObject != surface.gameObject) carrier = surface.GetComponent<FloatingWord>();
            groundVelocityY = IsGrounded && carrier != null ? carrier.Velocity : 0f;

            if (IsGrounded && !wasGrounded)
            {
                // Against the ground's own speed: dropping onto a sinking float is a soft landing.
                float impact = groundVelocityY - previousVerticalSpeed;
                // A slam lands with its own, bigger sound.
                if (impact > QuietLandingSpeed && !IsSlamming) Sfx.Play(Sound.Land);
                Landed?.Invoke(impact);
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

            // In ink, turning A just sinks: there is nothing to dive through.
            bool slam = form == LetterForm.Uppercase && !IsGrounded && !upperFromCaps && ink == null;
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
            if (carrier != null)
            {
                // The letter rides the jolt down instead of being left hanging above the word.
                carrier.Jolt();
                groundVelocityY = carrier.Velocity;
            }
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
            float runSpeed = s.runSpeed * (onWord ? surface.moveFactor : 1f) * (ink != null ? inkMoveFactor : 1f);
            float target = moveInput * runSpeed;
            float windSpeed = wind != null ? wind.Speed : 0f;
            if (windSpeed != 0f) target = WindTarget(target, windSpeed);
            float limit = Mathf.Max(runSpeed, Mathf.Abs(target));
            float rate;

            if (!hasInput)
            {
                rate = IsGrounded ? s.groundDeceleration * grip : s.airDeceleration;
                // In the air a would hardly drift with the wind at the gentle rate of letting go.
                if (windSpeed != 0f && Form == LetterForm.Lowercase) rate = Mathf.Max(rate, windAcceleration);
            }
            else if (Mathf.Sign(velocity.x) == Mathf.Sign(moveInput) && Mathf.Abs(velocity.x) > limit)
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

        /// <summary>
        /// How far the letter's middle is under the ink's surface, units; 0 when not in ink, negative above it. Above
        /// the surface the letter falls as anywhere else, so a jump out of the ink is a whole jump.
        /// </summary>
        float InkDepth => ink != null ? ink.SurfaceY - (rb.position.y + Current.colliderSize.y * 0.5f) : 0f;

        /// <summary>a floating at the ink's surface, where it can jump from.</summary>
        public bool IsFloating => Form == LetterForm.Lowercase && ink != null && Mathf.Abs(InkDepth) <= inkFloatReach;

        /// <summary>a rises to float half under the surface; A sinks to the bottom.</summary>
        void Swim(ref Vector2 velocity, float dt)
        {
            float target = Form == LetterForm.Lowercase
                ? Mathf.Clamp(InkDepth * inkSettle, -inkRiseSpeed, inkRiseSpeed)
                : -inkSinkSpeed;
            velocity.y = Mathf.MoveTowards(velocity.y, target, inkAcceleration * dt);
        }

        /// <summary>
        /// The run speed the letter heads for in the wind. a goes with it faster, drifts along with it when let go, and
        /// is pushed back slowly when it walks against it. A walks into it a little slower and is not moved otherwise.
        /// </summary>
        float WindTarget(float target, float windSpeed)
        {
            float along = moveInput * Mathf.Sign(windSpeed);
            if (Form == LetterForm.Uppercase)
                return along < 0f ? target * Mathf.Clamp01(1f - Mathf.Abs(windSpeed) * upperHeadwindDrag) : target;
            if (along >= 0f) return target + windSpeed;
            return windSpeed * (1f - headwindHold * Mathf.Abs(moveInput));
        }

        /// <summary>
        /// A on the ground walking into the side of a heavy word pushes it, at the word's own speed. a just meets a
        /// wall. Returns whether the letter is pushing.
        /// </summary>
        bool PushAhead(ref Vector2 velocity)
        {
            if (!IsGrounded || Mathf.Abs(moveInput) < 0.1f) return false;
            float direction = Mathf.Sign(moveInput);
            int count = rb.Cast(new Vector2(direction, 0f), solidFilter, hits, pushReach);
            for (int i = 0; i < count; i++)
            {
                if (Vector2.Dot(hits[i].normal, new Vector2(-direction, 0f)) < 0.5f) continue;
                var heavy = hits[i].collider.GetComponent<HeavyWord>();
                if (heavy == null) continue;
                heavy.Push(direction);
                if (velocity.x * direction > heavy.pushSpeed) velocity.x = direction * heavy.pushSpeed;
                return true;
            }
            return false;
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
            coyoteTimer = IsGrounded || IsClinging || IsFloating ? Current.coyoteTime : coyoteTimer - dt;
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

            // a never rests on the bottom of ink: it rises off it, and off anything else under the surface.
            if (Form == LetterForm.Lowercase && ink != null && InkDepth > 0f && !risingFromJump)
            {
                Swim(ref velocity, dt);
                return;
            }

            if (IsGrounded)
            {
                if (carrier != null)
                {
                    // Ride the floating word: its speed, plus whatever closes the gap to its top, so both end the
                    // step touching. Leaving it to the contacts lets the letter bounce off a word moving under it.
                    float gap = carrier.SurfaceY - rb.position.y;
                    velocity.y = groundVelocityY + Mathf.Clamp(gap / dt, -CarrierCatchUp, CarrierCatchUp);
                    return;
                }

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

            // A jump out of the ink leaves it at full speed; the ink doesn't hold the letter back on the way up.
            if (ink != null && InkDepth > 0f && !risingFromJump)
            {
                Swim(ref velocity, dt);
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
