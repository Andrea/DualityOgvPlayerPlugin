using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Duality;
using Duality.Components;
using Duality.Drawing;
using Duality.Editor;
using Duality.Resources;
using OpenTK;
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
		[NonSerialized]
		private VertexC1P3T2[] _vertices;
		[NonSerialized]
		private MediaState _state;

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

			_theoraVideo = new TheoraVideo();
			_theoraVideo.InitializeVideo(_fileName);
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
			PrepareVertices(device, ColourTint, new Rect(1f, 1f));
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

			device.AddVertices(Material, VertexMode.Quads, _vertices);
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

		private void PrepareVertices(IDrawDevice device, ColorRgba mainClr, Rect uvRect)
		{
			var posTemp = GameObj.Transform.Pos;
			var scaleTemp = 1.0f;
			device.PreprocessCoords(ref posTemp, ref scaleTemp);

			Vector2 xDot, yDot;
			MathF.GetTransformDotVec(GameObj.Transform.Angle, scaleTemp, out xDot, out yDot);

			var rectTemp = Rect.Transform(GameObj.Transform.Scale, GameObj.Transform.Scale);
			var edge1 = rectTemp.TopLeft;
			var edge2 = rectTemp.BottomLeft;
			var edge3 = rectTemp.BottomRight;
			var edge4 = rectTemp.TopRight;

			MathF.TransformDotVec(ref edge1, ref xDot, ref yDot);
			MathF.TransformDotVec(ref edge2, ref xDot, ref yDot);
			MathF.TransformDotVec(ref edge3, ref xDot, ref yDot);
			MathF.TransformDotVec(ref edge4, ref xDot, ref yDot);

			if (_vertices == null || _vertices.Length != 4) _vertices = new VertexC1P3T2[4];

			_vertices[0].Pos.X = posTemp.X + edge1.X;
			_vertices[0].Pos.Y = posTemp.Y + edge1.Y;
			_vertices[0].Pos.Z = posTemp.Z;
			_vertices[0].TexCoord.X = uvRect.X;
			_vertices[0].TexCoord.Y = uvRect.Y;
			_vertices[0].Color = mainClr;

			_vertices[1].Pos.X = posTemp.X + edge2.X;
			_vertices[1].Pos.Y = posTemp.Y + edge2.Y;
			_vertices[1].Pos.Z = posTemp.Z;
			_vertices[1].TexCoord.X = uvRect.X;
			_vertices[1].TexCoord.Y = uvRect.MaximumY;
			_vertices[1].Color = mainClr;

			_vertices[2].Pos.X = posTemp.X + edge3.X;
			_vertices[2].Pos.Y = posTemp.Y + edge3.Y;
			_vertices[2].Pos.Z = posTemp.Z;
			_vertices[2].TexCoord.X = uvRect.MaximumX;
			_vertices[2].TexCoord.Y = uvRect.MaximumY;
			_vertices[2].Color = mainClr;

			_vertices[3].Pos.X = posTemp.X + edge4.X;
			_vertices[3].Pos.Y = posTemp.Y + edge4.Y;
			_vertices[3].Pos.Z = posTemp.Z;
			_vertices[3].TexCoord.X = uvRect.MaximumX;
			_vertices[3].TexCoord.Y = uvRect.Y;
			_vertices[3].Color = mainClr;
		}
	}
}
