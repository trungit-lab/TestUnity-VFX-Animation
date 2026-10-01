using UnityEngine;

namespace Yeolha.BeltScroll
{
    /// <summary>Simple fixed-angle follow camera for the Monsterball test scene.</summary>
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Camera))]
    public sealed class MonsterballTestCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform monsterball;
        [SerializeField] private Vector3 offset = new Vector3(0f, 7.8f, -13.5f);
        [SerializeField, Min(0f)] private float lookHeight = 1.25f;
        [SerializeField, Range(15f, 90f)] private float fieldOfView = 30f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.18f;
        [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.14f;
        [SerializeField, Min(0f)] private float monsterFramingRange = 12f;
        [SerializeField, Min(0f)] private float maximumMonsterOffset = 2.5f;

        private Camera sceneCamera;
        private Vector3 velocity;

        private void Awake()
        {
            sceneCamera = GetComponent<Camera>();
            sceneCamera.fieldOfView = fieldOfView;
            ResolvePlayer();
            if (player != null) SnapToTarget();
        }

        private void LateUpdate()
        {
            if (player == null) ResolvePlayer();
            if (player == null) return;

            sceneCamera.fieldOfView = fieldOfView;
            Vector3 focus = GetFocusPoint();
            Vector3 desiredPosition = focus + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition,
                ref velocity, positionSmoothTime);
            Vector3 aim = focus + Vector3.up * lookHeight - transform.position;
            if (aim.sqrMagnitude < 0.001f) return;
            Quaternion desiredRotation = Quaternion.LookRotation(aim, Vector3.up);
            float blend = 1f - Mathf.Exp(-Time.deltaTime / rotationSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }

        private void ResolvePlayer()
        {
            Character character = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (character == null)
            {
                PlayerController controller = FindFirstObjectByType<PlayerController>();
                if (controller != null) character = controller.ControlledCharacterComponent;
            }
            if (character != null) player = character.transform;
        }

        private Vector3 GetFocusPoint()
        {
            Vector3 focus = player.position;
            if (monsterball == null || monsterFramingRange <= 0f) return focus;
            Vector3 towardMonster = monsterball.position - focus;
            towardMonster.y = 0f;
            float distance = towardMonster.magnitude;
            if (distance > monsterFramingRange || distance < 0.01f) return focus;
            float weight = 1f - distance / monsterFramingRange;
            focus += towardMonster.normalized * Mathf.Min(maximumMonsterOffset,
                distance * 0.3f) * weight;
            return focus;
        }

        private void SnapToTarget()
        {
            Vector3 focus = GetFocusPoint();
            transform.position = focus + offset;
            transform.rotation = Quaternion.LookRotation(
                focus + Vector3.up * lookHeight - transform.position, Vector3.up);
        }
    }
}
