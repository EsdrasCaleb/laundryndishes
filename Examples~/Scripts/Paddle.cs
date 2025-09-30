using UnityEngine;

public class Paddle : MonoBehaviour
{
    public float speed = 8f;
    public float xLimit = 3.5f;

    void Update()
    {
        float moveInput = Input.GetAxis("Horizontal");
        Vector2 newPosition = transform.position + new Vector3(moveInput * speed * Time.deltaTime, 0, 0);
        newPosition.x = Mathf.Clamp(newPosition.x, -xLimit, xLimit);
        transform.position = newPosition;
    }
}