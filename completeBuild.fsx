// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile

RestorePackages()

// Directories
let buildDir  = @".\build\"
let testDir   = @".\test\"
let deployDir = @".\deploy\"
let packagesDir = @".\packages"

// version info
let version = "0.2"  // or retrieve from CI server

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir]
)

Target "SetVersions" (fun _ ->
    CreateCSharpAssemblyInfo "./CorePlugin/Properties/AssemblyInfo.cs"
        [Attribute.Title "OgvVideo player"
         Attribute.Description "Ogv video player plugin for Duality Game engine"
         Attribute.Guid "00c8792c-39b8-4558-acf9-03013402301a"
         Attribute.Product "OgvVideo"
         Attribute.Version version
         Attribute.FileVersion version]

    CreateCSharpAssemblyInfo "./src/app/CalculatorLib/Properties/AssemblyInfo.cs"
        [Attribute.Title "Calculator library"
         Attribute.Description "Sample project for FAKE - F# MAKE"
         Attribute.Guid "EE5621DB-B86B-44eb-987F-9C94BCC98441"
         Attribute.Product "Calculator"
         Attribute.Version version
         Attribute.FileVersion version]
)
let setParams defaults =
    { defaults with
        Verbosity = Some(Quiet)
        Targets = ["Build"]
        Properties =
            [
                "Optimize", "True"
                "DebugSymbols", "True"
                "Configuration", "Release"
                "AllowUnsafeBlocks", "True"
            ]
    }
Target "CompileUnsafe" (fun _ ->       
    !! @"**\OgcPlayerCorePlugin.csproj"            
      |> MSBuildRelease buildDir "Build" 
      |> Log "AppBuild-Output: "
)

Target "Compile" (fun _ ->       
    !! @"**\EditorPlugin.csproj"      
      |> MSBuildRelease buildDir "Build" 
      |> Log "AppBuild-Output: "
)

Target "CompileTest" (fun _ ->
    !! @"**\*.Tests.csproj"
      |> MSBuildDebug testDir "Build"
      |> Log "TestBuild-Output: "
)

Target "NUnitTest" (fun _ ->
    !! (testDir + @"\NUnit.Test.*.dll")
      |> NUnit (fun p ->
                 {p with
                   DisableShadowCopy = true;
                   OutputFile = testDir + @"TestResults.xml"})
)

Target "CreateNuget" (fun _ ->
    // Copy all the package files into a package folder
    
    NuGet (fun p -> 
        {p with
            Authors = ["Digital Furnace Games "]
            Project = "OgvVideoPlayer"
            Description = "Plays ogv videos. Uses Fmod for the sound"                               
            OutputPath = "bin"
            Summary = "Plays ogv videos. Uses Fmod for the sound"            
            Version = buildVersion
            AccessKey = ""
            Publish = false }) 
            "nuget/OgvPlayer.nuspec"
)

Target "Zip" (fun _ ->
    !+ (buildDir + "\**\*.*")
        -- "*.zip"
        |> Scan
        |> Zip buildDir (deployDir + "Calculator." + version + ".zip")
)

// Dependencies
"Clean"
  ==> "SetVersions"
  ==> "CompileApp"
  ==> "CompileTest"  
  ==> "NUnitTest"
  ==> "CreateNuget"

// start build
RunTargetOrDefault "CreateNuget"

