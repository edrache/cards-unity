using Rewired;
using UnityEngine;

public class SockSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] sockPrefabs;
    [SerializeField] BoxCollider spawnZone;
    [SerializeField] string spawnActionName = "SpawnSock";

    Player _player;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    void Update()
    {
        if (_player.GetButtonDown(spawnActionName))
            SpawnSock();
    }

    void SpawnSock()
    {
        if (sockPrefabs == null || sockPrefabs.Length == 0 || spawnZone == null) return;

        Bounds b = spawnZone.bounds;
        Vector3 spawnPos = new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z)
        );
        Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Instantiate(sockPrefabs[Random.Range(0, sockPrefabs.Length)], spawnPos, spawnRot);
    }
}
