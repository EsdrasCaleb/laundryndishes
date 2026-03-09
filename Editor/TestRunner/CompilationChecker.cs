using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        
        // Mantive a propriedade caso precise ler o nome depois
        public string AssemblyName { get; private set; }

        // O parâmetro 'folder' foi mantido na assinatura para não quebrar o código
        // que chama essa função, mas ele será ignorado aqui dentro.
        public async Task Run(string testCode, string filename, string folder)
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning("CompilationChecker já está em execução.");
                return;
            }

            CurrentState = State.SavingAndCompiling;
            CompilationErrors = new List<CompilationError>();
            
            // 1. Definimos um diretório seguro DENTRO da pasta Temp do projeto Unity.
            // A pasta Temp é ignorada pelo AssetDatabase, então não causa recarregamentos.
            string tempDirectory = Path.Combine("Temp", "LnD_Validation");
            Directory.CreateDirectory(tempDirectory);

            // 2. Caminhos para o script C# temporário e a DLL temporária
            string tempCsPath = Path.Combine(tempDirectory, $"{filename}_Validation.cs");
            string tempDllPath = Path.Combine(tempDirectory, $"{filename}_Validation.dll");
            
            var compilationTaskSource = new TaskCompletionSource<List<CompilationError>>();

            try
            {
                // Salva o "rascunho" na pasta Temp
                await File.WriteAllTextAsync(tempCsPath, testCode);

                // 3. Usa o AssemblyBuilder apontando para o arquivo rascunho
                var assemblyBuilder = new AssemblyBuilder(tempDllPath, new[] { tempCsPath });
                
                assemblyBuilder.flags = AssemblyBuilderFlags.EditorAssembly;

                // Coleta todas as DLLs (UnityEngine, NUnit, e os seus próprios scripts como o "Ball")
                var references = new HashSet<string>();
                foreach (var asm in CompilationPipeline.GetAssemblies())
                {
                    // Adiciona os assemblies do seu projeto (ex: Assembly-CSharp.dll onde o Ball está)
                    references.Add(asm.outputPath);
                    
                    // Adiciona as referências base da Unity (ex: UnityEngine.CoreModule, nunit.framework)
                    foreach (var refPath in asm.compiledAssemblyReferences)
                    {
                        references.Add(refPath);
                    }
                }

                // Injeta todas as dependências no nosso compilador isolado
                assemblyBuilder.additionalReferences = references.ToArray();

                assemblyBuilder.buildFinished += (string assemblyPath, CompilerMessage[] compilerMessages) =>
                {
                    var errors = compilerMessages
                        .Where(m => m.type == CompilerMessageType.Error)
                        .Select(msg => new CompilationError { Line = msg.line, Message = msg.message })
                        .ToList();
                    
                    compilationTaskSource.TrySetResult(errors);
                };

                // Inicia a compilação isolada
                assemblyBuilder.Build();
                
                // Aguarda o resultado
                CompilationErrors = await compilationTaskSource.Task;

                if (HasErrors)
                {
                    Debug.Log($"[Checker] {CompilationErrors.Count} erro(s) encontrados na validação de {filename}.");
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
                // 4. LIMPEZA TOTAL: Apaga o .cs e a .dll rascunhos. 
                // Nenhum lixo fica no projeto!
                if (File.Exists(tempCsPath)) File.Delete(tempCsPath);
                if (File.Exists(tempDllPath)) File.Delete(tempDllPath);
                
                CurrentState = State.Finished;
            }
        }
    }
}