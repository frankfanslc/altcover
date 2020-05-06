﻿// # namespace `AltCover`
// ```
namespace AltCover

open System.Runtime.CompilerServices

// ```
// ## module `PrepareExtension` and module `CollectExtension`
// ```
[<Extension>]
module PrepareExtension = begin
  [<Extension>]
  val WhatIf : prepare:AltCover.PrepareOptions -> AltCover.ValidatedCommandLine
end
[<Extension>]
module CollectExtension = begin
  [<Extension>]
  val WhatIf :
    collect:AltCover.CollectOptions ->
      afterPreparation:bool -> AltCover.ValidatedCommandLine
end
// ```
// These provide C#-compatible extension methods to perform a `WhatIf` style command like validation
//
// `WhatIf` compiles the effective command-line and the result of `Validate`
//
// ## module `AltCoverExtension`
// ```
[<AutoOpen>]
module AltCoverExtension = begin
  type AltCover.CollectOptions with
    member WhatIf : afterPreparation:bool -> AltCover.AltCover.ValidatedCommandLine
  type AltCover.PrepareOptions with
    member WhatIf : unit -> AltCover.AltCover.ValidatedCommandLine
end
//```
// provides seamless F# style extensions