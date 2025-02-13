﻿// Based in part upon https://github.com/lucaslorentz/minicover/blob/fe7467b0c17f1d1dee6ce9c45f6fd3bf6933b429/src/MiniCover/Instrumentation/ILProcessorExtensions.cs
// Based in part upon C# code by Sergiy Sakharov (sakharov@gmail.com)
// http://code.google.com/p/dot-net-coverage/source/browse/trunk/Coverage/Instrument/CounterAssemblyBuilder.cs
// http://code.google.com/p/dot-net-coverage/source/browse/trunk/Coverage/Instrument/InstrumentorVisitor.cs

namespace AltCover

open System
open Mono.Cecil
open Mono.Cecil.Cil

[<AutoOpen>]
module internal CecilExtension =
  let internal scopesSeen =
    System.Collections.Generic.HashSet<ScopeDebugInformation>()

  let internal safeOffset (point: InstructionOffset) =
    if point.IsEndOfMethod then
      None
    else
      Some point.Offset

  // workround for old MCS + Cecil 0.11.4
  let pruneLocalScopes (m: MethodDefinition) =
    scopesSeen.Clear()

    let rec pruneScope (scope: ScopeDebugInformation) =
      let novel = scopesSeen.Add scope

      if novel then
        let scopes = scope.Scopes // non-null by construction

        scopes
        |> Seq.filter (fun subScope ->
          let repeat =
            subScope
            |> Option.ofObj
            |> Option.map pruneScope
            |> Option.defaultValue true

          repeat || subScope.Start.IsEndOfMethod)
        |> Seq.toList
        |> List.iter (scopes.Remove >> ignore)

      not novel

    m.DebugInformation.Scope
    |> Option.ofObj
    |> Option.map pruneScope
    |> ignore

  // address issue 135
  let internal isResolvedProp =
    typeof<InstructionOffset>.GetProperty
      ("IsResolved",
       System.Reflection.BindingFlags.Instance
       ||| System.Reflection.BindingFlags.NonPublic)

  let internal offsetTable =
    System.Collections.Generic.SortedDictionary<int, Instruction>()

  let unresolved (point: InstructionOffset) =
    isResolvedProp.GetValue(point) :?> bool |> not

  let prepareLocalScopes (m: MethodDefinition) =
    offsetTable.Clear()
    scopesSeen.Clear()

    let size =
      m.Body.Instructions
      |> Seq.fold
           (fun _ i ->
             offsetTable.Add(i.Offset, i)
             i.Offset + i.GetSize())
           0

    let resolvePoint (point: InstructionOffset) =
      point
      |> safeOffset
      |> Option.map (fun offset ->
        let o = Math.Max(offset, 0)
        let ok, i = offsetTable.TryGetValue(o)

        if ok then
          InstructionOffset(i)
        else
          offsetTable.Keys
          |> Seq.filter (fun kk -> o < size && kk <= o)
          |> Seq.tryLast
          |> Option.map (fun k -> InstructionOffset(offsetTable.[k]))
          |> Option.defaultValue (InstructionOffset()))
      |> Option.defaultValue (InstructionOffset())

    let rec resolveScope (scope: ScopeDebugInformation) =
      if scope.IsNotNull then
        if scopesSeen.Add scope then
          scope.Scopes // non-null by construction
          |> Seq.iter resolveScope

          if unresolved scope.Start then
            scope.Start <- resolvePoint scope.Start

          if unresolved scope.End then
            scope.End <- resolvePoint scope.End

    m.DebugInformation.Scope |> resolveScope
    pruneLocalScopes m

  // Adjust the IL for exception handling
  // param name="handler">The exception handler</param>
  // param name="oldBoundary">The uninstrumented location</param>
  // param name="newBoundary">Where it has moved to</param>
  let substituteExceptionBoundary
    (oldValue: Instruction)
    (newValue: Instruction)
    (handler: ExceptionHandler)
    =
    if handler.FilterStart = oldValue then
      handler.FilterStart <- newValue

    if handler.HandlerEnd = oldValue then
      handler.HandlerEnd <- newValue

    if handler.HandlerStart = oldValue then
      handler.HandlerStart <- newValue

    if handler.TryEnd = oldValue then
      handler.TryEnd <- newValue

    if handler.TryStart = oldValue then
      handler.TryStart <- newValue

  let substituteInstructionOperand
    (oldValue: Instruction)
    (newValue: Instruction)
    (instruction: Instruction)
    =
    // Performance reasons - only 3 types of operators have operands of Instruction types
    // instruction.Operand getter - is rather slow to execute it for every operator
    match instruction.OpCode.OperandType with
    | OperandType.InlineBrTarget
    | OperandType.ShortInlineBrTarget ->
      if instruction.Operand = (oldValue :> Object) then
        instruction.Operand <- newValue
    // At this point instruction.Operand will be either Operand != oldOperand
    // or instruction.Operand will be of type Instruction[]
    // (in other words - it will be a switch operator's operand)
    | OperandType.InlineSwitch ->
      let operands =
        instruction.Operand :?> Instruction array

      operands
      |> Array.iteri (fun i x ->
        if x = oldValue then
          Array.set operands i newValue)
    | _ -> ()

  let replaceInstructionReferences
    (oldInstruction: Instruction)
    (newInstruction: Instruction)
    (ilProcessor: ILProcessor)
    =
    ilProcessor.Body.ExceptionHandlers
    |> Seq.iter (substituteExceptionBoundary oldInstruction newInstruction)

    // Update instructions with a target instruction
    ilProcessor.Body.Instructions
    |> Seq.iter (substituteInstructionOperand oldInstruction newInstruction)

  let bulkInsertBefore
    (ilProcessor: ILProcessor)
    (target: Instruction)
    (newInstructions: Instruction seq)
    (updateReferences: bool)
    =
    let newTarget =
      newInstructions
      |> Seq.rev
      |> Seq.fold
           (fun next i ->
             ilProcessor.InsertBefore(next, i)
             i)
           target

    if updateReferences then
      replaceInstructionReferences target newTarget ilProcessor

    newTarget

  let replaceReturnsByLeave (ilProcessor: ILProcessor) =
    let methodDefinition =
      ilProcessor.Body.Method

    let voidType =
      methodDefinition.Module.TypeSystem.Void
    // capture current state
    let instructions =
      ilProcessor.Body.Instructions |> Seq.toArray

    if methodDefinition.ReturnType = voidType then
      let newReturnInstruction =
        ilProcessor.Create(OpCodes.Ret)

      ilProcessor.Append(newReturnInstruction)

      instructions
      |> Seq.filter (fun i -> i.OpCode = OpCodes.Ret)
      |> Seq.iter (fun i ->
        i.OpCode <- OpCodes.Leave
        i.Operand <- newReturnInstruction)

      (newReturnInstruction, methodDefinition.ReturnType, [])
    else
      // this is the new part that AltCover didn't have before
      // i.e. handling non-void methods
      let returnVariable =
        VariableDefinition(methodDefinition.ReturnType)

      ilProcessor.Body.Variables.Add(returnVariable)

      let loadResultInstruction =
        ilProcessor.Create(OpCodes.Ldloc, returnVariable)

      ilProcessor.Append(loadResultInstruction)

      let newReturnInstruction =
        ilProcessor.Create(OpCodes.Ret)

      ilProcessor.Append(newReturnInstruction)

      (loadResultInstruction,
       methodDefinition.ReturnType,
       instructions
       |> Seq.filter (fun i -> i.OpCode = OpCodes.Ret)
       |> Seq.map (fun i ->
         i.OpCode <- OpCodes.Leave
         i.Operand <- loadResultInstruction

         bulkInsertBefore
           ilProcessor
           i
           [| ilProcessor.Create(OpCodes.Stloc, returnVariable) |]
           true)
       |> Seq.toList)

  let private findFirstInstruction (body: MethodBody) = body.Instructions |> Seq.head

  let encapsulateWithTryFinally (ilProcessor: ILProcessor) =
    let body = ilProcessor.Body

    let firstInstruction =
      findFirstInstruction body

    let (newReturn, methodType, stlocs) =
      replaceReturnsByLeave ilProcessor

    let endFinally =
      Instruction.Create(OpCodes.Endfinally)

    ilProcessor.InsertBefore(newReturn, endFinally)

    if (findFirstInstruction body)
         .Equals(firstInstruction) then
      let tryStart =
        Instruction.Create(OpCodes.Nop)

      ilProcessor.InsertBefore(firstInstruction, tryStart)

    let finallyStart =
      Instruction.Create(OpCodes.Nop)

    ilProcessor.InsertBefore(endFinally, finallyStart)

    let handler =
      ExceptionHandler(ExceptionHandlerType.Finally)

    handler.TryStart <- firstInstruction
    handler.TryEnd <- finallyStart
    handler.HandlerStart <- finallyStart
    handler.HandlerEnd <- newReturn
    body.ExceptionHandlers.Add(handler)
    (endFinally, methodType, stlocs)

  let removeTailInstructions (ilProcessor: ILProcessor) =
    ilProcessor.Body.Instructions
    |> Seq.toArray // reify
    |> Seq.filter (fun i -> i.OpCode = OpCodes.Tail)
    |> Seq.iter (fun i ->
      i.OpCode <- OpCodes.Nop
      i.Operand <- null)