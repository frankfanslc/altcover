﻿namespace AltCover.Shared

open System.Diagnostics.CodeAnalysis
open System.Runtime.CompilerServices

[<AutoOpen>]
[<SuppressMessage("Gendarme.Rules.Smells",
                  "AvoidSpeculativeGeneralityRule",
                  Justification = "Delegation is DRYing the codebase")>]
module internal StringExtension =
#if !DEBUG && !NET20
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
#endif
  let (==) (x: string) (y: string) =
    x.Equals(y, System.StringComparison.Ordinal)

#if !DEBUG && !NET20
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
#endif
  let (!=) (x: string) (y: string) = (x == y) |> not