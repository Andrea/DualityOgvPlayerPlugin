using System;
using System.IO;
using System.Threading;
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
		internal IntPtr theoraDecoder;
		[NonSerialized]
		internal IntPtr videoStream;
		[NonSerialized]
		private IntPtr _previousFrame;

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

		[NonSerialized]
		private float INTERNAL_fps = 0.0f;
		[NonSerialized]
		private TheoraPlay.THEORAPLAY_VideoFrame _nextVideo;
		[NonSerialized]
		private TheoraPlay.THEORAPLAY_VideoFrame _currentVideo;
		[NonSerialized]
		private float _elapsedFrameTime;
		public bool IsDisposed { get; set; }

		public ContentRef<Texture> TextureOne { get; set; }
		public ContentRef<Texture> TextureTwo { get; set; }
		public ContentRef<Texture> TextureThree { get; set; }
		public ContentRef<Material> Material { get; set; }

		// FIXME: This is hacked, look up "This is a part of the Duration hack!"
		public TimeSpan Duration
		{
			get;
			internal set;
		}

		public float FramesPerSecond
		{
			get
			{
				return INTERNAL_fps;
			}
			internal set
			{
				INTERNAL_fps = value;
			}
		}

		public void OnInit(InitContext context)
		{
			if (context != InitContext.Activate)
				return;


			// Set everything to NULL. Yes, this actually matters later.
			theoraDecoder = IntPtr.Zero;
			videoStream = IntPtr.Zero;

			// Initialize the decoder nice and early...
			//IsDisposed = true;
			if (!string.IsNullOrEmpty(_fileName))
				Initialize();

			TextureOne = new ContentRef<Texture>(new Texture(Width, Height, 
				format: PixelInternalFormat.Luminance));
			TextureTwo = new ContentRef<Texture>(new Texture(Width / 2, Height / 2, format: PixelInternalFormat.Luminance));
			TextureThree = new ContentRef<Texture>(new Texture(Width / 2, Height / 2, format: PixelInternalFormat.Luminance));

			// FIXME: This is a part of the Duration hack!
			Duration = TimeSpan.MaxValue;
		}

		public void OnShutdown(ShutdownContext context)
		{

			// Stop and unassign the decoder.
			if (theoraDecoder != IntPtr.Zero)
			{
				TheoraPlay.THEORAPLAY_stopDecode(theoraDecoder);
				theoraDecoder = IntPtr.Zero;
			}

			// Free and unassign the video stream.
			if (videoStream != IntPtr.Zero)
			{
				TheoraPlay.THEORAPLAY_freeVideo(videoStream);
				videoStream = IntPtr.Zero;
			}

			IsDisposed = true;

		}


		internal void Initialize()
		{
			if (!IsDisposed)
			{
				Dispose(); // We need to start from the beginning, don't we? :P
			}

			// Initialize the decoder.
			theoraDecoder = TheoraPlay.THEORAPLAY_startDecodeFile(
				_fileName,
				150, // Arbitrarily 5 seconds in a 30fps movie.
				//#if !VIDEOPLAYER_OPENGL
				//                // Use the TheoraPlay software converter.
				//                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_RGBA
				//#else
				TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_IYUV
				//#endif
);

			// Wait until the decoder is ready.
			while (TheoraPlay.THEORAPLAY_isInitialized(theoraDecoder) == 0)
			{
				Thread.Sleep(10);
			}

			// Initialize the video stream pointer and get our first frame.
			if (TheoraPlay.THEORAPLAY_hasVideoStream(theoraDecoder) != 0)
			{
				while (videoStream == IntPtr.Zero)
				{
					videoStream = TheoraPlay.THEORAPLAY_getVideo(theoraDecoder);
					Thread.Sleep(10);
				}

				var frame = TheoraPlay.getVideoFrame(videoStream);

				// We get the FramesPerSecond from the first frame.
				FramesPerSecond = (float)frame.fps;
				Width = (int)frame.width;
				Height = (int)frame.height;
			}

			IsDisposed = false;
		}

		public void OnUpdate()
		{
			_elapsedFrameTime += (float)Time.LastDelta * Time.TimeScale;
			if (_elapsedFrameTime < _currentVideo.playms)
			{
				return;
			}
			
			_currentVideo = _nextVideo;
			var nextFrame = TheoraPlay.THEORAPLAY_getVideo(theoraDecoder);

			if (nextFrame != IntPtr.Zero)
			{
				TheoraPlay.THEORAPLAY_freeVideo(_previousFrame);
				_previousFrame = videoStream;
				videoStream = nextFrame;
				_nextVideo = TheoraPlay.getVideoFrame(videoStream);
			}
		}

		public override void Draw(IDrawDevice device)
		{
			if (_currentVideo.playms == 0)
				return;

			Texture.Bind(TextureOne);
			GL.TexSubImage2D(
				TextureTarget.Texture2D,
				0,
				0,
				0,
				Width,
				Height,
				PixelFormat.Luminance,
				PixelType.UnsignedByte,
				_currentVideo.pixels);

			Texture.Bind(TextureTwo);
			GL.TexSubImage2D(
			   TextureTarget.Texture2D,
			   0,
			   0,
			   0,
			   Width / 2,
			   Height / 2,
			   PixelFormat.Luminance,
			   PixelType.UnsignedByte,
			   new IntPtr(
				   _currentVideo.pixels.ToInt64() +
				   (_currentVideo.width * _currentVideo.height)
			   )
		   );

			Texture.Bind(TextureThree);
			GL.TexSubImage2D(
				TextureTarget.Texture2D,
				0,
				0,
				0,
				Width / 2,
				Height / 2,
				PixelFormat.Luminance,
				PixelType.UnsignedByte,
				new IntPtr(
					_currentVideo.pixels.ToInt64() +
					(_currentVideo.width * _currentVideo.height) +
					(_currentVideo.width / 2 * _currentVideo.height / 2)
				)
			);

			var drawTechnique = (OgvDrawTechnique) Material.Res.Technique.Res;
			drawTechnique.TextureOne = TextureOne.Res;
			drawTechnique.TextureTwo = TextureTwo.Res;
			drawTechnique.TextureThree = TextureThree.Res;

			var targetRect = new Rect(device.TargetSize);
			device.AddVertices(Material, VertexMode.Quads,
				new VertexC1P3T2(targetRect.MinimumX, targetRect.MinimumY, 0.0f, 0.0f, 0.0f),
				new VertexC1P3T2(targetRect.MaximumX, targetRect.MinimumY, 0.0f, 1, 0.0f),
				new VertexC1P3T2(targetRect.MaximumX, targetRect.MaximumY, 0.0f, 1, 1),
				new VertexC1P3T2(targetRect.MinimumX, targetRect.MaximumY, 0.0f, 0.0f, 1));
		}

		public override float BoundRadius
		{
			get { return float.MaxValue; }
		}
	}

	[Serializable]
	public class OgvDrawTechnique : DrawTechnique
	{
		public Texture TextureOne { get; set; }
		public Texture TextureTwo { get; set; }
		public Texture TextureThree { get; set; }

		public override bool NeedsPreparation
		{
			get { return true; }
		}

		protected override void PrepareRendering(IDrawDevice device, BatchInfo material)
		{
			base.PrepareRendering(device, material);

			material.SetTexture("mainTex", TextureOne);
			material.SetTexture("samp1", TextureTwo);
			material.SetTexture("samp2", TextureThree);
		}
	}


	[Serializable]
	public class OgvVideo : Resource
	{
		private OgvVideo(ContentRef<OgvVideo> beep)
		{
			throw new NotImplementedException();
		}

		public const string FileExt = "OgvVideo.res";

		public static ContentRef<OgvVideo> Beep { get; private set; }

		internal static void InitDefaultContent()
		{
			const string VirtualContentPath = ContentProvider.VirtualContentPath + "Sound:";
			const string ContentPath_Beep = VirtualContentPath + "Beep";

			ContentProvider.AddContent(ContentPath_Beep, new OgvVideo(OgvVideo.Beep));

			Beep = ContentProvider.RequestContent<OgvVideo>(ContentPath_Beep);
		}

		public OgvVideo(string filepath)
		{

			LoadOgvVorbisData(filepath);
		}

		private byte[] data = null;

		public void LoadOgvVorbisData(string ogvVorbisPath = null)
		{
			if (ogvVorbisPath == null)
				ogvVorbisPath = sourcePath;

			sourcePath = ogvVorbisPath;

			if (String.IsNullOrEmpty(this.sourcePath) || !File.Exists(this.sourcePath))
				data = null;
			else
				data = File.ReadAllBytes(this.sourcePath);

			this.DisposeAlBuffer();
		}

		/// <summary>
		/// A dummy OpenAL buffer handle, indicating that the buffer in question is not available.
		/// </summary>
		public const int AlBuffer_NotAvailable = 0;
		/// <summary>
		/// A dummy OpenAL buffer handle, indicating that the buffer in question is inactive due to streaming.
		/// </summary>
		public const int AlBuffer_StreamMe = -1;

		[NonSerialized]
		private int alBuffer = AlBuffer_NotAvailable;

		// <summary>
		/// Disposes the AudioDatas OpenAL buffer.
		/// </summary>
		/// <seealso cref="SetupAlBuffer"/>
		public void DisposeAlBuffer()
		{
			if (alBuffer > AlBuffer_NotAvailable)
				OpenTK.Audio.OpenAL.AL.DeleteBuffer(alBuffer);
			alBuffer = AlBuffer_NotAvailable;

		}



	}

	public enum MediaState
	{
		Stopped,
		Playing,
		Paused
	}
}
