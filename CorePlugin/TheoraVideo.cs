using System;
using System.Threading;
using Duality;

namespace OgvPlayer
{
	internal class TheoraVideo
	{
		private IntPtr _theoraDecoder;
		private IntPtr _videoStream;
		private IntPtr _previousFrame;

		private TheoraPlay.THEORAPLAY_VideoFrame _nextVideo;
		private TheoraPlay.THEORAPLAY_VideoFrame _currentVideo;
		private bool _disposed;

		public int Width { get; private set; }

		public int Height { get; private set; }

		public float FramesPerSecond { get; private set; }

		public uint ElapsedMilliseconds
		{
			get { return _currentVideo.playms; }
		}

		public IntPtr TheoraDecoder
		{
			get { return _theoraDecoder; }
		}

		public bool Disposed
		{
			get { return _disposed; }
		}

		public void InitializeVideo(string fileName)
		{
			// set finished to true before we start so that anyone polling on this flag afterwards won't get stuck in a loop in the case
			//  where initialization fails.
			IsFinished = true;

			// Initialize the decoder.
		    try
		    {
		        _theoraDecoder = TheoraPlay.THEORAPLAY_startDecodeFile(
		            fileName,
		            150, // Arbitrarily 5 seconds in a 30fps movie.
		            //#if !VIDEOPLAYER_OPENGL
		            //                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_RGBA
		            //#else
		            TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_IYUV
		            //#endif
		            );

		        var counter = 0;
		        // Wait until the decoder is ready.
		        while (TheoraPlay.THEORAPLAY_isInitialized(_theoraDecoder) == 0 && counter < 100)
		        {
		            Thread.Sleep(10); 
		            counter++;
		        }
		        if (counter >= 100)
		        {
		            Log.Editor.WriteError("Could not initialize {0}. Operation timed out sorry", fileName);
                    return;
		        }
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
			        IsFinished = false;
		        }
		        _disposed = false;
		    }
		    catch (BadImageFormatException exception)
		    {
		        Log.Editor.WriteError("There was a problem initializing Theoraplay possibly running on x64, we only support x86 for the moment.{1} {0} {1} {2}", exception.Message , Environment.NewLine, exception.StackTrace);
		    }
            catch (Exception exception)
            {
                Log.Editor.WriteError("There was a problem initializing video with Theoraplay. {1} {0} {1} {2}", exception.Message, Environment.NewLine, exception.StackTrace);
            }
		}

		public void Terminate()
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
			_currentVideo = new TheoraPlay.THEORAPLAY_VideoFrame();
			_nextVideo = new TheoraPlay.THEORAPLAY_VideoFrame();
			TheoraPlay.THEORAPLAY_freeVideo(_previousFrame);
			_previousFrame = IntPtr.Zero;
			_videoStream = IntPtr.Zero;
			_disposed = true;
			
		}

		public void UpdateVideo(float elapsedFrameTime)
		{
			while (_currentVideo.playms <= elapsedFrameTime && TheoraPlay.THEORAPLAY_availableVideo(_theoraDecoder)!=0)
			{
				_currentVideo = _nextVideo;
				
				var nextFrame = TheoraPlay.THEORAPLAY_getVideo(_theoraDecoder);

				if (nextFrame != IntPtr.Zero)
				{
					TheoraPlay.THEORAPLAY_freeVideo(_previousFrame);
					_previousFrame = _videoStream;
					_videoStream = nextFrame;
					_nextVideo = TheoraPlay.getVideoFrame(_videoStream);

				}
			}
		    IsFinished = TheoraPlay.THEORAPLAY_isDecoding(_theoraDecoder) == 0;
		}

	    public bool IsFinished { get; private set; }

	    /*
		 Theora divides the pixel array up into three separate color planes, one for each of the Y 0 , Cb, and Cr components of the pixel. The Y
		0 plane is also called the luma plane, and the Cb and Cr planes are also called the chroma planes. 
		In some pixel formats, the chroma planes are subsampled by a factor of two in one or both directions. This means that the width or height of the chroma
		planes may be half that of the total frame width and height. The luma plane is never subsampled.
		 from http://www.theora.org/doc/Theora.pdf chapter 2
		 */

		public IntPtr GetYColorPlane()
		{
			return _currentVideo.pixels;
		}
		public IntPtr GetCbColorPlane()
		{
			return new IntPtr(_currentVideo.pixels.ToInt64() + (_currentVideo.width * _currentVideo.height));
		}
		public IntPtr GetCrColorPlane()
		{
			return new IntPtr(_currentVideo.pixels.ToInt64() + (_currentVideo.width * _currentVideo.height) + (_currentVideo.width / 2 * _currentVideo.height / 2));
		}

	}
}