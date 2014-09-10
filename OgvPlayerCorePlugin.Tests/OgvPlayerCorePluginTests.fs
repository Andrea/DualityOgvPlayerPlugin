module  OgvPlayerCorePluginTests

open FsCheck.NUnit
open OgvPlayer
open Duality


    [<Property>]
    let ``When player initialized then not disposed``() =
        let ogv =new OgvComponent()
        ogv.OnInit Duality.Component.InitContext.Activate
        ogv.Disposed = false


   (* [<Property>]
    let ``When player playing then not disposed``() =
        let ogv =new OgvComponent()
        ogv.FileName <- "cats.ogg"
        ogv.OnInit Duality.Component.InitContext.Activate
        ogv.Play()
        ogv.Disposed = false
   *)