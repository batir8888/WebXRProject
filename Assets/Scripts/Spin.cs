using UnityEngine;

public class Spin : MonoBehaviour
{
  [SerializeField] private float degreesPerSecond = 45f;

  private void Update()
  {
    transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
  }
}
