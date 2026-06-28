using UnityEngine;

public class ParrySuccessEffect : MonoBehaviour
{
    [Header("音效")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip parrySound;

    [Header("生命時間")]
    [SerializeField] private float lifeTime = 0.6f;

    private void Start()
    {
        PlaySound();
        Destroy(gameObject, lifeTime);
    }

    private void PlaySound()
    {
        if (audioSource != null && parrySound != null)
        {
            audioSource.PlayOneShot(parrySound);
        }
    }
}