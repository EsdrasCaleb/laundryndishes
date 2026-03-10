using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaundryNDishes.UnityCore;
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
        [Serializable]
        private class AsmDefData { public string name; }
        
        public enum State { Idle, SavingAndCompiling, Finished }
        public State CurrentState { get; private set; } = State.Idle;
        
        public bool IsDone => CurrentState == State.Finished;
        public bool HasErrors => CompilationErrors.Count > 0;
        
        public List<CompilationError> CompilationErrors { get; private set; }
        
        // Mantive a propriedade caso precise ler o nome depois
        public string AssemblyName { get; private set; }

        // O parâmetro 'folder' foi mantido na assinatura para não quebrar o código
        // que chama essa função, mas ele será ignorado aqui dentro.
        public async Task Run(string testCode, string filename, string folder, LnDConfig config, bool isEditorTest)        
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
                
                var references = new HashSet<string>();
                var allAssemblies = CompilationPipeline.GetAssemblies();

                // Função local para achar o assembly compilado correspondente ao .asmdef e adicionar as refs
                void AddAssemblyAndItsReferences(UnityEditorInternal.AssemblyDefinitionAsset asmDefAsset)
                {
                    if (asmDefAsset == null) return;
                
                    string targetName = JsonUtility.FromJson<AsmDefData>(asmDefAsset.text).name;
                        var targetAsm = allAssemblies.FirstOrDefault(a => a.name == targetName);
                
                    if (targetAsm != null)
                    {
                        references.Add(targetAsm.outputPath); // Adiciona a DLL do próprio assembly
                        foreach (var refPath in targetAsm.compiledAssemblyReferences)
                        {
                            references.Add(refPath); // Adiciona as dependências dele (UnityEngine, etc)
                        }
                    }
                }

                // Adiciona as referências do Projeto Principal (onde fica o script testado)
                AddAssemblyAndItsReferences(config.MainProjectAssembly);

                // Adiciona as referências do Projeto de Testes correto (onde estão os utilitários de teste e NUnit)
                var testAssemblyTarget = isEditorTest ? config.EditorTestAssembly : config.PlayModeTestAssembly;
                AddAssemblyAndItsReferences(testAssemblyTarget);

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
                    Debug.Log($"[Checker] {CompilationErrors.Count} erro(s) encontrados na validação de {filename}.\n " +
                              $"{string.Join('\n',CompilationErrors)}");
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