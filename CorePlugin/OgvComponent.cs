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
		
		[NonSerialized]
		private TheoraVideo _theoraVideo;

		[NonSerialized]
		private FmodTheoraStream _fmodTheoraStream;

		[NonSerialized]
		private CancellationTokenSource _cancellationTokenSource;

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
		public MediaState State { get; private set; }
		
		[EditorHintFlags(MemberFlags.Invisible)]
		public bool IsDisposed { get; set; }

		public ContentRef<Material> Material { get; set; }

		[EditorHintFlags(MemberFlags.Invisible)]

		public override float BoundRadius
		{
			get { return float.MaxValue; }
		}

		public void OnInit(InitContext context)
		{
			if (context != InitContext.Activate || DualityApp.ExecContext == DualityApp.ExecutionContext.Editor)
				return;

			if (string.IsNullOrEmpty(_fileName))
				return;

			Initialize();

			_textureOne = new Texture(_theoraVideo.Width, _theoraVideo.Height, format: PixelInternalFormat.Luminance);
			_textureTwo = new Texture(_theoraVideo.Width / 2, _theoraVideo.Height / 2, format: PixelInternalFormat.Luminance);
			_textureThree = new Texture(_theoraVideo.Width / 2, _theoraVideo.Height / 2, format: PixelInternalFormat.Luminance);
		}

		public void OnShutdown(ShutdownContext context)
		{
            if(context == ShutdownContext.Deactivate)
                Stop();
			if(_theoraVideo != null) 
				_theoraVideo.Terminate();

			IsDisposed = true;
			_cancellationTokenSource = null;
			_startTime = (float)Time.GameTimer.TotalMilliseconds;
		}

		internal void Initialize()
		{
		    if (!IsDisposed && _theoraVideo != null) 
                _theoraVideo.Terminate();
		    _fmodTheoraStream = new FmodTheoraStream();
			_fmodTheoraStream.Initialize();

			_theoraVideo = new TheoraVideo();
			_theoraVideo.InitializeVideo(_fileName);

			IsDisposed = false;
		}

		private void DecodeAudio()
		{
			const int bufferSize = 4096 * 2;

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
		}

		public void OnUpdate()
		{
			if (State != MediaState.Playing)
				return;
			if (Time.GameTimer.TotalMilliseconds - _startTime < 800)
				return;
			
			_elapsedFrameTime += Time.LastDelta * Time.TimeScale;

			_theoraVideo.UpdateVideo(_elapsedFrameTime);
		}


		public void Stop()
		{
			if (IsDisposed)
				return;
			if (State == MediaState.Stopped)
				return;
			_cancellationTokenSource.Cancel();
			_fmodTheoraStream.Stop();
			_theoraVideo.Terminate();
			State = MediaState.Stopped;
			IsDisposed = false;
		}

		public void Play()
		{
			if (IsDisposed)
				return;

			if (State != MediaState.Stopped)
				return;
			State = MediaState.Playing;
			if (_cancellationTokenSource == null || _cancellationTokenSource.Token.IsCancellationRequested)
			{
				_cancellationTokenSource = new CancellationTokenSource();
				Task.Factory.StartNew(DecodeAudio, _cancellationTokenSource.Token);
			}

			if (_theoraVideo.Disposed)
				Initialize();

			_startTime = (float)Time.GameTimer.TotalMilliseconds;
		}

		public override void Draw(IDrawDevice device)
		{
			if (State != MediaState.Playing)
				return;

			if (_theoraVideo.ElapsedMilliseconds == 0)
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

			var targetRect = new Rect(device.TargetSize);
			device.AddVertices(Material, VertexMode.Quads,
				new VertexC1P3T2(targetRect.MinimumX, targetRect.MinimumY, 0.0f, 0.0f, 0.0f),
				new VertexC1P3T2(targetRect.MaximumX, targetRect.MinimumY, 0.0f, 1, 0.0f),
				new VertexC1P3T2(targetRect.MaximumX, targetRect.MaximumY, 0.0f, 1, 1),
				new VertexC1P3T2(targetRect.MinimumX, targetRect.MaximumY, 0.0f, 0.0f, 1));
		}

	}
}
