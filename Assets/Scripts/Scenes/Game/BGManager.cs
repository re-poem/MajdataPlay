using Cysharp.Threading.Tasks;
using MajdataPlay.Extensions;
using MajdataPlay.IO;
using MajdataPlay.Scenes.View;
using MajdataPlay.Utils;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;
#if UNITY_STANDALONE_WIN
using LibVLCSharp;
#endif
using UnityEngine.UI;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
#nullable enable
namespace MajdataPlay.Scenes.Game
{
    public class BGManager : MonoBehaviour
    {
        public float CurrentSec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if UNITY_STANDALONE_WIN
                return _videoPlayer.Time / 1000f;
#else
                return (float)_videoPlayer.time;
#endif
            }
        }
        public TimeSpan MediaLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return TimeSpan.FromMilliseconds(_mediaLengthMs);
            }
        }

        [SerializeField]
        GameObject _CLVideosCanvas;
        [SerializeField]
        GameObject _CLVideoPrefab;

        [SerializeField]
        Vector3 _defaultScale;
        

        [SerializeField]
        RawImage _videoRenderer;
        [SerializeField]
        List<RawImage?> _CLVideoRenderer;
        [SerializeField]
        Sprite _defaultSprite = MajEnv.EmptySongCover;
        // This is the texture libVLC writes to directly. It's private.
        Texture2D? _vlcTexture = null;
        List<Texture2D?> _vlcclvTextures = new();
        // We copy it into this texture which we actually use in unity.
        [SerializeField]
        RenderTexture? _renderTexture = null;
        [SerializeField]
        List<RenderTexture?> _clvRenderTexture = new();

        SpriteRenderer _pictureCover;
        SpriteRenderer _pictureRenderer;

#if UNITY_STANDALONE_WIN
        MediaPlayer _videoPlayer;
        List<MediaPlayer> _CLVideoPlayer = new();
#else
        VideoPlayer _videoPlayer;
        List<VideoPlayer> _CLVideoPlayer = new();
#endif

        // when copying native Texture2D textures to Unity RenderTextures, the orientation mapping is incorrect on Android, so we flip it over.
        [SerializeField]
        bool _flipTextureX = true;
        [SerializeField]
        bool _flipTextureY = true;

        bool _usePictureAsBackground = false;

        long _mediaLengthMs = 0;

        void Awake()
        {
            Majdata<BGManager>.Instance = this;
#if UNITY_STANDALONE_WIN
            _videoPlayer = new MediaPlayer(MajEnv.VLCLibrary)
            {
                FileCaching = 0,
                NetworkCaching = 0,
            };
#else
            _videoPlayer = GetComponent<VideoPlayer>();
#endif

            _pictureCover = GameObject.Find("BackgroundCover").GetComponent<SpriteRenderer>();
            _pictureRenderer = GetComponent<SpriteRenderer>();
            _defaultScale = transform.localScale;
        }
        void OnDestroy()
        {
#if UNITY_STANDALONE_WIN
            MajDebug.LogInfo("[VLC] DestroyMediaPlayer");
            _videoPlayer.Stop();
            _videoPlayer.Dispose();
            foreach (var clv in _CLVideoPlayer)
            {
                clv.Stop();
                clv.Dispose();
            }
#endif
            Majdata<BGManager>.Free();
        }
        [Conditional("UNITY_STANDALONE_WIN")]
        internal void OnLateUpdate()
        {
            if (_usePictureAsBackground)
            {
                return;
            }
#if UNITY_STANDALONE_WIN
            VLCLateUpdate();
            VLCCLVLateUpdate();
#endif
        }

        public void PauseVideo()
        {
            if (_usePictureAsBackground)
            {
                return;
            }

            _videoPlayer.Pause();
            foreach (var clv in _CLVideoPlayer)
                clv.Pause();
        }

        public void StopVideo()
        {
            if (_usePictureAsBackground)
            {
                return;
            }
            _videoPlayer.Stop();
            foreach (var clv in _CLVideoPlayer)
                clv.Stop();
        }
        public void PlayVideo(float time,float speed)
        {
            if (_usePictureAsBackground)
            {
                return;
            }
#if UNITY_STANDALONE_WIN
            _videoPlayer.SetRate(speed);
            _videoPlayer.SeekTo(TimeSpan.FromSeconds(time));
            _videoPlayer.Play();
            foreach (var clv in _CLVideoPlayer)
            {
                clv.SetRate(speed);
                clv.SeekTo(TimeSpan.FromSeconds(time));
                clv.Play();
            }
#else
            _videoPlayer.playbackSpeed = speed;
            _videoPlayer.time = time;
            _videoPlayer.Play();
#endif
        }

        public void SetVideoSpeed(float speed)
        {
            if (_usePictureAsBackground)
            {
                return;
            }
#if UNITY_STANDALONE_WIN
            _videoPlayer.SetRate(speed);
            foreach (var clv in _CLVideoPlayer)
                clv.SetRate(speed);
#else
            _videoPlayer.playbackSpeed = speed;
#endif
        }

        public void SetBackgroundPic(Sprite? sprite)
        {
            DisableVideo();
            if (sprite is null) 
            { 
                _pictureRenderer.sprite = _defaultSprite;
                transform.localScale = _defaultScale;
                return; 
            }
            _pictureRenderer.sprite = sprite;
            //todo:set correct scale
            var scale = 1080f / sprite.texture.width;
            gameObject.transform.localScale = new Vector3(scale, scale, scale);
        }

        public void DisableVideo()
        {
            if (_usePictureAsBackground)
            {
                return;
            }
            _usePictureAsBackground = true;
            //Disable rawimage optional
#if UNITY_STANDALONE_WIN
            _videoPlayer.Media = null;
            foreach (var clv in _CLVideoPlayer)
                clv.Media = null;
#else
            _videoPlayer.url = null;
            _videoPlayer.Stop();
#endif
        }
        public void SetBackgroundDim(float dim)
        {
            _pictureCover.color = new Color(0f, 0f, 0f, dim);
        }

        public async UniTask SetMovieAsync(string path, Sprite? fallback)
        {
#if UNITY_STANDALONE_WIN // VLC Unity
            try
            {

                MajDebug.LogInfo("[VLC] LoadMedia");
                if (_videoPlayer.Media != null)
                {
                    _videoPlayer.Media.Dispose();
                }

                var trimmedPath = path.Trim(new char[] { '"' });//Windows likes to copy paths with quotes but Uri does not like to open them
                var uri = new Uri(trimmedPath);
                MajDebug.LogInfo("[VLC] Uri: " + uri.ToString());
                var media = new Media(uri);
                //media.AddOption(":start-paused");
                _videoPlayer.Media = media;


                MajDebug.LogInfo("[VLC] BeginParse");
                var ret = await _videoPlayer.Media.ParseAsync(MajEnv.VLCLibrary!);
                if(ret != MediaParsedStatus.Done)
                {
                    SetBackgroundPic(fallback);
                    return;
                }
                MajDebug.LogInfo("[VLC] " + ret);
                _pictureRenderer.forceRenderingOff = true;
                _usePictureAsBackground = false;
                _videoPlayer.Play();
                _mediaLengthMs = media.Duration;
                await UniTask.Delay(200);
                _videoPlayer.Pause();
                _videoPlayer.SeekTo(TimeSpan.Zero);
            }
            catch(Exception e)
            {
                MajDebug.LogException(e);
                SetBackgroundPic(fallback);
            }
#else // Unity VideoPlayer
            _videoPlayer.url = "file://" + path;
            _videoPlayer.Prepare();
            var startAt = MajTimeline.UnscaledTime;
            var timeout = TimeSpan.FromSeconds(15);
            while (true)
            {
                try
                {
                    var remainingTime = timeout - (MajTimeline.UnscaledTime - startAt);
                    if (remainingTime.TotalSeconds < 0)
                    {
                        MajDebug.LogError("Video loading timeout, fall back to default cover".i18n());
                        SetBackgroundPic(fallback);
                        return;
                    }
                    if (_videoPlayer.isPrepared)
                        break;
                }
                finally
                {
                    await UniTask.Yield();
                }
            }
            _pictureRenderer.forceRenderingOff = true;
            _videoRenderer.texture = _videoPlayer.texture;
            var scale = (float)_videoPlayer.height / (float)_videoPlayer.width;
            _videoRenderer.gameObject.transform.localScale = new Vector3(1f, scale, 1f);
            _mediaLengthMs = (long)(_videoPlayer.length * 1000);
#endif
        }

        public async UniTask SetCLVideoAsync(CLVideo[] clvideos)
        {
#if UNITY_STANDALONE_WIN // VLC Unity
            try
            {
                MajDebug.LogInfo("[VLC_CLV] LoadCLVs");
                foreach (var vid in _CLVideoPlayer)
                    if (vid.Media != null)
                        vid.Media.Dispose();

                foreach (var clv in clvideos)
                {
                    var trimmedPath = clv.FullName.Trim(new char[] { '"' });//Windows likes to copy paths with quotes but Uri does not like to open them
                    var uri = new Uri(trimmedPath);
                    MajDebug.LogInfo("[VLC_CLV] Uri: " + uri.ToString());

                    var media = new Media(uri);
                    var player  = new MediaPlayer(MajEnv.VLCLibrary)
                    {
                        FileCaching = 0,
                        NetworkCaching = 0,
                        Media = media
                    };

                    MajDebug.LogInfo("[VLC_CLV] BeginParse");
                    var ret = await player.Media.ParseAsync(MajEnv.VLCLibrary!);
                    if (ret != MediaParsedStatus.Done)
                    {
                        MajDebug.LogError("[VLC_CLV] Failed to parse CLV: " + clv.FullName);
                        continue;
                    }
                    MajDebug.LogInfo("[VLC_CLV] " + ret);
                    _CLVideoPlayer.Add(player);
                    _vlcclvTextures.Add(null);
                    _clvRenderTexture.Add(null);

                    GameObject GOCLVideo = Instantiate(_CLVideoPrefab);
                    GOCLVideo.transform.SetParent(_CLVideosCanvas.transform, false);
                    GOCLVideo.transform.position = Vector3.zero;
                    ((RectTransform)GOCLVideo.transform).sizeDelta = new Vector2(1080, 1080);
                    var ri = GOCLVideo.GetComponent<RawImage>();
                    var cv = GOCLVideo.GetComponent<Canvas>();
                    cv.overrideSorting = true;
                    cv.sortingLayerID = clv.LayerID;
                    cv.sortingOrder = clv.Order;
                    _CLVideoRenderer.Add(ri);

                    player.Play();
                    await UniTask.Delay(200);
                    player.Pause();
                    player.SeekTo(TimeSpan.Zero);
                }
            }
            catch (Exception e)
            {
                MajDebug.LogException(e);
            }
#else // Unity VideoPlayer

#endif
        }


#if UNITY_STANDALONE_WIN
        void VLCLateUpdate()
        {
            if (_videoPlayer is null)
            {
                return;
            }
            if (!_videoPlayer.IsPlaying)
            {
                return;
            }

            //Get size every frame
            uint height = 0;
            uint width = 0;
            IntPtr texPtr;
            _videoPlayer.Size(0, ref width, ref height);
            //Update the vlc texture (tex)
            texPtr = _videoPlayer.GetTexture(width, height, out var isUpdated);

            if (!isUpdated)
            {
                return;
            }

            if (_vlcTexture is null || _vlcTexture.width != width || _vlcTexture.height != height)
            {
                var px = width;
                var py = height;
                //If the currently playing video uses the Bottom Right orientation, we have to do this to avoid stretching it.
                if (GetVideoOrientation() == VideoOrientation.BottomRight)
                {
                    uint swap = px;
                    px = py;
                    py = swap;
                }

                var scale = (float)py / (float)px;

                //Make a texture of the proper size for the video to output to
                _vlcTexture = Texture2D.CreateExternalTexture((int)px, (int)py, TextureFormat.RGBA32, false, true, texPtr);
                //Make a renderTexture the same size as vlctex
                _renderTexture = new RenderTexture(_vlcTexture.width, _vlcTexture.height, 0, RenderTextureFormat.ARGB32);

                _videoRenderer.texture = _renderTexture;
                _videoRenderer.gameObject.transform.localScale = new Vector3(1f, scale, 1f);
            }


            _vlcTexture.UpdateExternalTexture(texPtr);

            //Copy the vlc texture into the output texture, automatically flipped over
            var flip = new Vector2(_flipTextureX ? -1 : 1, _flipTextureY ? -1 : 1);
            //If you wanted to do post processing outside of VLC you could use a shader here.
            Graphics.Blit(_vlcTexture, _renderTexture, flip, Vector2.zero);
        }

        void VLCCLVLateUpdate()
        {
            for (int i = 0; i < _CLVideoPlayer.Count; i++)
            {
                var clv = _CLVideoPlayer[i];
                var clvTexture = _vlcclvTextures[i];
                var clvRenderTexture = _clvRenderTexture[i];
                var clvRenderer = _CLVideoRenderer[i];
                if (clv is null)
                {
                    return;
                }
                if (!clv.IsPlaying)
                {
                    return;
                }

                //Get size every frame
                uint height = 0;
                uint width = 0;
                IntPtr texPtr;
                clv.Size(0, ref width, ref height);
                //Update the vlc texture (tex)
                texPtr = clv.GetTexture(width, height, out var isUpdated);

                if (!isUpdated)
                {
                    return;
                }

                if (clvTexture is null || clvTexture.width != width || clvTexture.height != height)
                {
                    var px = width;
                    var py = height;
                    //If the currently playing video uses the Bottom Right orientation, we have to do this to avoid stretching it.
                    if (GetCLVideoOrientation(i) == VideoOrientation.BottomRight)
                    {
                        uint swap = px;
                        px = py;
                        py = swap;
                    }

                    var scale = (float)py / (float)px;

                    //Make a texture of the proper size for the video to output to
                    clvTexture = Texture2D.CreateExternalTexture((int)px, (int)py, TextureFormat.RGBA32, false, true, texPtr);
                    //Make a renderTexture the same size as vlctex
                    clvRenderTexture = new RenderTexture(clvTexture.width, clvTexture.height, 0, RenderTextureFormat.ARGB32);

                    clvRenderer.texture = clvRenderTexture;
                    clvRenderer.gameObject.transform.localScale = new Vector3(1f, scale, 1f);
                }


                clvTexture.UpdateExternalTexture(texPtr);

                //Copy the vlc texture into the output texture, automatically flipped over
                var flip = new Vector2(_flipTextureX ? -1 : 1, _flipTextureY ? -1 : 1);
                //If you wanted to do post processing outside of VLC you could use a shader here.
                Graphics.Blit(clvTexture, clvRenderTexture, flip, Vector2.zero);
            }
        }

        public VideoOrientation? GetVideoOrientation()
        {
            var tracks = _videoPlayer?.Tracks(TrackType.Video);

            if (tracks == null || tracks.Count == 0)
                return null;

            var orientation = tracks[0]?.Data.Video.Orientation; //At the moment we're assuming the track we're playing is the first track

            return orientation;
        }

        public VideoOrientation? GetCLVideoOrientation(int index)
        {
            var tracks = _CLVideoPlayer[index]?.Tracks(TrackType.Video);

            if (tracks == null || tracks.Count == 0)
                return null;

            var orientation = tracks[0]?.Data.Video.Orientation; //At the moment we're assuming the track we're playing is the first track

            return orientation;
        }
#endif
    }
}