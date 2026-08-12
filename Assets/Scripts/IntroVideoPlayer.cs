using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace IceEscape
{
    [RequireComponent(typeof(VideoPlayer))]
    public class IntroVideoPlayer : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private VideoClip introClip;
        [SerializeField] private string streamingAssetsFileName = "zzz_intro_mp4.mp4";

        [Header("Flow")]
        [SerializeField] private string nextScene = "Game";
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private float failsafeSeconds = 60f;

        [Header("Display")]
        [SerializeField] private RawImage targetImage;

        private VideoPlayer player;
        private RenderTexture texture;
        private bool advancing;
        private float elapsed;

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();

            if (introClip == null)
            {
#if UNITY_EDITOR
                introClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/zzz_intro_mp4.mp4");
#endif
            }

            if (targetImage == null)
                targetImage = FindFirstObjectByType<RawImage>();

            if (targetImage != null)
            {
                targetImage.color = Color.white;
            }

            player.playOnAwake = false;
            player.isLooping = false;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                player.renderMode = VideoRenderMode.CameraNearPlane;
                player.targetCamera = mainCam;
            }
            else
            {
                player.renderMode = VideoRenderMode.RenderTexture;
            }

            player.audioOutputMode = VideoAudioOutputMode.AudioSource;

            AudioSource speaker = GetComponent<AudioSource>();
            if (speaker == null)
                speaker = gameObject.AddComponent<AudioSource>();

            speaker.playOnAwake = false;
            player.SetTargetAudioSource(0, speaker);

            player.loopPointReached += OnFinished;
            player.errorReceived += OnError;
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.loopPointReached -= OnFinished;
                player.errorReceived -= OnError;
            }

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        private void Start()
        {
            if (introClip != null)
            {
                player.source = VideoSource.VideoClip;
                player.clip = introClip;
            }
            else if (!string.IsNullOrWhiteSpace(streamingAssetsFileName))
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
                player.source = VideoSource.Url;
                player.url = path;
            }
            else
            {
                Debug.LogWarning("[IntroVideoPlayer] No clip set; advancing to " + nextScene + ".", this);
                Advance();
                return;
            }

            player.prepareCompleted += OnPrepared;
            player.Prepare();
        }

        private void OnPrepared(VideoPlayer source)
        {
            source.prepareCompleted -= OnPrepared;

            if (player.renderMode == VideoRenderMode.RenderTexture)
            {
                texture = new RenderTexture((int)source.width, (int)source.height, 0);
                source.targetTexture = texture;

                if (targetImage != null)
                {
                    targetImage.texture = texture;
                    targetImage.color = Color.white;
                }
            }
            else if (targetImage != null)
            {
                targetImage.gameObject.SetActive(false); // Hide black UI overlay if using CameraNearPlane
            }

            source.Play();
        }

        private void Update()
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (elapsed > failsafeSeconds)
            {
                Advance();
                return;
            }

            if (allowSkip && AnyInputThisFrame())
                Advance();
        }

        private static bool AnyInputThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            return false;
#else
            return Input.anyKeyDown;
#endif
        }

        private void OnFinished(VideoPlayer source)
        {
            Advance();
        }

        private void OnError(VideoPlayer source, string message)
        {
            Debug.LogWarning("[IntroVideoPlayer] Video failed (" + message + "). Skipping to " + nextScene, this);
            Advance();
        }

        private void Advance()
        {
            if (advancing)
                return;

            advancing = true;

            if (Application.CanStreamedLevelBeLoaded(nextScene))
            {
                SceneManager.LoadScene(nextScene);
            }
            else
            {
                Debug.LogError("[IntroVideoPlayer] Scene '" + nextScene + "' is not in Build Settings.", this);
            }
        }
    }
}
