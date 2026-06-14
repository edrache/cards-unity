using System.Collections;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] tracks;

    int _currentIndex;

    void Start()
    {
        if (tracks == null || tracks.Length == 0) return;
        StartCoroutine(PlayLoop());
    }

    IEnumerator PlayLoop()
    {
        while (true)
        {
            AudioClip clip = tracks[_currentIndex];
            audioSource.clip = clip;
            audioSource.Play();
            yield return new WaitForSeconds(clip.length);
            _currentIndex = (_currentIndex + 1) % tracks.Length;
        }
    }
}
