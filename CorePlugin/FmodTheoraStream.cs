using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Duality;
using OgvPlayer.Fmod;

namespace OgvPlayer
{
	internal class FmodTheoraStream
    {
        private  Fmod.System _system;
        private  Sound _sound;
        private  Channel _channel;
        private  CREATESOUNDEXINFO _createsoundexinfo;
        private  bool _soundcreated;
        private  bool _isInitialized;

        private static MODE _mode = (MODE._2D | MODE.DEFAULT | MODE.OPENUSER | MODE.LOOP_NORMAL |
                                    MODE.HARDWARE);
		private ConcurrentQueue<float> _dataBuffer;
        private  readonly object _syncObject = new object();

        public  void Initialize()
        {
			uint version = 0;
            var result = RESULT.ERR_UPDATE;
            const uint channels = 2;
            const uint frequency = 48000;
            
			_dataBuffer = new ConcurrentQueue<float>();
            try
            {
                result = Factory.System_Create(ref _system);
                result = _system.getVersion(ref version);
                result = _system.init(32, INITFLAGS.NORMAL, (IntPtr) null);

                _createsoundexinfo.cbsize = Marshal.SizeOf(_createsoundexinfo);
                _createsoundexinfo.fileoffset = 0;
                _createsoundexinfo.length = frequency*channels*2*2;
                _createsoundexinfo.numchannels = (int) channels;
                _createsoundexinfo.defaultfrequency = (int) frequency;
                _createsoundexinfo.format = SOUND_FORMAT.PCMFLOAT;
                _createsoundexinfo.pcmreadcallback += PcmReadCallback;
                _createsoundexinfo.dlsname = null;

                result = CreateSound(result);
            }
            catch (Exception exception)
            {
                Log.Editor.WriteWarning("Problem initialising the audio part of the video player.{0}. Error: {1}",
                    result, exception.Message);
                return;
            }
            result= _system.playSound(CHANNELINDEX.FREE, _sound, false, ref _channel);
            if (result != RESULT.OK)
            {
                Log.Editor.WriteWarning("Problem initialising the audio part of the video player.{0}. No exceptions",
                    result);
                return;
            }
	        _isInitialized = true;
        }

	    private  RESULT CreateSound(RESULT result)
	    {
		    if (!_soundcreated)
		    {
			    result = _system.createSound((string) null, (_mode | MODE.CREATESTREAM), ref _createsoundexinfo,
				    ref _sound);
			    _soundcreated = true;
		    }
		    return result;
	    }

	    public  void Stop()
        {
	        _channel.stop();
	        _isInitialized = false;
		    _soundcreated = false;
			
			_dataBuffer = new ConcurrentQueue<float>();
			Dispose(true);
        }

        public  void Stream(float[] data)
        {
			if(!_isInitialized)
				Initialize();
	        
            lock (_syncObject)
            {
				foreach (var f in data)
				{
					_dataBuffer.Enqueue(f);
				}
            }
        }

        private  RESULT PcmReadCallback(IntPtr sounDraw, IntPtr data, uint datalen)
        {
            unsafe
            {
                uint count; //Does this need to be outside the lock? AM
                lock (_syncObject)
                {
                    if (_dataBuffer.Count == 0)

                        return RESULT.OK;

                    var stereo32BitBuffer = (float*) data.ToPointer();
                    for (count = 0; count < (datalen >> 2); count++) //WTF does this do AM
                    {
                        if (_dataBuffer.Count == 0)
                            break;
	                    float result;
						if(_dataBuffer.TryDequeue(out result))
							*stereo32BitBuffer++ = result;
                    }
                }
            }
            return RESULT.OK;
        }

		protected  void Dispose(bool disposing)
		{
			if (!disposing) 
				return;
			if (_sound != null)
				_sound.release();
			if (_system != null)
			{
				_system.close();
				_system.release();
			}
		}

    }
}