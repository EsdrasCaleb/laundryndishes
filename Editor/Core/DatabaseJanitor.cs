using UnityEditor;
using System.Linq;
using UnityEngine;

namespace LaundryNDishes.Core
{
    /// <summary>
    /// Esta classe atua como um "zelador", monitorando a exclusão de assets
    /// para manter o TestDatabase limpo e sem referências quebradas.
    /// </summary>
    public class DatabaseJanitor : UnityEditor.AssetModificationProcessor
    {
        /// <summary>
        /// Este método é chamado pela Unity automaticamente ANTES de um asset ser deletado.
        /// </summary>
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            // 1. Ignora se o arquivo sendo deletado não for um script C#.
            if (!assetPath.EndsWith(".cs"))
            {
                return AssetDeleteResult.DidNotDelete;
            }

            // 2. Carrega a instância do nosso banco de dados.
            var db = Data.TestDatabase.Instance;
            if (db == null || db.AllTests == null || db.AllTests.Count == 0)
            {
                return AssetDeleteResult.DidNotDelete;
            }

            // 3. Usa o método 'RemoveAll' para varrer a lista e remover entradas órfãs.
            //    É mais seguro do que um loop 'foreach' ao modificar a lista.
            int itemsRemoved = db.AllTests.RemoveAll(testData =>
            {
                // Verifica se o TargetScript ou o GeneratedTestScript correspondem ao arquivo deletado.
                
                string targetPath = testData.TargetScript != null ? AssetDatabase.GetAssetPath(testData.TargetScript) : null;
                string generatedPath = testData.GeneratedTestScript != null ? AssetDatabase.GetAssetPath(testData.GeneratedTestScript) : null;

                return targetPath == assetPath || generatedPath == assetPath;
            });

            // 4. Se removemos algo, marcamos o banco de dados como "sujo" para que a Unity salve as alterações.
            if (itemsRemoved > 0)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log($"[DatabaseJanitor] Removidas {itemsRemoved} entradas órfãs do TestDatabase devido à exclusão de '{assetPath}'.");
            }

            // 5. Permite que a Unity continue com a exclusão do arquivo.
            return AssetDeleteResult.DidNotDelete;
        }
    }
}