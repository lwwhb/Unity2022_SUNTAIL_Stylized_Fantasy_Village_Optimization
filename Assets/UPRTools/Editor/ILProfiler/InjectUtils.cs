using UPR.Cecil;
using UPR.Cecil.Cil;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace UPRProfiler
{
    public static class InjectUtils
    {
        public static void addProfiler(string assemblyPath, string fileName, string typeName, string methodName)
        {
            if (Application.isPlaying || EditorApplication.isCompiling)
            {
                Debug.Log("You need stop play mode or wait compiling finished");
                return;
            }
            if (!System.IO.File.Exists(assemblyPath + fileName))
            {
                Debug.Log("This Project didn't contains this dll");
                return;
            }

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(assemblyPath);
            var readerParameters = new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver };
            var assembly = AssemblyDefinition.ReadAssembly(assemblyPath + fileName, readerParameters);

            if (assembly == null)
            {
                Debug.LogError("InjectTool Inject Load assembly failed: " + assemblyPath);
                return;
            }

            try
            {
                var res = ProcessAssembly(assembly, typeName, methodName);
                assembly.Write(assemblyPath + fileName, new WriterParameters { WriteSymbols = false });
                if (res)
                {
                    Debug.Log("Listening function " + methodName + " successfully!");
                }
                else
                {
                    Debug.Log("function " + methodName + " doesn't has method body!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("InjectTool addProfiler failed: " + ex);
                throw;
            }
            finally
            {
                if (assembly.MainModule.SymbolReader != null)
                {
                    Debug.Log("InjectTool addProfiler Succeed");
                    assembly.MainModule.SymbolReader.Dispose();
                }
            }

            // Debug.Log("InjectTool Inject" + fileName + " End");
        }

        static bool ProcessAssembly(AssemblyDefinition assembly, string typeName, string methodName)
        {
            var changed = false;
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    if (type.Name == typeName)
                    {
                        foreach (var method in type.Methods)
                        {
                            if ((method.Name == methodName || method.Name == methodName + "Async") && method.HasParameters)
                            {
                                string label = "upr: " + method.Name;
                                if (methodName == "LoadAsset" && method.ReturnType.FullName == "T")
                                {
                                    continue;
                                }
                                if (methodName == "LoadScene")
                                {
                                    label += " " + method.Parameters[0] + ": ";
                                }
                                else
                                {
                                    label += " " + method.ReturnType + " " + method.Parameters[0] + ": ";
                                }
                                
                                var beginMethod =
                                    module.ImportReference(typeof(Profiler).GetMethod("BeginSample",
                                                                                       new[] { typeof(string) }));
                                var endMethod =
                                    module.ImportReference(typeof(Profiler).GetMethod("EndSample",
                                                                                       BindingFlags.Static |
                                                                                       BindingFlags.Public));
                                var concatMethod = module.ImportReference(typeof(String).GetMethod("Concat", new[] { typeof(string), typeof(string) }));

                                if (!method.HasBody)
                                {
                                    return false;
                                }
                                
                                var ilProcessor = method.Body.GetILProcessor();
                                var first = method.Body.Instructions[0];

                                ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Nop));
                                ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Ldstr, label));

                                if (methodName == "LoadAsset" || methodName == "Load")
                                {
                                    ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_1));
                                }
                                else
                                {
                                    ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                                }
                                if (method.Parameters[0].ParameterType.ToString() != "System.String")
                                {
                                    ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Box, method.Parameters[0].ParameterType));
                                    concatMethod = module.ImportReference(typeof(String).GetMethod("Concat", new[] { typeof(object), typeof(object) }));
                                }
        
                                ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Call, concatMethod));
                                ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Call, beginMethod));

                                var lastCall = Instruction.Create(OpCodes.Call, endMethod);

                                InnerProcess(module, method, lastCall);

                                changed = true;
                            }
                        }
                    }
                }
            }

            return changed;
        }

        private static Instruction FixReturns(ModuleDefinition mod, MethodDefinition method)
        {
            UPR.Cecil.Cil.MethodBody body = method.Body;
            var instructions = body.Instructions;
            var lastRet = Instruction.Create(OpCodes.Ret);


            if (method.ReturnType == mod.TypeSystem.Void)
            {
                instructions.Add(lastRet);

                for (var index = 0; index < instructions.Count - 1; index++)
                {
                    var instruction = instructions[index];
                    if (instruction.OpCode == OpCodes.Ret)
                    {
                        instructions[index] = Instruction.Create(OpCodes.Leave, lastRet);
                    }
                }
                return lastRet;
                // force ret to leave to the Endsample
            }
            else
            {
                var returnVariable = new VariableDefinition(method.ReturnType);
                body.Variables.Add(returnVariable);
                var lastLd = Instruction.Create(OpCodes.Ldloc, returnVariable); //load the local variable to the stack top
                instructions.Add(lastLd);
                instructions.Add(lastRet);

                for (var index = 0; index < instructions.Count - 2; index++)
                {
                    var instruction = instructions[index];
                    if (instruction.OpCode == OpCodes.Ret)
                    {
                        instructions[index] = Instruction.Create(OpCodes.Leave, lastLd);
                        instructions.Insert(index, Instruction.Create(OpCodes.Stloc, returnVariable));
                        index++;
                    }
                }
                return lastLd;
            }
        }

        private static Instruction FirstInstructionSkipCtor(MethodDefinition method)
        {
            UPR.Cecil.Cil.MethodBody body = method.Body;
            if (method.IsConstructor && !method.IsStatic)
            {
                return body.Instructions.Skip(2).First();
            }
            return body.Instructions.First();
        }

        private static void InnerProcess(ModuleDefinition module, MethodDefinition method, Instruction lastcall)
        {
            UPR.Cecil.Cil.MethodBody body = method.Body;
            var ilProcessor = body.GetILProcessor();
            var returnInstruction = FixReturns(module, method);
            var firstInstruction = FirstInstructionSkipCtor(method);
            var beforeReturn = Instruction.Create(OpCodes.Nop);
            ilProcessor.InsertBefore(returnInstruction, beforeReturn);
            ilProcessor.InsertBefore(returnInstruction, lastcall);
            ilProcessor.InsertBefore(returnInstruction, Instruction.Create(OpCodes.Nop));
            ilProcessor.InsertBefore(returnInstruction, Instruction.Create(OpCodes.Nop));
            ilProcessor.InsertBefore(returnInstruction, Instruction.Create(OpCodes.Endfinally));

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = firstInstruction,
                TryEnd = beforeReturn,
                HandlerStart = beforeReturn,
                HandlerEnd = returnInstruction,
            };

            body.ExceptionHandlers.Add(handler);
            body.InitLocals = true;
        }
    }
}
