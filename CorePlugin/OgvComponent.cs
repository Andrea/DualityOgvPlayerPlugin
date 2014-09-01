using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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

			FmodTheoraStream.Init();

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

			const int BUFFER_SIZE = 4096 * 2;

			// Store our abstracted buffer into here.
			var data = new List<float>();

			TheoraPlay.THEORAPLAY_AudioPacket currentAudio;
			currentAudio.channels = 0;
			currentAudio.freq = 0;

//			while (TheoraPlay.THEORAPLAY_availableAudio(theoraDecoder) == 0) ;
			
			while (data.Count < BUFFER_SIZE && TheoraPlay.THEORAPLAY_availableAudio(theoraDecoder) > 0)
			{
				var audioPtr = TheoraPlay.THEORAPLAY_getAudio(theoraDecoder);
				currentAudio = TheoraPlay.getAudioPacket(audioPtr);
				data.AddRange(TheoraPlay.getSamples(
						currentAudio.samples,
						currentAudio.frames * currentAudio.channels)
				);
				TheoraPlay.THEORAPLAY_freeAudio(audioPtr);
			}

			FmodTheoraStream.Stream(data.ToArray());
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

	public class FmodTheoraStream
	{
		private static Fmod.System system = null;
		private static Fmod.Sound sound = null;
		private static Fmod.Channel channel = null;
		private static Fmod.CREATESOUNDEXINFO createsoundexinfo = new Fmod.CREATESOUNDEXINFO();
		private static bool soundcreated = false;
		private static Fmod.MODE mode = (Fmod.MODE._2D | Fmod.MODE.DEFAULT | Fmod.MODE.OPENUSER | Fmod.MODE.LOOP_NORMAL | Fmod.MODE.HARDWARE);
		private static float[] _buffer = new float[0];
		private static object _syncObject = new object();

		public static void Init()
		{
			uint version = 0;
			Fmod.RESULT result;
			uint channels = 2, frequency = 48000;

			/*
				Create a System object and initialize.
			*/
			result = Fmod.Factory.System_Create(ref system);
//			ERRCHECK(result);
			result = system.getVersion(ref version);
//			ERRCHECK(result);
//			if (version < Fmod.VERSION.number)
//			{
//				MessageBox.Show("Error!  You are using an old version of Fmod " + version.ToString("X") + ".  This program requires " + Fmod.VERSION.number.ToString("X") + ".");
//				Application.Exit();
//			}
			result = system.init(32, Fmod.INITFLAGS.NORMAL, (IntPtr)null);
//			ERRCHECK(result);

			createsoundexinfo.cbsize = Marshal.SizeOf(createsoundexinfo);
			createsoundexinfo.fileoffset = 0;
			createsoundexinfo.length = frequency * channels * 2 * 2;
			createsoundexinfo.numchannels = (int)channels;
			createsoundexinfo.defaultfrequency = (int)frequency;
			createsoundexinfo.format = Fmod.SOUND_FORMAT.PCMFLOAT;
			createsoundexinfo.pcmreadcallback += PcmReadCallback;
			createsoundexinfo.dlsname = null;

			if (!soundcreated)
			{
				result = system.createSound(
					(string)null,
					(mode | Fmod.MODE.CREATESTREAM),
					ref createsoundexinfo,
					ref sound);

				soundcreated = true;
			}
			system.playSound(Fmod.CHANNELINDEX.FREE, sound, false, ref channel);
		}

		public static void Stream(float[] data)
		{
			lock (_syncObject)
			{
				var destinationIndex = _buffer.Length;
				Array.Resize(ref _buffer, _buffer.Length + data.Length);
				Array.Copy(data, 0, _buffer, destinationIndex, data.Length);
			}
		}

		private static Fmod.RESULT PcmReadCallback(IntPtr soundraw, IntPtr data, uint datalen)
		{
			unsafe
			{
				uint count;

				lock (_syncObject)
				{
					if (_buffer.Length == 0)
						return Fmod.RESULT.OK;

					var stereo32BitBuffer = (float*)data.ToPointer();

					for (count = 0; count < (datalen >> 2); count+=2)
					{
						*stereo32BitBuffer++ = _buffer[count];
						*stereo32BitBuffer++ = _buffer[count + 1];
					}

					var temp = new float[_buffer.Length - (datalen >> 2)];
					Array.Copy(_buffer, (datalen >> 2), temp, 0, _buffer.Length - (datalen >> 2));
					_buffer = temp;
				}

//				short* stereo16bitbuffer = (short*)data.ToPointer();
//
//				for (count = 0; count < (datalen >> 2); count++)        // >>2 = 16bit stereo (4 bytes per sample)
//				{
//					*stereo16bitbuffer++ = (short)(Math.Sin(t1) * 32767.0f);    // left channel
//					*stereo16bitbuffer++ = (short)(Math.Sin(t2) * 32767.0f);    // right channel
//
//					t1 += 0.01f + v1;
//					t2 += 0.0142f + v2;
//					v1 += (float)(Math.Sin(t1) * 0.002f);
//					v2 += (float)(Math.Sin(t2) * 0.002f);
//				}
			}
			return Fmod.RESULT.OK;
		}
	}
}
