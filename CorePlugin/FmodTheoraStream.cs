using System;
using System.Runtime.InteropServices;
using Duality;
using OgvPlayer.Fmod;

namespace OgvPlayer
{
	internal class FmodTheoraStream
    {
        private const int BufferSize = 1024768;

        private  Fmod.System _system;
        private  Sound _sound;
        private  Channel _channel;
        private  CREATESOUNDEXINFO _createsoundexinfo;
        private  bool _soundcreated;
        private  bool _isInitialized;

        private static MODE _mode = (MODE._2D | MODE.DEFAULT | MODE.OPENUSER | MODE.LOOP_NORMAL |
                                    MODE.HARDWARE);

        private  CircularBuffer<float> _circularBuffer;
        private  readonly object _syncObject = new object();

        public  void Initialize()
        {
			uint version = 0;
            var result = RESULT.ERR_UPDATE;
            const uint channels = 2;
            const uint frequency = 48000;
            _circularBuffer = new CircularBuffer<float>(BufferSize, true);
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
            }
            result= _system.playSound(CHANNELINDEX.FREE, _sound, false, ref _channel);
            if (result != RESULT.OK)
            {
                Log.Editor.WriteWarning("Problem initialising the audio part of the video player.{0}. No exceptions",
                    result);
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
			_circularBuffer.Clear();
        }

        public  void Stream(float[] data)
        {
			if(!_isInitialized)
				Initialize();
            lock (_syncObject)
            {
                _circularBuffer.Put(data);
            }
        }

        private  RESULT PcmReadCallback(IntPtr sounDraw, IntPtr data, uint datalen)
        {
            unsafe
            {
                uint count; //Does this need to be outside the lock? AM

                lock (_syncObject)
                {
                    if (_circularBuffer.Size == 0)
                        return RESULT.OK;

                    var stereo32BitBuffer = (float*) data.ToPointer();
                    for (count = 0; count < (datalen >> 2); count++) //WTF does this do AM
                    {
                        if (_circularBuffer.Size == 0)
                            break;

                        *stereo32BitBuffer++ = _circularBuffer.Get();
                    }
                }
            }
            return RESULT.OK;
        }
    }
}