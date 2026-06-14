using DG.Tweening;
using UnityEngine;

public class GameStarter : MonoBehaviour
{
    [SerializeField] SockSpawner spawner;
    [SerializeField] CanvasGroup keyArtCanvas;
    [SerializeField] float fadeDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] float audioDelay = 1f;

    void Start()
    {
        spawner.OnSpawnComplete += HandleSpawnComplete;
        spawner.BeginSpawn();
        audioSource.PlayDelayed(audioDelay);
    }

    void OnDestroy()
    {
        spawner.OnSpawnComplete -= HandleSpawnComplete;
    }

    void HandleSpawnComplete()
    {
        keyArtCanvas.DOFade(0f, fadeDuration)
            .OnComplete(() => keyArtCanvas.blocksRaycasts = false);
    }
}
