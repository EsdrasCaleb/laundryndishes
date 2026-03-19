using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using LaundryNDishes.UnityCore; // Para acessar o TestDatabase

namespace LaundryNDishes.DomainAdapter
{
    // O atributo InitializeOnLoad garante que a Unity ligue esse ouvinte 
    // automaticamente sempre que o projeto for aberto ou compilar um código.
    [InitializeOnLoad]
    public class GlobalTestListener : ScriptableObject, ICallbacks
    {
        static GlobalTestListener()
        {
            // Registra este ScriptableObject como um ouvinte oficial da API de testes
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var listener = ScriptableObject.CreateInstance<GlobalTestListener>();
            api.RegisterCallbacks(listener);
        }

        public void RunStarted(ITestAdaptor testsToRun) { }

        // Disparado quando qualquer rotina de testes (grande ou pequena) termina.
        public void RunFinished(ITestResultAdaptor result)
        {
            var db = TestDatabase.Instance;
            if (db == null) return;

            bool dbNeedsSave = false;

            // Varre a árvore de resultados e atualiza o banco
            ProcessResultTree(result, db, ref dbNeedsSave);

            if (dbNeedsSave)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log("[LnD] TestDatabase atualizado com os resultados da última execução de testes!");
            }
        }

        private void ProcessResultTree(ITestResultAdaptor result, TestDatabase db, ref bool dbNeedsSave)
        {
            // O NUnit agrupa testes dentro de uma "Suite" (que geralmente é a Classe de Teste)
            if (result.Test.IsSuite)
            {
                string suiteFullName = result.Test.FullName; // Ex: "Namespace.MinhaClasseDeTeste"

                // Procura se essa classe pertence a algum dos nossos testes gerados
                foreach (var testData in db.AllTests)
                {
                    if (testData.GeneratedTestScript != null)
                    {
                        var scriptClass = testData.GeneratedTestScript.GetClass();
                        
                        // Se o nome completo da classe bater com a Suite do NUnit...
                        if (scriptClass != null && scriptClass.FullName == suiteFullName)
                        {
                            testData.passedTestCount = result.PassCount;
                            
                            // A soma de todos os estados nos dá o total de testes executados daquela classe
                            testData.numberOfTests = result.PassCount + result.FailCount + result.InconclusiveCount;
                            
                            dbNeedsSave = true;
                        }
                    }
                }
            }

            // Desce recursivamente para olhar os filhos (Assembly -> Namespace -> Classe -> Método)
            if (result.Children != null)
            {
                foreach (var child in result.Children)
                {
                    ProcessResultTree(child, db, ref dbNeedsSave);
                }
            }
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }
}