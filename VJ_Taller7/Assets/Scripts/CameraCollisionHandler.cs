using UnityEngine;
using Unity.Cinemachine;

public class CameraCollisionHandler : MonoBehaviour
{
    [SerializeField] private Transform player; // El personaje al que sigue la c�mara
    [System.Obsolete] public CinemachineCamera freeLookCamera;
    [SerializeField] private LayerMask collisionMask; // Capas con las que la c�mara colisionar�

    private float defaultDistance;
    private Vector3 direction;

    private void Start()
    {
        if (freeLookCamera != null)
        {
            defaultDistance = Vector3.Distance(transform.position, player.position);
            direction = (transform.position - player.position).normalized;
        }
    }

    private void LateUpdate()
    {
        if (freeLookCamera == null || player == null) return;

        RaycastHit hit;
        Vector3 targetPosition = player.position + direction * defaultDistance;

        // Disparar un raycast desde el jugador hacia la c�mara
        if (Physics.Raycast(player.position, direction, out hit, defaultDistance, collisionMask))
        {
            // Si el raycast golpea una pared, ajustamos la posici�n de la c�mara
            transform.position = hit.point;
        }
        else
        {
            // Si no hay colisi�n, la c�mara vuelve a su posici�n normal
            transform.position = targetPosition;
        }
    }
}
