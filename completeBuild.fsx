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
)

Target "CompileUnsafe" (fun _ ->          
    let buildMode = getBuildParamOrDefault "buildMode" "Release"
    let setParams defaults =
        { defaults with
            Verbosity = Some(Quiet)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", buildMode
                    "AllowUnsafeBlocks", "True"
                ]
        }
    build setParams "./ProjectPlugins.sln"    
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
            Version = buildVersion
            AccessKey = ""
            Publish = false }) 
            "nuget/OgcPlayerCorePlugin.nuspec"
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
  ==> "CompileUnsafe"
//  ==> "CompileTest"  
  //==> "NUnitTest"
  ==> "CreateNuget"

// start build
RunTargetOrDefault "CreateNuget"


