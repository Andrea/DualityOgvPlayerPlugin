using System;
using System.Runtime.InteropServices;
using System.Threading;
using OgvPlayer.Fmod;

namespace OgvPlayer
{
	public class FmodTheoraStream
	{
		const int BUFFER_SIZE = 1024768;

		private static Fmod.System system = null;
		private static Fmod.Sound sound = null;
		private static Fmod.Channel channel = null;
		private static Fmod.CREATESOUNDEXINFO createsoundexinfo = new Fmod.CREATESOUNDEXINFO();
		private static bool soundcreated = false;
		private static Fmod.MODE mode = (Fmod.MODE._2D | Fmod.MODE.DEFAULT | Fmod.MODE.OPENUSER | Fmod.MODE.LOOP_NORMAL | Fmod.MODE.HARDWARE);
		private static float[] _buffer = new float[0];
		private static CircularBuffer<float> _circularBuffer;
		private static object _syncObject = new object();

		public static void Init()
		{
			uint version = 0;
			Fmod.RESULT result;
			uint channels = 2, frequency = 48000;
			_circularBuffer = new CircularBuffer<float>(BUFFER_SIZE, true);
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
				//				var destinationIndex = _buffer.Length;
				//				Array.Resize(ref _buffer, _buffer.Length + data.Length);
				//				Array.Copy(data, 0, _buffer, destinationIndex, data.Length);
				_circularBuffer.Put(data);
			}
		}

		private static Fmod.RESULT PcmReadCallback(IntPtr soundraw, IntPtr data, uint datalen)
		{
			unsafe
			{
				uint count;

				lock (_syncObject)
				{
					//					if (_buffer.Length == 0)
					if (_circularBuffer.Size == 0)
						return Fmod.RESULT.OK;

					var stereo32BitBuffer = (float*)data.ToPointer();

					for (count = 0; count < (datalen >> 2); count++)
					{
						if (_circularBuffer.Size == 0)
							break;

						*stereo32BitBuffer++ = _circularBuffer.Get();
						//						*stereo32BitBuffer++ = _circularBuffer.Get();
						//					*stereo32BitBuffer++ = _buffer[count];
						//						*stereo32BitBuffer++ = _buffer[count + 1];
					}

					//					var temp = new float[_buffer.Length - (datalen >> 2)];
					//					Array.Copy(_buffer, (datalen >> 2), temp, 0, _buffer.Length - (datalen >> 2));
					//					_buffer = temp;
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