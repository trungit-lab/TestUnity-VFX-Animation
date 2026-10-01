using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Yeolha.BeltScroll
{
    /// <summary>Monsterball emerges, charges, detonates on contact, retreats, and attacks again.</summary>
    [RequireComponent(typeof(CharacterController), typeof(MonsterballRedStats))]
    public sealed class MonsterballRedController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform target;
        [SerializeField] private MonsterballRedStats stats;

        [Header("CharacterMonsterRedBall Animator states")]
        [SerializeField] private string idleState = "Base Layer.BattleMovement.BattleIdle";
        [SerializeField] private string unburrowState = "Base Layer.Burrow.Unburrow";
        [SerializeField] private string backwalkState = "Base Layer.BattleMovement.BattleWalk";
        [SerializeField] private string leapState = "Base Layer.Backup.NormalAttack.NormalAttack2";
        [SerializeField] private string rollState = "Base Layer.Backup.NormalAttack.NormalAttack1";
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float emergeSeconds = 0.77f;

        [Header("Approach and roll")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 8f;
        [SerializeField, Min(0f)] private float detectionHeight = 3f;
        [SerializeField, Min(0f)] private float stopDistance = 0.9f;
        [SerializeField, Min(0f)] private float rollHitRadius = 0.25f;
        [SerializeField, Min(0.1f)] private float maximumRollSeconds = 2f;
        [SerializeField, Min(0f)] private float burstRecoverySeconds = 0.3f;
        [SerializeField, Min(0f)] private float impactPushSpeed = 7f;
        [SerializeField, Min(0.1f)] private float maximumRetreatSeconds = 2.4f;
        [SerializeField, Min(0f)] private float rollCooldown = 0.6f;
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 360f;
        [SerializeField, Min(0.1f)] private float buriedDepth = 2f;

        [Header("Circle, retreat, and leap")]
        [SerializeField, Min(0.1f)] private float circleSeconds = 1.15f;
        [SerializeField, Min(0.1f)] private float circleSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float retreatDistance = 4.5f;
        [SerializeField, Min(0.1f)] private float leapSeconds = 1.1f;
        [SerializeField, Min(0f)] private float leapHeight = 0.55f;
        [SerializeField, Min(0.1f)] private float maximumLeapDistance = 7f;

        [Header("Fire from VFX Assets_Monster ball")]
        [Tooltip("Optional override. If empty, the included Destructive Elements Flame Simple prefab is loaded.")]
        [SerializeField] private GameObject firePrefab;
        [SerializeField] private Vector3 fireLocalOffset = new Vector3(0f, 0.7f, 0f);
        [SerializeField, Min(0.01f)] private float fireLocalScale = 0.8f;

        public enum StateId { Hidden, Emerging, Idle, Circling, Leaping, Rolling, Retreating }
        public StateId CurrentState => state != null ? state.Id : StateId.Hidden;

        private const string DefaultFireResource = "Monsterball/DestructiveElementsFlameSimple";
        private CharacterController body;
        private MonsterballEffects effects;
        private Collider hurtboxCollider;
        private Vector3 visibleLocalPosition;
        private GameObject fireInstance;
        private AnimationEventHandler animationEventHandler;
        private float nextTargetSearchTime;
        private float nextRollTime;
        private bool hitThisRoll;
        private State state;
        private HiddenState hidden;
        private EmergingState emerging;
        private IdleState idle;
        private CirclingState circling;
        private LeapingState leaping;
        private RollingState rolling;
        private RetreatingState retreating;
        private int orbitSign = 1;
        private float standingRootY;

        private void Reset()
        {
            stats = GetComponent<MonsterballRedStats>();
            animator = GetComponentInChildren<Animator>(true);
            visualRoot = transform.Find("Group");
        }

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            maximumRollSeconds = Mathf.Max(0.1f, maximumRollSeconds);
            emergeSeconds = Mathf.Max(0.01f, emergeSeconds);
            fireLocalScale = Mathf.Max(0.01f, fireLocalScale);
            circleSeconds = Mathf.Max(0.1f, circleSeconds);
            circleSpeed = Mathf.Max(0.1f, circleSpeed);
            retreatDistance = Mathf.Max(0.1f, retreatDistance);
            leapSeconds = Mathf.Max(0.1f, leapSeconds);
            maximumLeapDistance = Mathf.Max(0.1f, maximumLeapDistance);
        }

        private void Awake()
        {
            standingRootY = transform.position.y;
            body = GetComponent<CharacterController>();
            effects = GetComponent<MonsterballEffects>();
            Hurtbox hurtbox = GetComponentInChildren<Hurtbox>(true);
            if (hurtbox != null) hurtboxCollider = hurtbox.GetComponent<Collider>();
            if (stats == null) stats = GetComponent<MonsterballRedStats>();
            stats.Initialize();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (visualRoot == null) visualRoot = transform.Find("Group");
            if (visualRoot == null || visualRoot == transform || animator == null)
            {
                Debug.LogError("Monsterball requires a separate Group visual root and Animator.", this);
                enabled = false;
                return;
            }

            visibleLocalPosition = visualRoot.localPosition;
            CalibrateBuriedDepth();
            animator.applyRootMotion = false;
            if (!ValidateRequiredAnimations())
            {
                enabled = false;
                return;
            }
            animationEventHandler = animator.GetComponent<AnimationEventHandler>();
            if (animationEventHandler == null)
                animationEventHandler = animator.gameObject.AddComponent<AnimationEventHandler>();
            animationEventHandler.OnAnimationEvent += HandleAnimationEvent;
            // The supplied VFX Graph prefab is optional. The particle effects work
            // without the Visual Effect Graph package used by that prefab.
            if (effects == null) CreateFire();
            hidden = new HiddenState(this);
            emerging = new EmergingState(this);
            idle = new IdleState(this);
            circling = new CirclingState(this);
            leaping = new LeapingState(this);
            rolling = new RollingState(this);
            retreating = new RetreatingState(this);
            ChangeState(hidden);
        }

        private void OnEnable()
        {
            if (stats == null) stats = GetComponent<MonsterballRedStats>();
            if (stats != null) stats.Died += OnDied;
        }

        private void OnDisable()
        {
            if (stats != null) stats.Died -= OnDied;
        }

        private void OnDestroy()
        {
            if (animationEventHandler != null)
                animationEventHandler.OnAnimationEvent -= HandleAnimationEvent;
        }

        private void OnDied()
        {
            if (fireInstance != null) fireInstance.SetActive(false);
            if (effects != null) effects.Death();
            enabled = false;
        }

        // The test's damage configuration can subscribe to this cue without changing the clips.
        public event System.Action RollHitRequested;

        private void HandleAnimationEvent(string eventName)
        {
            if (!enabled) return;
            switch (eventName)
            {
                case "TurnOnEffect":
                    Ignite();
                    break;
                case "CheckHit":
                    if (CurrentState == StateId.Rolling)
                        TryRollHit();
                    break;
                // The Unburrow clip fires this at time zero. EmergingState owns the timing.
                case "Unburrow":
                    break;
            }
        }

        private void Update()
        {
            if (state == null) return;
            if (target == null || target == transform || target.IsChildOf(transform)) ResolveTarget();
            state.Tick(Time.deltaTime);
        }

        private void ResolveTarget()
        {
            if (Time.time < nextTargetSearchTime) return;
            nextTargetSearchTime = Time.time + 0.5f;

            Character player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null)
            {
                PlayerController controller = FindFirstObjectByType<PlayerController>();
                if (controller != null)
                {
                    player = controller.ControlledCharacterComponent;
                    if (player == null && controller.ControlledCharacter != null)
                        player = controller.ControlledCharacter.GetComponent<Character>();
                }
            }
            if (player == null)
            {
                Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
                for (int i = 0; i < characters.Length; i++)
                    if (characters[i].Faction == FactionType.Player) { player = characters[i]; break; }
            }
            target = player != null ? player.transform : null;
        }

        private bool PlayerIsClose()
        {
            if (target == null) return false;
            Vector3 delta = target.position - transform.position;
            if (Mathf.Abs(delta.y) > detectionHeight) return false;
            delta.y = 0f;
            return delta.sqrMagnitude <= detectionRadius * detectionRadius;
        }

        private void ChangeState(State next)
        {
            if (state == next) return;
            state?.Exit();
            state = next;
            state.Enter();
        }

        private void Play(string statePath)
        {
            if (animator.HasState(0, Animator.StringToHash(statePath)))
                animator.CrossFadeInFixedTime(statePath, crossFadeSeconds, 0, 0f);
            else
                Debug.LogWarning($"Monsterball Animator state not found: {statePath}", this);
        }

        private bool ValidateRequiredAnimations()
        {
            if (!(animator.runtimeAnimatorController is AnimatorOverrideController overrides))
            {
                Debug.LogError("Monsterball needs the CharacterMonsterRedBall Animator Override Controller.", this);
                return false;
            }

            var clips = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides.overridesCount);
            overrides.GetOverrides(clips);
            bool valid = ValidateClip(clips, "BattleIdle", "MonsterBall_Idle", idleState)
                & ValidateClip(clips, "Unburrow", "MonsterBall_Unburrow", unburrowState)
                & ValidateClip(clips, "WalkBackward", "MonsterBall_backwalk", backwalkState)
                & ValidateClip(clips, "NormalAttack2", "MonsterRedball_Attack2", leapState)
                & ValidateClip(clips, "NormalAttack1", "MonsterRedBall_Attack1", rollState);
            return valid;
        }

        private bool ValidateClip(List<KeyValuePair<AnimationClip, AnimationClip>> clips,
            string originalName, string requiredName, string statePath)
        {
            if (!animator.HasState(0, Animator.StringToHash(statePath)))
            {
                Debug.LogError($"Monsterball Animator state is missing: {statePath}", this);
                return false;
            }

            foreach (var pair in clips)
                if (pair.Key != null && pair.Key.name == originalName &&
                    pair.Value != null && pair.Value.name == requiredName)
                    return true;

            Debug.LogError($"Monsterball Animator override needs {originalName} -> {requiredName}.", this);
            return false;
        }

        private void SetBurrowAmount(float amount)
        {
            visualRoot.localPosition = visibleLocalPosition + Vector3.down * buriedDepth * Mathf.Clamp01(amount);
        }

        private void CalibrateBuriedDepth()
        {
            float groundY = standingRootY +
                (body.center.y - body.height * 0.5f) * transform.lossyScale.y;
            float topY = groundY;
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                    topY = Mathf.Max(topY, renderer.bounds.max.y);
            buriedDepth = Mathf.Max(buriedDepth, topY - groundY + 0.25f);
        }

        private void CreateFire()
        {
            if (firePrefab == null) firePrefab = Resources.Load<GameObject>(DefaultFireResource);
            if (firePrefab == null)
            {
                Debug.LogWarning("Monsterball flame prefab was not found in Resources/Monsterball.", this);
                return;
            }
            fireInstance = Instantiate(firePrefab, visualRoot);
            fireInstance.name = "MonsterballFire";
            fireInstance.transform.localPosition = fireLocalOffset;
            fireInstance.transform.localRotation = Quaternion.identity;
            fireInstance.transform.localScale = Vector3.one * fireLocalScale;
            fireInstance.SetActive(false);
        }

        private void Ignite()
        {
            if (effects != null) effects.SetBurning(true);
            if (fireInstance == null || fireInstance.activeSelf) return;
            fireInstance.SetActive(true);
            foreach (VisualEffect effect in fireInstance.GetComponentsInChildren<VisualEffect>())
                effect.Play();
        }

        private void TryRollHit()
        {
            if (hitThisRoll || target == null) return;
            Character victim = target.GetComponentInParent<Character>();
            if (victim == null || victim.Faction != FactionType.Player || victim.IsDead) return;

            Vector3 offset = victim.transform.position - transform.position;
            offset.y = 0f;
            float range = rollHitRadius + body.radius;
            CharacterController victimBody = victim.GetComponent<CharacterController>();
            if (victimBody != null) range += victimBody.radius;
            if (offset.sqrMagnitude > range * range) return;

            hitThisRoll = true;
            Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            Vector3 hitPoint = victimBody != null
                ? victimBody.ClosestPoint(transform.position)
                : victim.transform.position;
            var damage = new DamageInfo(gameObject, stats.RollDamage, hitPoint, direction);
            damage.Reaction = new HitReactionSpec
            {
                Kind = HitReactionKind.Knockdown,
                HorizontalForce = impactPushSpeed,
                PushDuration = 0.35f
            };
            victim.ReceiveDamage(damage);
            if (effects != null) effects.Impact(hitPoint);
            RollHitRequested?.Invoke();
        }

        #region Movement, Rotation & Animation Synchronization
        /// <summary>
        /// Xoay quái vật hướng về vector hướng xác định (tách biệt hoàn toàn với di chuyển).
        /// </summary>
        public void RotateTowards(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnDegreesPerSecond * dt);
        }

        /// <summary>
        /// Xoay quái vật quay mặt về phía mục tiêu (Target/Player) — được gọi tách biệt.
        /// </summary>
        public void FaceTarget(float dt)
        {
            if (target == null) return;
            Vector3 look = target.position - transform.position;
            RotateTowards(look, dt);
        }

        /// <summary>
        /// Xoay quái vật quay theo hướng di chuyển — được gọi tách biệt.
        /// </summary>
        public void FaceDirection(Vector3 direction, float dt)
        {
            RotateTowards(direction, dt);
        }

        /// <summary>
        /// Thực hiện di chuyển vị trí quái vật bằng CharacterController (tách biệt với logic xoay).
        /// </summary>
        public void Move(Vector3 horizontalVelocity, float dt)
        {
            if (body == null || !body.enabled) return;
            Vector3 motion = (horizontalVelocity + Vector3.down * 0.2f) * dt;
            body.Move(motion);
        }

        /// <summary>
        /// Đồng bộ Animator khi di chuyển: cập nhật liên tục vận tốc thực tế,
        /// hướng di chuyển tương đối (RelativeForward / RelativeLateral), MoveSpeed và WalkSpeedMultiplier.
        /// </summary>
        public void SyncMovementAnimation(Vector3 worldVelocity, float baseSpeed = 2.5f)
        {
            if (animator == null) return;

            worldVelocity.y = 0f;
            float currentSpeed = worldVelocity.magnitude;
            bool isMoving = currentSpeed > 0.05f;

            animator.SetBool(AnimParams.Walking, isMoving);
            animator.SetBool(AnimParams.BattleMode, true);
            animator.SetBool(AnimParams.Grounded, true);
            animator.SetFloat(AnimParams.MoveSpeed, currentSpeed);

            if (isMoving)
            {
                // Vector di chuyển tương đối so với hướng mặt hiện tại của quái vật
                Vector3 localDir = transform.InverseTransformDirection(worldVelocity.normalized);
                animator.SetFloat(AnimParams.RelForward, Mathf.Clamp(localDir.z, -1f, 1f));
                animator.SetFloat(AnimParams.RelLateral, Mathf.Clamp(localDir.x, -1f, 1f));

                // Điều chỉnh tốc độ phát animation để bước chân đồng bộ hoàn toàn với tốc độ di chuyển thực tế
                float speedMultiplier = baseSpeed > 0.01f ? currentSpeed / baseSpeed : 1f;
                animator.SetFloat(AnimParams.WalkSpeedMultiplier, Mathf.Clamp(speedMultiplier, 0.5f, 2.5f));
            }
            else
            {
                animator.SetFloat(AnimParams.RelForward, 0f);
                animator.SetFloat(AnimParams.RelLateral, 0f);
                animator.SetFloat(AnimParams.WalkSpeedMultiplier, 1f);
            }
        }
        #endregion

        private abstract class State
        {
            protected readonly MonsterballRedController owner;
            protected float elapsed;
            public abstract StateId Id { get; }
            protected State(MonsterballRedController owner) { this.owner = owner; }
            public virtual void Enter() { elapsed = 0f; }
            public virtual void Exit() { }
            public abstract void Tick(float dt);
        }

        private sealed class HiddenState : State
        {
            public override StateId Id => StateId.Hidden;
            public HiddenState(MonsterballRedController owner) : base(owner) { }
            public override void Enter()
            {
                base.Enter();
                owner.SyncMovementAnimation(Vector3.zero);
                owner.SetBurrowAmount(1f);
                owner.body.enabled = false;
                if (owner.hurtboxCollider != null) owner.hurtboxCollider.enabled = false;
                if (owner.fireInstance != null) owner.fireInstance.SetActive(false);
                if (owner.effects != null) owner.effects.SetBurning(false);
                owner.Play(owner.idleState);
            }
            public override void Tick(float dt)
            {
                if (owner.PlayerIsClose()) owner.ChangeState(owner.emerging);
            }
        }

        private sealed class EmergingState : State
        {
            public override StateId Id => StateId.Emerging;
            public EmergingState(MonsterballRedController owner) : base(owner) { }
            public override void Enter()
            {
                base.Enter();
                owner.SyncMovementAnimation(Vector3.zero);
                owner.FaceTarget(100f);
                owner.Play(owner.unburrowState);
                if (owner.effects != null) owner.effects.Emerge();
            }
            public override void Tick(float dt)
            {
                elapsed += dt;
                float progress = Mathf.Clamp01(elapsed / owner.emergeSeconds);
                owner.SetBurrowAmount(1f - Mathf.SmoothStep(0f, 1f, progress));
                if (owner.effects != null) owner.effects.EmergeProgress(progress);
                if (progress >= 0.45f) owner.Ignite();
                if (progress < 1f) return;

                owner.SetBurrowAmount(0f);
                owner.body.enabled = true;
                if (owner.hurtboxCollider != null) owner.hurtboxCollider.enabled = true;
                owner.ChangeState(owner.circling);
            }
        }

        private sealed class IdleState : State
        {
            public override StateId Id => StateId.Idle;
            public IdleState(MonsterballRedController owner) : base(owner) { }
            public override void Enter()
            {
                base.Enter();
                owner.SyncMovementAnimation(Vector3.zero);
                owner.Play(owner.idleState);
            }
            public override void Tick(float dt)
            {
                // Xoay hướng mặt về mục tiêu được gọi tách biệt
                owner.FaceTarget(dt);
                owner.SyncMovementAnimation(Vector3.zero);

                if (Time.time >= owner.nextRollTime && owner.PlayerIsClose() &&
                    (owner.target.position - owner.transform.position).sqrMagnitude >
                    (owner.stopDistance + 0.4f) * (owner.stopDistance + 0.4f))
                    owner.ChangeState(owner.circling);
            }
        }

        private sealed class CirclingState : State
        {
            public override StateId Id => StateId.Circling;
            public CirclingState(MonsterballRedController owner) : base(owner) { }

            public override void Enter()
            {
                base.Enter();
                owner.orbitSign = -owner.orbitSign;
                owner.Play(owner.backwalkState);
            }

            public override void Exit()
            {
                base.Exit();
                owner.SyncMovementAnimation(Vector3.zero);
            }

            public override void Tick(float dt)
            {
                if (owner.target == null) { owner.ChangeState(owner.idle); return; }
                elapsed += dt;

                // 1. Tính toán hướng di chuyển
                Vector3 away = owner.transform.position - owner.target.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f) away = -owner.transform.forward;
                float distance = away.magnitude;
                away /= distance;
                Vector3 tangent = Vector3.Cross(Vector3.up, away) * owner.orbitSign;
                float retreatWeight = distance < owner.retreatDistance ? 0.75f : 0.25f;
                Vector3 direction = (away * retreatWeight + tangent * 0.7f).normalized;
                Vector3 moveVelocity = direction * owner.circleSpeed;

                // 2. Xoay hướng được gọi TÁCH BIỆT (luôn quay mặt về mục tiêu khi khoanh vùng)
                owner.FaceTarget(dt);

                // 3. Thực hiện di chuyển vị trí tách biệt
                owner.Move(moveVelocity, dt);

                // 4. Đồng bộ animation di chuyển liên tục theo vận tốc và hướng di chuyển thực tế
                owner.SyncMovementAnimation(moveVelocity, owner.circleSpeed);

                if (elapsed >= owner.circleSeconds) owner.ChangeState(owner.leaping);
            }
        }

        private sealed class LeapingState : State
        {
            private Vector3 start;
            private Vector3 destination;
            private Vector3 leapDirection;
            private float landingSpeed;
            public override StateId Id => StateId.Leaping;
            public LeapingState(MonsterballRedController owner) : base(owner) { }

            public override void Enter()
            {
                base.Enter();
                owner.SyncMovementAnimation(Vector3.zero);
                start = owner.transform.position;
                start.y = owner.standingRootY;
                Vector3 towardTarget = owner.target != null
                    ? owner.target.position - start : owner.transform.forward * 2f;
                towardTarget.y = 0f;
                float targetRadius = 0.5f;
                if (owner.target != null)
                {
                    CharacterController targetBody = owner.target.GetComponent<CharacterController>();
                    if (targetBody != null) targetRadius = targetBody.radius;
                }
                float safeDistance = owner.body.radius + targetRadius + 0.35f;
                float travelDistance = Mathf.Clamp(towardTarget.magnitude - safeDistance,
                    0f, owner.maximumLeapDistance);
                Vector3 travel = towardTarget.sqrMagnitude > 0.001f
                    ? towardTarget.normalized * travelDistance : Vector3.zero;
                destination = start + travel;
                leapDirection = towardTarget.sqrMagnitude > 0.001f ? towardTarget.normalized : owner.transform.forward;

                // Xoay hướng nhảy TÁCH BIỆT: quay mặt về hướng nhảy
                owner.FaceDirection(leapDirection, 1000f);

                landingSpeed = 0f;
                owner.Play(owner.leapState);
            }

            public override void Tick(float dt)
            {
                elapsed += dt;

                // Xoay hướng trong lúc nhảy được gọi TÁCH BIỆT
                owner.FaceDirection(leapDirection, dt);

                float progress = Mathf.Clamp01(elapsed / owner.leapSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float height = Mathf.Sin(progress * Mathf.PI) *
                    Mathf.Min(owner.leapHeight, owner.body.height * 0.25f);
                Vector3 desired = Vector3.Lerp(start, destination, eased);
                desired.y = start.y + height;
                if (progress < 1f)
                {
                    owner.body.Move(desired - owner.transform.position);
                    return;
                }

                // CharacterController.Move can be blocked during the arc. Keep descending
                // until the actual root reaches its takeoff height before starting the roll.
                landingSpeed = Mathf.Min(landingSpeed + 30f * dt, 20f);
                Vector3 landingDelta = destination - owner.transform.position;
                landingDelta.y = Mathf.Max(start.y - owner.transform.position.y, -landingSpeed * dt);
                owner.body.Move(landingDelta);
                if (elapsed >= owner.leapSeconds + 0.45f &&
                    owner.transform.position.y > start.y + 0.03f)
                {
                    // A moving player can block the capsule on descent. End the attack
                    // on the arena's standing plane rather than hanging above the target.
                    owner.body.enabled = false;
                    Vector3 grounded = owner.transform.position;
                    grounded.y = owner.standingRootY;
                    owner.transform.position = grounded;
                    owner.body.enabled = true;
                }
                if (owner.transform.position.y <= start.y + 0.03f)
                {
                    if (owner.effects != null) owner.effects.Land();
                    owner.ChangeState(owner.rolling);
                }
            }
        }

        private sealed class RollingState : State
        {
            private Vector3 direction;
            private bool detonated;
            private float detonateTime;
            public override StateId Id => StateId.Rolling;
            public RollingState(MonsterballRedController owner) : base(owner) { }
            public override void Enter()
            {
                base.Enter();
                owner.hitThisRoll = false;
                detonated = false;
                if (owner.target != null)
                {
                    direction = owner.target.position - owner.transform.position;
                    direction.y = 0f;
                    direction = direction.sqrMagnitude > 0.001f ? direction.normalized : owner.transform.forward;
                }
                else direction = owner.transform.forward;

                // Xoay hướng lăn TÁCH BIỆT
                owner.FaceDirection(direction, 1000f);
                owner.Play(owner.rollState);
            }

            public override void Exit()
            {
                base.Exit();
                owner.SyncMovementAnimation(Vector3.zero);
            }

            public override void Tick(float dt)
            {
                elapsed += dt;
                if (detonated)
                {
                    owner.SyncMovementAnimation(Vector3.zero);
                    if (elapsed - detonateTime >= owner.burstRecoverySeconds)
                        owner.ChangeState(owner.retreating);
                    return;
                }

                // 1. Tính toán hướng lăn về phía người chơi
                if (owner.target != null)
                {
                    Vector3 toPlayer = owner.target.position - owner.transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.001f)
                        direction = Vector3.RotateTowards(direction, toPlayer.normalized,
                            owner.turnDegreesPerSecond * Mathf.Deg2Rad * dt, 0f).normalized;
                }

                // 2. Xoay hướng được gọi TÁCH BIỆT: xoay theo hướng lăn
                owner.FaceDirection(direction, dt);

                // 3. Thực hiện di chuyển lăn vị trí
                Vector3 rollVelocity = direction * owner.stats.RollSpeed;
                owner.Move(rollVelocity, dt);

                if (owner.effects != null) owner.effects.RollTrail();
                owner.TryRollHit();
                if (owner.hitThisRoll || elapsed >= owner.maximumRollSeconds)
                    Detonate();
            }

            private void Detonate()
            {
                detonated = true;
                detonateTime = elapsed;
                owner.SyncMovementAnimation(Vector3.zero);
                if (owner.effects != null) owner.effects.Detonate();
                owner.nextRollTime = Time.time + owner.rollCooldown;
            }
        }

        private sealed class RetreatingState : State
        {
            public override StateId Id => StateId.Retreating;
            public RetreatingState(MonsterballRedController owner) : base(owner) { }

            public override void Enter()
            {
                base.Enter();
                owner.Play(owner.backwalkState);
            }

            public override void Exit()
            {
                base.Exit();
                owner.SyncMovementAnimation(Vector3.zero);
            }

            public override void Tick(float dt)
            {
                if (owner.target == null) { owner.ChangeState(owner.idle); return; }
                elapsed += dt;
                Vector3 away = owner.transform.position - owner.target.position;
                away.y = 0f;
                float distance = away.magnitude;
                if ((distance >= owner.retreatDistance && elapsed >= 0.35f) ||
                    elapsed >= owner.maximumRetreatSeconds)
                {
                    owner.ChangeState(owner.idle);
                    return;
                }
                away = distance > 0.001f ? away / distance : -owner.transform.forward;
                Vector3 moveVelocity = away * owner.circleSpeed;

                // 1. Xoay hướng được gọi TÁCH BIỆT: quái lùi nhưng mặt vẫn hướng về người chơi
                owner.FaceTarget(dt);

                // 2. Thực hiện di chuyển lùi vị trí
                owner.Move(moveVelocity, dt);

                // 3. Đồng bộ animation di chuyển lùi liên tục
                owner.SyncMovementAnimation(moveVelocity, owner.circleSpeed);
            }
        }
    }
}
