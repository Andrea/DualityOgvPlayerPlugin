using System;
using System.IO;
using System.Threading;
using Duality;

namespace OgvPlayer
{
    [Serializable]
    public class OgvComponent : Component, ICmpInitializable
    {

        private string _fileName;
        internal IntPtr theoraDecoder;
        internal IntPtr videoStream;

        public int Width { get; private set; }

        public int Height { get; private set; }

        public string FileName { get { return _fileName; } }

        private float INTERNAL_fps = 0.0f;
        public bool IsDisposed { get; set; }

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
            // Check out the file...
            _fileName = Normalize(FileName);
            if (_fileName == null)
            {
                throw new Exception("File " + FileName + " does not exist!");
            }

            // Set everything to NULL. Yes, this actually matters later.
            theoraDecoder = IntPtr.Zero;
            videoStream = IntPtr.Zero;

            // Initialize the decoder nice and early...
            //IsDisposed = true;
            Initialize();

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

        internal static string Normalize(string fileName)
        {
            if (File.Exists(fileName))
            {
                return fileName;
            }


            if (!string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                return null;
            }


            if (File.Exists(fileName + ".ogv"))
            {
                return fileName + ".ogv";
            }
            if (File.Exists(fileName + ".ogg"))
            {
                return fileName + ".ogg";
            }

            return null;
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
#if VIDEOPLAYER_OPENGL
                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_IYUV
#else
                // Use the TheoraPlay software converter.
                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_RGBA
#endif
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

                TheoraPlay.THEORAPLAY_VideoFrame frame = TheoraPlay.getVideoFrame(videoStream);

                // We get the FramesPerSecond from the first frame.
                FramesPerSecond = (float)frame.fps;
                Width = (int)frame.width;
                Height = (int)frame.height;
            }

            IsDisposed = false;
        }
    }


    [Serializable]
    public class OgvVideo : Resource
    {
        public const string FileExt = "OgvVideo.res";
    }

    public enum MediaState
    {
        Stopped,
        Playing,
        Paused
    }
}
