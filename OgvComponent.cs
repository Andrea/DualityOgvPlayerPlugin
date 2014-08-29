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
            if(context != InitContext.Activate)
                return;
            

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
#if !VIDEOPLAYER_OPENGL
                // Use the TheoraPlay software converter.
                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_RGBA
#else
                TheoraPlay.THEORAPLAY_VideoFormat.THEORAPLAY_VIDFMT_IYUV
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
            var fileName = NormalizeFileName(filepath);
			LoadOgvVorbisData(fileName);
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
        
        internal static string NormalizeFileName(string fileName)
        {
            //not terribly sure if this method is truly necessary but it was there for monogame
            if (File.Exists(fileName))
                return fileName;
            
            if (!string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName))) 
                return null;

            if (File.Exists(fileName + ".ogv"))
                return fileName + ".ogv";

            if (File.Exists(fileName + ".ogg"))
                return fileName + ".ogg";

            return null;
        }

    }

    public enum MediaState
    {
        Stopped,
        Playing,
        Paused
    }
}
