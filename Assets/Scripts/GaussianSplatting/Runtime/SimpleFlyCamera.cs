using UnityEngine;

namespace GaussianSplatting.Runtime
{
    public sealed class SimpleFlyCamera : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField]
        private float moveSpeed = 3f;

        [SerializeField]
        private float fastMoveMultiplier = 4f;

        [SerializeField]
        private float verticalSpeed = 3f;

        [Header("Look")]
        [SerializeField]
        private float lookSensitivity = 2f;

        [SerializeField]
        private bool requireRightMouseButton = true;

        [Header("Speed Tuning")]
        [SerializeField]
        private float scrollSpeedStep = 0.5f;

        [SerializeField]
        private float minMoveSpeed = 0.1f;

        [SerializeField]
        private float maxMoveSpeed = 100f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
        }

        private void Update()
        {
            UpdateLook();
            UpdateMove();
            UpdateSpeedAdjustment();
        }

        private void UpdateLook()
        {
            bool canLook = !requireRightMouseButton || Input.GetMouseButton(1);
            if (!canLook)
            {
                return;
            }

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            _yaw += mouseX * lookSensitivity;
            _pitch -= mouseY * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateMove()
        {
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                speed *= fastMoveMultiplier;
            }

            Vector3 move = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
                move += transform.forward;

            if (Input.GetKey(KeyCode.S))
                move -= transform.forward;

            if (Input.GetKey(KeyCode.D))
                move += transform.right;

            if (Input.GetKey(KeyCode.A))
                move -= transform.right;

            if (Input.GetKey(KeyCode.E))
                move += transform.up;

            if (Input.GetKey(KeyCode.Q))
                move -= transform.up;

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            transform.position += move * speed * Time.deltaTime;
        }

        private void UpdateSpeedAdjustment()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.0001f)
            {
                return;
            }

            moveSpeed += scroll * scrollSpeedStep;
            moveSpeed = Mathf.Clamp(moveSpeed, minMoveSpeed, maxMoveSpeed);
        }
    }
}
