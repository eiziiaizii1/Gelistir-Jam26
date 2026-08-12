using UnityEngine;

namespace IceEscape
{
    public class IceAudioManager : MonoBehaviour
    {
        private static IceAudioManager instance;
        public static IceAudioManager Instance => instance;

        [Header("Audio Sources")]
        private AudioSource slideAudioSource;
        private AudioSource sfxAudioSource;
        private AudioSource musicAudioSource;

        [Header("Background Music")]
        [Tooltip("Gameplay music. Assign this - an unassigned clip means a silent build, since " +
                 "the editor-only asset lookup does not exist in a player.")]
        [SerializeField] private AudioClip bgmMusicClip;
        [Tooltip("Fallback path under a Resources folder, used only when the clip above is empty.")]
        [SerializeField] private string bgmResourcePath = "Music/GameplayTheme";

        [Header("Procedural Audio Clips")]
        private AudioClip crystalChimeClip;
        private AudioClip explosionClip;
        private AudioClip slapWhooshClip;
        private AudioClip slideNoiseClip;

        private Rigidbody playerRb;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            SetupAudioSources();
            GenerateProceduralAudioClips();

            // Load Master Volume & Mute state from PlayerPrefs
            AudioListener.volume = IsMuted ? 0f : MasterVolume;
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            set
            {
                PlayerPrefs.SetFloat("MasterVolume", Mathf.Clamp01(value));
                AudioListener.volume = IsMuted ? 0f : Mathf.Clamp01(value);
            }
        }

        public static bool IsMuted
        {
            get => PlayerPrefs.GetInt("IsMuted", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("IsMuted", value ? 1 : 0);
                AudioListener.volume = value ? 0f : MasterVolume;
            }
        }

        private void Start()
        {
            playerRb = PlayerLocator.FindPlayerBody();
        }

        private void SetupAudioSources()
        {
            slideAudioSource = gameObject.AddComponent<AudioSource>();
            slideAudioSource.loop = true;
            slideAudioSource.playOnAwake = false;
            slideAudioSource.volume = 0.125f;

            // Halving this halves every one-shot at once: PlayOneShot's volumeScale multiplies
            // the source volume, so the per-clip 0.7 / 0.9 / 0.6 balance is preserved.
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
            sfxAudioSource.loop = false;
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.volume = 0.4f;

            musicAudioSource = gameObject.AddComponent<AudioSource>();
            musicAudioSource.loop = true;
            musicAudioSource.playOnAwake = false;
            musicAudioSource.volume = 0.55f;

            if (bgmMusicClip == null)
            {
                // Resources.Load works in a build; the AssetDatabase lookup below does not.
                bgmMusicClip = Resources.Load<AudioClip>(bgmResourcePath);
            }

#if UNITY_EDITOR
            if (bgmMusicClip == null)
            {
                bgmMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ultrakill_12 novocals kinga.wav");
            }
#endif

            if (bgmMusicClip != null)
            {
                musicAudioSource.clip = bgmMusicClip;
                musicAudioSource.Play();
            }
            else
            {
                // Loud on purpose: this used to fail silently in builds only, because the
                // editor-only lookup above is compiled out of a player.
                Debug.LogWarning("[IceAudioManager] No background music clip. Assign 'Bgm Music " +
                                 "Clip' in the inspector, or put the track at " +
                                 "Assets/Resources/" + bgmResourcePath + ".", this);
            }
        }

        private void GenerateProceduralAudioClips()
        {
            crystalChimeClip = CreateChimeClip();
            explosionClip = CreateExplosionClip();
            slapWhooshClip = CreateWhooshClip();
            slideNoiseClip = CreateSlideNoiseClip();

            if (slideAudioSource != null && slideNoiseClip != null)
            {
                slideAudioSource.clip = slideNoiseClip;
                slideAudioSource.Play();
            }
        }

        private void Update()
        {
            if (playerRb == null)
            {
                playerRb = PlayerLocator.FindPlayerBody();
            }

            if (slideAudioSource != null && playerRb != null)
            {
                float speed = playerRb.linearVelocity.magnitude;
                // Recomputed every frame, so this — not the initial volume above — is what the
                // slide loop actually plays at. Both the ramp and the caps are halved.
                slideAudioSource.volume = Mathf.Clamp(speed / 30f, 0.025f, 0.2f);
                slideAudioSource.pitch = Mathf.Clamp(0.8f + (speed / 20f), 0.8f, 1.8f);
            }
        }

        public void PlayCrystalChime()
        {
            if (sfxAudioSource != null && crystalChimeClip != null)
            {
                sfxAudioSource.PlayOneShot(crystalChimeClip, 0.7f);
            }
        }

        public void PlayExplosion()
        {
            if (sfxAudioSource != null && explosionClip != null)
            {
                sfxAudioSource.PlayOneShot(explosionClip, 0.9f);
            }
        }

        public void PlaySlapWhoosh()
        {
            if (sfxAudioSource != null && slapWhooshClip != null)
            {
                sfxAudioSource.PlayOneShot(slapWhooshClip, 0.6f);
            }
        }

        private AudioClip CreateChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq1 = 880f; // A5
                float freq2 = 1320f; // E6
                float env = Mathf.Exp(-t * 12f);
                data[i] = (Mathf.Sin(2 * Mathf.PI * freq1 * t) + Mathf.Sin(2 * Mathf.PI * freq2 * t) * 0.5f) * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("CrystalChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateExplosionClip()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            System.Random rand = new System.Random();
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6f);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                float subFreq = Mathf.Sin(2 * Mathf.PI * 65f * t);
                data[i] = (noise * 0.7f + subFreq * 0.5f) * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("ExplosionBoom", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateWhooshClip()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            System.Random rand = new System.Random();
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(t / duration * Mathf.PI);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                data[i] = noise * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("SlapWhoosh", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateSlideNoiseClip()
        {
            int sampleRate = 44100;
            float duration = 2.0f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            System.Random rand = new System.Random();
            float lastVal = 0f;
            for (int i = 0; i < samples; i++)
            {
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                // Low-pass filter for smooth sliding hiss
                lastVal = Mathf.Lerp(lastVal, noise, 0.15f);
                data[i] = lastVal * 0.2f;
            }

            AudioClip clip = AudioClip.Create("SlideNoise", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
