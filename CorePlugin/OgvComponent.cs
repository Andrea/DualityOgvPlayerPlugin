using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Duality;
using Duality.Components;
using Duality.Drawing;
using Duality.Editor;
using Duality.Resources;
using OpenTK.Graphics.OpenGL;

namespace OgvPlayer
{

	/// <summary>
	/// HACK this is a hack, it will not render video on android or anything :|
	/// the reason to create this hack was to decide how to play video later
	/// </summary>
	public interface ITheoraVideo : IDisposable
	{
		bool IsFinished { get; }
		int Width { get; }
		int Height { get;}
		bool Disposed { get;  }
		decimal ElapsedMilliseconds { get; set; }
		IntPtr TheoraDecoder { get; }
		void Terminate();
		void InitializeVideo(string fileName);
		void UpdateVideo(float elapsedFrameTime);
		IntPtr GetYColorPlane();
		IntPtr GetCbColorPlane();
		IntPtr GetCrColorPlane();
	}
	[Serializable]
	public class OgvComponent : Renderer, ICmpInitializable, ICmpUpdatable
	{
		private string _fileName;
		[NonSerialized]
		private double _startTime;
		[NonSerialized]
		private Texture _textureOne;
		[NonSerialized]
		private Texture _textureTwo;
		[NonSerialized]
		private Texture _textureThree;
		[NonSerialized]
		private float _elapsedFrameTime;
#if __ANDROID__
		[NonSerialized]
		private ITheoraVideo _theoraVideo;
#else
		[NonSerialized]
		private TheoraVideo _theoraVideo;
#endif
		[NonSerialized]
		private FmodTheoraStream _fmodTheoraStream;
		[NonSerialized]
		private CancellationTokenSource _cancellationTokenSource;
		[NonSerialized]
		private VertexC1P3T2[] _vertices;
		[NonSerialized]
		private MediaState _state;
		[NonSerialized]
		private Canvas _canvas;

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

		[EditorHintFlags(MemberFlags.Invisible)]
		public MediaState State
		{
			get { return _state; }
			private set { _state = value; }
		}

		[EditorHintFlags(MemberFlags.Invisible)]
		public bool IsFinished 
		{ 
			get
		    {
				return _theoraVideo == null || _theoraVideo.IsFinished;
		    }
		}

	    public bool CanRunOnThisArchitecture { get { return !Environment.Is64BitProcess; } }

		public ContentRef<Material> Material { get; set; }

		[EditorHintFlags(MemberFlags.Invisible)]
		public override float BoundRadius
		{
			get
			{
				return Rect.Transform(GameObj.Transform.Scale, GameObj.Transform.Scale).BoundingRadius;
			}
		}

		public Rect Rect { get; set; }
		public ColorRgba ColourTint { get; set; }
		public ScreenAspectOptions ScreenAspectOptions { get; set; }

		public void OnInit(InitContext context)
		{
			if (context != InitContext.Activate || DualityApp.ExecContext == DualityApp.ExecutionContext.Editor)
				return;

			if (string.IsNullOrEmpty(_fileName))
				return;

		    if (Environment.Is64BitProcess)
		    {
		        Log.Editor.WriteWarning("The video player is not supported on 64 bit processes, and this is one.");
                return;
		    }
            Initialize();

			_textureOne = new Texture(_theoraVideo.Width, _theoraVideo.Height);
			_textureTwo = new Texture(_theoraVideo.Width / 2, _theoraVideo.Height / 2);
			_textureThree = new Texture(_theoraVideo.Width / 2, _theoraVideo.Height / 2);
		}

		public void OnShutdown(ShutdownContext context)
		{
			if (context != ShutdownContext.Deactivate)
				return;

			Stop();
			if (_theoraVideo != null && CanRunOnThisArchitecture)
				_theoraVideo.Terminate();

			_cancellationTokenSource = null;
		}

		internal void Initialize()
		{
		    if (_theoraVideo != null) 
                _theoraVideo.Terminate();

		    _fmodTheoraStream = new FmodTheoraStream();
			_fmodTheoraStream.Initialize();
#if !__ANDROID__
			_theoraVideo = new TheoraVideo();
#endif
			_theoraVideo.InitializeVideo(_fileName);
		}

		private void DecodeAudio()
		{
			const int bufferSize = 4096 * 2;
#if !__ANDROID__

			while (State != MediaState.Stopped)
			{
				while (State != MediaState.Stopped && TheoraPlay.THEORAPLAY_availableAudio(_theoraVideo.TheoraDecoder) == 0)
					continue;

				var data = new List<float>();
				TheoraPlay.THEORAPLAY_AudioPacket currentAudio;
				while (data.Count < bufferSize && TheoraPlay.THEORAPLAY_availableAudio(_theoraVideo.TheoraDecoder) > 0)
				{
					var audioPtr = TheoraPlay.THEORAPLAY_getAudio(_theoraVideo.TheoraDecoder);
					currentAudio = TheoraPlay.getAudioPacket(audioPtr);
					data.AddRange(TheoraPlay.getSamples(currentAudio.samples, currentAudio.frames * currentAudio.channels));
					TheoraPlay.THEORAPLAY_freeAudio(audioPtr);
				}

				if (State == MediaState.Playing)
					_fmodTheoraStream.Stream(data.ToArray());
			}
#endif
		}

		public void OnUpdate()
		{
			if (State != MediaState.Playing)
				return;
			if (Time.GameTimer.TotalMilliseconds - _startTime < 800)
				return;
			if(!CanRunOnThisArchitecture)
                return;
			_elapsedFrameTime += Time.LastDelta * Time.TimeScale;

		    if (_theoraVideo != null && CanRunOnThisArchitecture )
		    {
		        _theoraVideo.UpdateVideo(_elapsedFrameTime);
		        if(_theoraVideo.IsFinished)
		            Stop();
		    }
		}
        
		public void Stop()
		{
			if (State == MediaState.Stopped)
				return;

			if(_cancellationTokenSource != null)
				_cancellationTokenSource.Cancel();

		    if (CanRunOnThisArchitecture)
		    {
		        if (_fmodTheoraStream != null) _fmodTheoraStream.Stop();
		        if (_theoraVideo != null) _theoraVideo.Terminate();
		    }
		    State = MediaState.Stopped;
		}

		public void Play()
		{
			if (State != MediaState.Stopped)
				return;

            if(!CanRunOnThisArchitecture)
                Log.Editor.WriteWarning("Can't play video on this architecture sorry ");
			State = MediaState.Playing;
			if (_cancellationTokenSource == null || _cancellationTokenSource.Token.IsCancellationRequested)
			{
				_cancellationTokenSource = new CancellationTokenSource();
				Task.Factory.StartNew(DecodeAudio, _cancellationTokenSource.Token);
			}

			if (_theoraVideo == null || _theoraVideo.Disposed)
				Initialize();

			_startTime = (float)Time.GameTimer.TotalMilliseconds;
		}

		public override bool IsVisible(IDrawDevice device)
		{
			if ((device.VisibilityMask & VisibilityFlag.ScreenOverlay) != (VisibilityGroup & VisibilityFlag.ScreenOverlay))
				return false;

			if ((VisibilityGroup & device.VisibilityMask & VisibilityFlag.AllGroups) == VisibilityFlag.None)
				return false;

			return device.IsCoordInView(GameObj.Transform.Pos, BoundRadius);
		}

		public override void Draw(IDrawDevice device)
		{
			DrawDesignTimeVisuals(device);

			if (State != MediaState.Playing)
				return;

			if (_theoraVideo == null || _theoraVideo.ElapsedMilliseconds == 0)
				return;

			Texture.Bind(_textureOne);
			GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _theoraVideo.Width, _theoraVideo.Height, PixelFormat.Luminance, PixelType.UnsignedByte,
				_theoraVideo.GetYColorPlane());

			Texture.Bind(_textureTwo);
			GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _theoraVideo.Width / 2, _theoraVideo.Height / 2, PixelFormat.Luminance, PixelType.UnsignedByte,
				_theoraVideo.GetCbColorPlane());

			Texture.Bind(_textureThree);
			GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _theoraVideo.Width / 2, _theoraVideo.Height / 2, PixelFormat.Luminance, PixelType.UnsignedByte,
				_theoraVideo.GetCrColorPlane());

			var drawTechnique = (OgvDrawTechnique)Material.Res.Technique.Res;
			drawTechnique.TextureOne = _textureOne;
			drawTechnique.TextureTwo = _textureTwo;
			drawTechnique.TextureThree = _textureThree;

			var rect = GetScreenRect();
			var z = GameObj.Transform == null ? 0 : GameObj.Transform.Pos.Z;

			if (_canvas == null)
			{
				_canvas = new Canvas(device);
				_canvas.State.SetMaterial(Material);
			}
			
			_canvas.State.ColorTint = ColourTint;
			_canvas.FillRect(rect.X, rect.Y, z, rect.W, rect.H);
		}

		private Rect GetScreenRect()
		{
			var rect = Rect.Empty;
			if (ScreenAspectOptions == ScreenAspectOptions.MaintainAspectRatio)
			{
				var videoRatio = (float) _theoraVideo.Width/_theoraVideo.Height;
				var screenRatio = DualityApp.TargetResolution.X/DualityApp.TargetResolution.Y;

				if (videoRatio > screenRatio)
				{
					rect.W = DualityApp.TargetResolution.X;
					rect.H = rect.W / videoRatio;
					rect.Y = (DualityApp.TargetResolution.Y - rect.H) / 2;
				}
				else
				{
					rect.H = DualityApp.TargetResolution.Y;
					rect.W = rect.H * videoRatio;
					rect.X = (DualityApp.TargetResolution.X - rect.W) / 2;
				}
			}
			else
			{
				rect = new Rect(0, 0, DualityApp.TargetResolution.X, DualityApp.TargetResolution.Y);
			}

			return rect;
		}

		private void DrawDesignTimeVisuals(IDrawDevice device)
		{
			if (DualityApp.ExecContext != DualityApp.ExecutionContext.Editor)
				return;

			if (device == null)
				return;

			var canvas = new Canvas(device);
			canvas.DrawRect(GameObj.Transform.Pos.X + Rect.MinimumX, GameObj.Transform.Pos.Y + Rect.MinimumY, GameObj.Transform.Pos.Z, Rect.W, Rect.H);
		}
	}
}
