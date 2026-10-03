using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private Transform cameraHolder;

    [Header("이동 속도")]
    [SerializeField] private float speed = 3f;

    [Header("회전 감도")]
    [SerializeField] private float mouseSensitivity = 150f;

    [Header("상하 각도 제한")]
    [SerializeField] private float minPitch = -85f; // 아래로 내려다보는 최대 각도
    [SerializeField] private float maxPitch = 85f;  // 위로 올려다보는 최대 각도

    private bool MoveInputEnabled = true;
    Rigidbody rb;

    private float xRotation = 0f;


    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 마우스 커서를 게임 화면 중앙에 고정하고 숨김 처리
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraHolder == null)
        {
            cameraHolder = Camera.main.transform;
        }
    }

    void Update()
    {
        if (MoveInputEnabled)
        {
            Rotate();
        }
    }

    void FixedUpdate()
    {
        if (MoveInputEnabled)
        {
            Move();
        }
    }

    private void Move()
    {
        float x = Input.GetAxisRaw("Horizontal"); // A, D
        float z = Input.GetAxisRaw("Vertical");   // W, S

        Vector3 move = transform.right * x + transform.forward * z;
        rb.MovePosition(rb.position + move * speed * Time.fixedDeltaTime);
    }

    private void Rotate()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    public void SetMoveInputEnabled(bool enabled)
    {
        MoveInputEnabled = enabled;
    }
}