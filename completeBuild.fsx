// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile

RestorePackages()

// Directories
let buildDir  = @".\build\"
let testDir   = @".\test\"
let packagesDir = @".\packages"

// version info
let version = "0.3.11"  // or retrieve from CI server


// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir]
)

Target "SetVersions" (fun _ ->
    CreateCSharpAssemblyInfo "./CorePlugin/Properties/AssemblyInfo.cs"
        [Attribute.Title "OgvVideo player"
         Attribute.Description "Ogv video player plugin for Duality Game engine"
         Attribute.Guid "00c8792c-39b8-4558-acf9-03013402301a"
         Attribute.Product "OgvVideo"
         Attribute.Version version
         Attribute.FileVersion version]
)

Target "CompileUnsafe" (fun _ ->          
    let buildMode = getBuildParamOrDefault "buildMode" "Release"
    let setParams defaults =
        { defaults with
            Verbosity = Some(Normal)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"                    
                    "Configuration", buildMode
                    "AllowUnsafeBlocks", "True"
                ]
        }
    build setParams "./DualityOgvPlayer.sln"    
    |> DoNothing  
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
            Version = version
            Project = "OgvPlayerCorePlugin"
            PublishUrl = getBuildParamOrDefault "nugetrepo" ""
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetrepo"
        }) 
        "nuget/OgvPlayerCorePlugin.nuspec"
)

// Dependencies
"Clean"
  ==> "SetVersions"
  ==> "CompileUnsafe"
//  ==> "NUnitTest"
  ==> "CreateNuget"
  

// start build
RunTargetOrDefault "CreateNuget"
