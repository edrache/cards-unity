using UnityEngine;

/// <summary>
/// Kliknij LPM w Game view → losowy wariant skarpety spawnie się w tym miejscu.
/// Dodaj ten skrypt na dowolny GameObject w scenie (np. GameManager).
/// Przypisz prefaby w liście sockPrefabs w Inspektorze.
/// </summary>
public class SockSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] sockPrefabs;
    [SerializeField] float spawnHeightOffset = 0.05f;

    Camera _cam;

    void Awake() => _cam = Camera.main;

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (sockPrefabs == null || sockPrefabs.Length == 0) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 spawnPos = hit.point + hit.normal * spawnHeightOffset;
            Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject prefab = sockPrefabs[Random.Range(0, sockPrefabs.Length)];
            Instantiate(prefab, spawnPos, spawnRot);
        }
    }
}
