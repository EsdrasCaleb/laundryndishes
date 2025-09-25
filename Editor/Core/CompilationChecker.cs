using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace LaundryNDishes.Core
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

        public async Task Run(string testCode)
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning("CompilationChecker já está em execução.");
                return;
            }

            CurrentState = State.SavingAndCompiling;
            CompilationErrors = new List<CompilationError>();
            TempFilePath = Path.Combine("Assets", "Temp", $"TempTestScript_{Guid.NewGuid()}.cs");

            // Criamos o "tradutor" de callback para Task.
            var compilationTaskSource = new TaskCompletionSource<List<CompilationError>>();

            // A função que será chamada quando o evento de compilação disparar.
            Action<string,CompilerMessage[]> onCompilationFinished = null;
            onCompilationFinished = (string s, CompilerMessage[] compilerMessages) =>
            {
                // 1. Desregistra o evento imediatamente para evitar memory leaks.
                CompilationPipeline.assemblyCompilationFinished -= onCompilationFinished;

                // 2. Coleta os erros de compilação APENAS do nosso arquivo temporário.
                var errors = compilerMessages.Where(m => m.type == CompilerMessageType.Error)
                    .Where(msg => msg.file == TempFilePath)
                    .Select(msg => new CompilationError { Line = msg.line, Message = msg.message })
                    .ToList();
                
                // 3. Completa a Task, "liberando" o await e passando os erros como resultado.
                compilationTaskSource.TrySetResult(errors);
            };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TempFilePath));
                await File.WriteAllTextAsync(TempFilePath, testCode);

                // Registra nosso callback para o evento.
                CompilationPipeline.assemblyCompilationFinished += onCompilationFinished;
                
                // Força a Unity a notar o novo arquivo e iniciar a compilação.
                AssetDatabase.ImportAsset(TempFilePath, ImportAssetOptions.ForceSynchronousImport);
                
                // Opcional: Força uma recarga se a importação não for suficiente para disparar a compilação.
                // EditorUtility.RequestScriptReload();
                
                // Espera aqui até que o callback chame 'TrySetResult'.
                CompilationErrors = await compilationTaskSource.Task;

                if (HasErrors)
                {
                    // Se houver erros, o arquivo já deve ser limpo.
                    AssetDatabase.DeleteAsset(TempFilePath);
                }
                else
                {
                    // Se compilou com sucesso, guarda o nome do assembly.
                    AssemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(TempFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CompilationErrors.Add(new CompilationError { Line = 0, Message = "Uma exceção ocorreu: " + ex.Message });
                if (File.Exists(TempFilePath)) AssetDatabase.DeleteAsset(TempFilePath);
            }
            finally
            {
                // Garante que o evento seja desregistrado mesmo se ocorrer uma exceção antes do await.
                CompilationPipeline.assemblyCompilationFinished -= onCompilationFinished;
                CurrentState = State.Finished;
            }
        }
    }
}