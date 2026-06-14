using System.Collections;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

public class SockSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] sockPrefabs;
    [SerializeField] BoxCollider[] spawnZones;
    [SerializeField] Material sourceMaterial;
    [SerializeField] string spawnActionName = "SpawnSock";

    [Header("Pairs")]
    [SerializeField] int pairCount = 5;
    [SerializeField] float spawnInterval = 0.5f;

    [Header("Colors")]
    [SerializeField] Color[] sockColors;

    [Header("Textures")]
    [SerializeField] Texture2D[] albedoTextures;
    [SerializeField] Texture2D[] detailMapTextures;

    static readonly int DetailMapColorId = Shader.PropertyToID("_DetailMapColor");
    static readonly int BaseMapId        = Shader.PropertyToID("_BaseMap");
    static readonly int DetailMapId      = Shader.PropertyToID("_DetailMap");

    Player _player;
    bool _spawning;

    public event System.Action OnSpawnComplete;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    public void BeginSpawn()
    {
        if (!_spawning)
            StartCoroutine(SpawnAll());
    }

    void Update()
    {
        if (_player.GetButtonDown(spawnActionName))
            BeginSpawn();
    }

    IEnumerator SpawnAll()
    {
        _spawning = true;

        if (sockPrefabs == null || sockPrefabs.Length == 0 || spawnZones == null || spawnZones.Length == 0 || sourceMaterial == null)
        {
            _spawning = false;
            OnSpawnComplete?.Invoke();
            yield break;
        }

        var queue = BuildShuffledQueue();

        foreach (Material mat in queue)
        {
            SpawnSock(mat);
            yield return new WaitForSeconds(spawnInterval);
        }

        _spawning = false;
        OnSpawnComplete?.Invoke();
    }

    List<Material> BuildShuffledQueue()
    {
        var list = new List<Material>(pairCount * 2);

        for (int i = 0; i < pairCount; i++)
        {
            Material mat = new Material(sourceMaterial);

            if (sockColors != null && sockColors.Length > 0)
                mat.SetColor(DetailMapColorId, sockColors[Random.Range(0, sockColors.Length)]);

            if (albedoTextures != null && albedoTextures.Length > 0)
                mat.SetTexture(BaseMapId, albedoTextures[Random.Range(0, albedoTextures.Length)]);

            if (detailMapTextures != null && detailMapTextures.Length > 0)
                mat.SetTexture(DetailMapId, detailMapTextures[Random.Range(0, detailMapTextures.Length)]);

            list.Add(mat);
            list.Add(mat);
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    void SpawnSock(Material mat)
    {
        Bounds b = spawnZones[Random.Range(0, spawnZones.Length)].bounds;
        Vector3 pos = new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z)
        );
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject prefab = sockPrefabs[Random.Range(0, sockPrefabs.Length)];
        GameObject go     = Instantiate(prefab, pos, rot);

        Sock sock = go.GetComponent<Sock>();
        if (sock != null)
        {
            sock.SourcePrefab = prefab;
            sock.SetMaterial(mat);
        }
    }
}
