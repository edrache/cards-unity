using System.Collections;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

public class SockSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] sockPrefabs;
    [SerializeField] BoxCollider spawnZone;
    [SerializeField] Material sourceMaterial;
    [SerializeField] string spawnActionName = "SpawnSock";

    [Header("Pairs")]
    [SerializeField] int pairCount = 5;
    [SerializeField] float spawnInterval = 0.5f;

    [Header("Textures")]
    [SerializeField] Texture2D[] albedoTextures;
    [SerializeField] Texture2D[] detailMapTextures;

    static readonly int BaseColorId      = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId          = Shader.PropertyToID("_Color");
    static readonly int DetailMapColorId = Shader.PropertyToID("_DetailMapColor");
    static readonly int BaseMapId        = Shader.PropertyToID("_BaseMap");
    static readonly int DetailMapId      = Shader.PropertyToID("_DetailMap");

    Player _player;
    bool _spawning;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    void Update()
    {
        if (!_spawning && _player.GetButtonDown(spawnActionName))
            StartCoroutine(SpawnAll());
    }

    IEnumerator SpawnAll()
    {
        if (sockPrefabs == null || sockPrefabs.Length == 0 || spawnZone == null || sourceMaterial == null)
            yield break;

        _spawning = true;

        var queue = BuildShuffledQueue();

        foreach (Material mat in queue)
        {
            SpawnSock(mat);
            yield return new WaitForSeconds(spawnInterval);
        }

        _spawning = false;
    }

    List<Material> BuildShuffledQueue()
    {
        var list = new List<Material>(pairCount * 2);

        for (int i = 0; i < pairCount; i++)
        {
            float hue    = (float)i / pairCount;
            float detHue = (hue + 0.5f) % 1f;

            Material mat = new Material(sourceMaterial);
            Color mainColor   = Color.HSVToRGB(hue,    0.65f, 0.9f);
            Color detailColor = Color.HSVToRGB(detHue, 0.55f, 1.0f);

            mat.SetColor(BaseColorId,      mainColor);
            mat.SetColor(ColorId,          mainColor);
            mat.SetColor(DetailMapColorId, detailColor);

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
        Bounds b = spawnZone.bounds;
        Vector3 pos = new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z)
        );
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject go = Instantiate(sockPrefabs[Random.Range(0, sockPrefabs.Length)], pos, rot);

        Sock sock = go.GetComponent<Sock>();
        if (sock != null) sock.SetMaterial(mat);
    }
}
