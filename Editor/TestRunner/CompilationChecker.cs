using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaundryNDishes.UnityCore;
using UnityEditor.Compilation;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LaundryNDishes.TestRunner
{
    public struct CompilationError
    {
        public int Line;
        public string Message;
        public override string ToString() => $"Line {Line}: {Message}";
    }

    public class CompilationChecker
    {
        [Serializable]
        private class AsmDefData { public string name; }
        
        public enum State { Idle, Compiling, Finished }
        public State CurrentState { get; private set; } = State.Idle;
        
        public bool IsDone => CurrentState == State.Finished;
        public bool HasErrors => CompilationErrors.Count > 0;
        
        public List<CompilationError> CompilationErrors { get; private set; }
        
        public string AssemblyName { get; private set; }

        public async Task Run(string testCode, string filename, string folder, LnDConfig config, bool isEditorTest)        
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning("CompilationChecker já está em execução.");
                return;
            }

            CurrentState = State.Compiling;
            CompilationErrors = new List<CompilationError>();

            try
            {
                // 1. Coleta os caminhos das DLLs de referência (Isso DEVE rodar na Main Thread)
                var referencesPaths = new HashSet<string>();
                var allAssemblies = CompilationPipeline.GetAssemblies();

                void AddAssemblyAndItsReferences(UnityEditorInternal.AssemblyDefinitionAsset asmDefAsset)
                {
                    if (asmDefAsset == null) return;
                
                    string targetName = JsonUtility.FromJson<AsmDefData>(asmDefAsset.text).name;
                    var targetAsm = allAssemblies.FirstOrDefault(a => a.name == targetName);
                
                    if (targetAsm != null)
                    {
                        referencesPaths.Add(targetAsm.outputPath); 
                        foreach (var refPath in targetAsm.compiledAssemblyReferences)
                        {
                            referencesPaths.Add(refPath); 
                        }
                    }
                }

                // A) Adiciona as dependências explícitas dos seus projetos
                AddAssemblyAndItsReferences(config.MainProjectAssembly);
                var testAssemblyTarget = isEditorTest ? config.EditorTestAssembly : config.PlayModeTestAssembly;
                AddAssemblyAndItsReferences(testAssemblyTarget);

                // B) A MÁGICA DA FORÇA BRUTA: Varre o domínio da aplicação.
                // Isso pega o NUnit, o Unity Test Framework, o Core do C#, e todas as pacotes escondidos.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                    {
                        referencesPaths.Add(assembly.Location);
                    }
                }

                // 2. Transforma tudo em tarefa assíncrona (Task.Run) para a compilação não pesar na UI
                await Task.Run(() =>
                {
                    // A. Transforma os caminhos em Referências do Roslyn
                    var metadataReferences = referencesPaths
                        .Select(path => MetadataReference.CreateFromFile(path))
                        .ToList();

                    // B. Cria a Árvore Sintática direto da string
                    var syntaxTree = CSharpSyntaxTree.ParseText(testCode);

                    // C. Prepara a compilação do Roslyn
                    var compilation = CSharpCompilation.Create(
                        $"{filename}_Validation",
                        new[] { syntaxTree },
                        metadataReferences,
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    );

                    // D. Compila para a Memória (MemoryStream) em vez do disco
                    using var memoryStream = new MemoryStream();
                    var emitResult = compilation.Emit(memoryStream);

                    // E. Lê o resultado
                    if (!emitResult.Success)
                    {
                        CompilationErrors = emitResult.Diagnostics
                            .Where(diag => diag.Severity == DiagnosticSeverity.Error)
                            .Select(diag => new CompilationError 
                            { 
                                Line = diag.Location.GetLineSpan().StartLinePosition.Line + 1, 
                                Message = diag.GetMessage() 
                            })
                            .ToList();
                    }
                });

                if (HasErrors)
                {
                    Debug.Log($"[Checker] {CompilationErrors.Count} erro(s) encontrados na validação de {filename}.\n" +
                              $"{string.Join('\n', CompilationErrors)}");
                }
                else
                {
                    AssemblyName = filename; 
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CompilationErrors.Add(new CompilationError { Line = 0, Message = "Exceção Interna: " + ex.Message });
            }
            finally
            {
                CurrentState = State.Finished;
            }
        }
    }
}