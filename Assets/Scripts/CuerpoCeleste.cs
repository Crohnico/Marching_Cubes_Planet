using UnityEngine;

public class CuerpoCeleste : MonoBehaviour
{
    [Header("Rotación Propia")]
    [Tooltip("Velocidad de rotación sobre su propio eje (Y).")]
    public float velocidadRotacion = 10f;

    [Header("Órbita (Opcional)")]
    [Tooltip("El objeto alrededor del cual va a orbitar.")]
    public Transform centroOrbita;
    [Tooltip("Velocidad a la que orbita alrededor del centro.")]
    public float velocidadOrbita = 20f;

    void Update()
    {
        // 1. Rotación sobre sí mismo (Eje Y)
        transform.Rotate(Vector3.up, velocidadRotacion * Time.deltaTime);

        // 2. Movimiento orbital (si se ha asignado un centro)
        if (centroOrbita != null)
        {
            // Rota la posición de este objeto alrededor del centro en el eje Y
            transform.RotateAround(centroOrbita.position, Vector3.up, velocidadOrbita * Time.deltaTime);
        }
    }
}