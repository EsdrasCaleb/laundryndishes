using System.Threading.Tasks;
using LaundryNDishes.TestRunner;
using LaundryNDishes.UnityCore;

namespace LaundryNDishes.Core
{
    public class Orquestrator
    {
        public async Task<(bool compiles, string FilePath)> CompilePlayTest(string code)
        {
            return await CompileTest(code, LnDConfig.Instance.PlayTestDestinationFolder);
        }

        public async Task<(bool compiles, string FilePath)> CompileEditorTest(string code)
        {
            return await CompileTest(code, LnDConfig.Instance.EditorTestScriptsFolder);
        }

        private async Task<(bool compiles, string FilePath)> CompileTest(string code, string destinationFolder)
        {
            var checker = new CompilationChecker();
            string tempFileNameBase = "TEMPTEST";

            await checker.Run(code, tempFileNameBase, destinationFolder);

            return (!checker.HasErrors, checker.TempFilePath);
        }

        public void saveTest(string code,string filePath)
        {
            
        }

        private async Task<bool?> TestTest(string code, string destinationFolder)
        {
            UnitTestGenerator utg = new UnitTestGenerator();
            return await utg.ExecuteTestAsync(code,destinationFolder);   
        }

    }
}