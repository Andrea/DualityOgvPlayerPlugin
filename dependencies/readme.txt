# Cross-compiling on Windows for Android

You will need the Anroid NDK installed for all of this. Probably didn't need to be said, but there it is:)

## Ogg, Vorbis, and Theora
Check out https://github.com/jhotovy/android-ffmpeg. On Windows, you'll need to have msys and mingw installed to build these. Get those from http://sourceforge.net/projects/mingw/files/. Install a basic setup including mingw-developer-toolkit, mingw32-base, and msys-base. In your android-ffmpeg folder, go to Project/jni and modify settings.sh so that the NDK variable points at your NDK installation. On my system, that's C:/NVPACK/android-ndk-r10e. Next, open create_toolchain.sh and add the following at the end of the make-standalone-toolchain command:

	--toolchain=arm-linux-androideabi-4.9 --system=windows-x86_64

Change the toolchain if building for a different system. Open an msys shell (by default, here - C:\MinGW\msys\1.0\msys.bat) and navigate to [path/to/android-ffmpeg]/Project/jni. Run ./create_toolchain.sh, followed by ./config_make_everything.sh, and ndk-build. Check out the instructions on the android-ffmpeg github page for more details.

## TheoraPlay
Checkout android-cmake from https://github.com/taka-no-me/android-cmake. Open a command prompt in the TheoraPlay folder (make sure cmake is in your path) and follow these instructions from the android-cmake readme:

	cmake -DCMAKE_TOOLCHAIN_FILE=android.toolchain.cmake \
      -DANDROID_NDK=<ndk_path>                       \
      -DCMAKE_BUILD_TYPE=Release                     \
      -DANDROID_ABI="armeabi-v7a with NEON"          \
      .
	cmake --build .

This will build everything and put libtheoraplay.so in the Debug folder.