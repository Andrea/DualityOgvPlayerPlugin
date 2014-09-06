using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Duality;
using Duality.Components;
using Duality.Drawing;
using Duality.Resources;
using OpenTK.Graphics.OpenGL;

namespace OgvPlayer
{
    [Serializable]
    public class OgvComponent : Renderer, ICmpInitializable, ICmpUpdatable
    {
        private string _fileName;
        [NonSerialized]
        private IntPtr _theoraDecoder;
        [NonSerialized]
        private IntPtr _videoStream;
        [NonSerialized]
        private IntPtr _previousFrame;
        [NonSerialized]
        private float _internalFps = 0.0f;
        [NonSerialized]
        private TheoraPlay.THEORAPLAY_VideoFrame _nextVideo;
        [NonSerialized]
        private TheoraPlay.THEORAPLAY_VideoFrame _currentVideo;
        [NonSerialized]
        private float _elapsedFrameTime;
        [NonSerialized]
        private double _startTime;

        [NonSerialized]
        private Texture _textureOne;
        [NonSerialized]
        private Texture _textureTwo;
        [NonSerialized]
        private Texture _textureThree;

	    
	    private bool _videoDisposed;
		[NonSerialized]
	    private CancellationTokenSource _cancellationTokenSource;

	    public int Width { get; private set; }

        public int Height { get; private set; }

        public string FileName
        {
            get { return _fileName; }
            set
            {
                if (!value.StartsWith("video", StringComparison.OrdinalIgnoreCase))
                {
                    value = "video\\" + value.TrimStart('\\');
                }
                _fileName = value;
            }
        }
		
	    public MediaState State { get; private set; }

	    public bool IsDisposed { get; set; }

	    public ContentRef<Material> Material { get; set; }

	    public float FramesPerSecond
        {
            get { return _internalFps; }
            internal set { _internalFps = value; }
        }

	    public override float BoundRadius
	    {
		    get { return float.MaxValue; }
	    }

	    public void OnInit(InitContext context)
        {
            if (context != InitContext.Activate || DualityApp.ExecContext == DualityApp.ExecutionContext.Editor)
                return;

            _theoraDecoder = IntPtr.Zero;
            _videoStream = IntPtr.Zero;

            FmodTheoraStream.Init();

            if (!string.IsNullOrEmpty(_fileName)) //TODO: Check it exists too? AM
                Initialize();

            _textureOne = new Texture(Width, Height, format: PixelInternalFormat.Luminance);
            _textureTwo = new Texture(Width / 2, Height / 2, format: PixelInternalFormat.Luminance);
            _textureThree = new Texture(Width / 2, Height / 2, format: PixelInternalFormat.Luminance);
        }

	    public void OnShutdown(ShutdownContext context)
        {
	        Terminate();
			IsDisposed = true;
		    _cancellationTokenSource = null;
        }

	    private void Terminate()
	    {
			// Stop and unassign the decoder.
		    if (_theoraDecoder != IntPtr.Zero)
		    {
			    TheoraPlay.THEORAPLAY_stopDecode(_theoraDecoder);
			    _theoraDecoder = IntPtr.Zero;
		    }

		    // Free and unassign the video stream.
		    if (_videoStream != IntPtr.Zero)
		    {
			    TheoraPlay.THEORAPLAY_freeVideo(_videoStream);
			    _videoStream = IntPtr.Zero;
		    }
		    
		    _videoDisposed = true;
		    _startTime = (float) Time.GameTimer.TotalMilliseconds;
	    }

	    internal void Initialize()
        {
            if (!IsDisposed)
            {
                Terminate();
            }

            // Initialize the decoder.
            _theoraDecoder = TheoraPlay.THEORAPLAY_startDecodeFile(
                _fileName,
                150, // Arbitrarily 5 seconds in a 30fps movie.
                //#if !VIDEOPLAYER_OPENGL
                //                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_RGBA
                //#else
                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_IYUV
                //#endif
            );

            // Wait until the decoder is ready.
            while (TheoraPlay.THEORAPLAY_isInitialized(_theoraDecoder) == 0)
            {
                Thread.Sleep(10);
            }

            InitializeVideo();
            _videoDisposed = false;
            IsDisposed = false;
        }

	    private void InitializeVideo()
        {
            // Initialize the video stream pointer and get our first frame.
            if (TheoraPlay.THEORAPLAY_hasVideoStream(_theoraDecoder) != 0)
            {
                while (_videoStream == IntPtr.Zero)
                {
                    _videoStream = TheoraPlay.THEORAPLAY_getVideo(_theoraDecoder);
                    Thread.Sleep(10);
                }

                var frame = TheoraPlay.getVideoFrame(_videoStream);

                // We get the FramesPerSecond from the first frame.
                FramesPerSecond = (float)frame.fps;
                Width = (int)frame.width;
                Height = (int)frame.height;
            }
        }

	    private void DecodeAudio()
        {
            const int bufferSize = 4096 * 2;

			
            while (true)
            {

				if (_cancellationTokenSource.Token.IsCancellationRequested)
					break;
				if(State != MediaState.Playing)
					continue;
                while (TheoraPlay.THEORAPLAY_availableAudio(_theoraDecoder) == 0)
                    ;

                var data = new List<float>();
                TheoraPlay.THEORAPLAY_AudioPacket currentAudio;
                while (data.Count < bufferSize && TheoraPlay.THEORAPLAY_availableAudio(_theoraDecoder) > 0)
                {
                    var audioPtr = TheoraPlay.THEORAPLAY_getAudio(_theoraDecoder);
                    currentAudio = TheoraPlay.getAudioPacket(audioPtr);
                    data.AddRange(TheoraPlay.getSamples(currentAudio.samples, currentAudio.frames * currentAudio.channels));
                    TheoraPlay.THEORAPLAY_freeAudio(audioPtr);
                }

                if (State == MediaState.Playing)
                    FmodTheoraStream.Stream(data.ToArray());
            }
        }


	    public void OnUpdate()
        {
			if(State != MediaState.Playing)
				return;
            if (Time.GameTimer.TotalMilliseconds - _startTime < 800)
                return;

            _elapsedFrameTime += Time.LastDelta * Time.TimeScale;

            UpdateVideo();
        }

	    private void UpdateVideo()
	    {
		    bool missedFrame = false;
		    while (_currentVideo.playms <= _elapsedFrameTime && !missedFrame)
		    {
			    _currentVideo = _nextVideo;
			    var nextFrame = TheoraPlay.THEORAPLAY_getVideo(_theoraDecoder);

			    if (nextFrame != IntPtr.Zero)
			    {
				    TheoraPlay.THEORAPLAY_freeVideo(_previousFrame);
				    _previousFrame = _videoStream;
				    _videoStream = nextFrame;
				    _nextVideo = TheoraPlay.getVideoFrame(_videoStream);
				    missedFrame = false;
			    }
			    else
			    {
				    missedFrame = true;
			    }
		    }
	    }

	    public void Stop()
        {
            if (IsDisposed)
                return;
		    if (State == MediaState.Stopped)
			    return;
			_cancellationTokenSource.Cancel();
		    FmodTheoraStream.Stop();
		    Terminate();
		    State = MediaState.Stopped;

		    IsDisposed = false;
			
            Log.Editor.Write("Theora player stopped!");
        }

	    public void Play()
        {
            if (IsDisposed)
                return;
			
		    if (State != MediaState.Stopped)
			    return;

			if (_cancellationTokenSource == null || _cancellationTokenSource.Token.IsCancellationRequested)
	        {
				_cancellationTokenSource = new CancellationTokenSource();
				Task.Factory.StartNew(DecodeAudio, _cancellationTokenSource.Token);
	        }
	       
			if (_videoDisposed)
		        Initialize();

	        State = MediaState.Playing;
            _startTime = (float)Time.GameTimer.TotalMilliseconds;
        }

	    public override void Draw(IDrawDevice device)
        {
            if (_currentVideo.playms == 0)
                return;

            if(State!= MediaState.Playing)
                return;
            
            Texture.Bind(_textureOne);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height, PixelFormat.Luminance, PixelType.UnsignedByte, _currentVideo.pixels);

            Texture.Bind(_textureTwo);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width / 2, Height / 2, PixelFormat.Luminance, PixelType.UnsignedByte,
               new IntPtr(
                   _currentVideo.pixels.ToInt64() +
                   (_currentVideo.width * _currentVideo.height)
               ));

            Texture.Bind(_textureThree);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width / 2, Height / 2, PixelFormat.Luminance, PixelType.UnsignedByte,
                new IntPtr(
                    _currentVideo.pixels.ToInt64() +
                    (_currentVideo.width * _currentVideo.height) +
                    (_currentVideo.width / 2 * _currentVideo.height / 2)
                ));

            var drawTechnique = (OgvDrawTechnique)Material.Res.Technique.Res;
            drawTechnique.TextureOne = _textureOne;
            drawTechnique.TextureTwo = _textureTwo;
            drawTechnique.TextureThree = _textureThree;

            var targetRect = new Rect(device.TargetSize);
            device.AddVertices(Material, VertexMode.Quads,
                new VertexC1P3T2(targetRect.MinimumX, targetRect.MinimumY, 0.0f, 0.0f, 0.0f),
                new VertexC1P3T2(targetRect.MaximumX, targetRect.MinimumY, 0.0f, 1, 0.0f),
                new VertexC1P3T2(targetRect.MaximumX, targetRect.MaximumY, 0.0f, 1, 1),
                new VertexC1P3T2(targetRect.MinimumX, targetRect.MaximumY, 0.0f, 0.0f, 1));
        }
    }
}
