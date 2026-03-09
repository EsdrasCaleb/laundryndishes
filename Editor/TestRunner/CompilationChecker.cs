using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

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
        public enum State { Idle, SavingAndCompiling, Finished }
        public State CurrentState { get; private set; } = State.Idle;
        
        public bool IsDone => CurrentState == State.Finished;
        public bool HasErrors => CompilationErrors.Count > 0;
        
        public List<CompilationError> CompilationErrors { get; private set; }
        public string TempFilePath { get; private set; }
        public string AssemblyName { get; private set; }

        public async Task Run(string testCode, string filename, string folder)
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning("CompilationChecker já está em execução.");
                return;
            }

            CurrentState = State.SavingAndCompiling;
            CompilationErrors = new List<CompilationError>();
            
            // Salvamos o arquivo fisicamente. Como o Editor está "Locked", a Unity
            // não vai tentar importar isso automaticamente para o projeto principal ainda.
            TempFilePath = Path.Combine(folder, $"{filename}_Test.cs");
            var compilationTaskSource = new TaskCompletionSource<List<CompilationError>>();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TempFilePath));
                await File.WriteAllTextAsync(TempFilePath, testCode);

                // --- A MÁGICA ACONTECE AQUI ---
                // Criamos uma DLL temporária na pasta Temp da Unity (fora dos Assets)
                string tempDllPath = Path.Combine("Temp", $"{filename}_TempValidation.dll");
                
                // Usamos o AssemblyBuilder para compilar APENAS esse arquivo
                var assemblyBuilder = new AssemblyBuilder(tempDllPath, new[] { TempFilePath });

                // Evento que dispara quando a compilação isolada termina
                assemblyBuilder.buildFinished += (string assemblyPath, CompilerMessage[] compilerMessages) =>
                {
                    // Filtramos apenas os erros
                    var errors = compilerMessages
                        .Where(m => m.type == CompilerMessageType.Error)
                        .Select(msg => new CompilationError { Line = msg.line, Message = msg.message })
                        .ToList();
                    
                    compilationTaskSource.TrySetResult(errors);
                };

                // Inicia a compilação em background (NÃO causa Domain Reload)
                assemblyBuilder.Build();
                
                // Esperamos o callback do AssemblyBuilder
                CompilationErrors = await compilationTaskSource.Task;

                if (HasErrors)
                {
                    // Erros encontrados (a IA vai tentar corrigir graças ao seu loop)
                    Debug.Log($"[Checker] {CompilationErrors.Count} erro(s) encontrados na validação isolada.");
                }
                else
                {
                    // Sucesso!
                    AssemblyName = filename; 
                }

                // Limpa a DLL temporária, pois só precisávamos dela para validar os erros
                if (File.Exists(tempDllPath)) File.Delete(tempDllPath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CompilationErrors.Add(new CompilationError { Line = 0, Message = "Exceção: " + ex.Message });
            }
            finally
            {
                CurrentState = State.Finished;
            }
        }
    }
}