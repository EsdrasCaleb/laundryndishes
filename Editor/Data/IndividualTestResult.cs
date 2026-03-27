using System;

namespace LaundryNDishes.Data
{
    public enum SingleTestStatus
    {
        Unknown,
        Passed,
        Failed,
        Inconclusive,
        Skipped
    }

    [Serializable]
    public class IndividualTestResult
    {
        public string MethodName; // Nome curto para a UI (ex: "Testa_Lavar_Prato")
        public string FullName; // Caminho completo pro Runner achar (ex: "Namespace.Classe.Testa_Lavar_Prato")
        public SingleTestStatus Status;
    }
}