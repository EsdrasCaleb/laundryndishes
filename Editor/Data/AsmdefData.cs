using System;

namespace LaundryNDishes.Data
{
    [Serializable]
    public class AsmdefData
    {
        public string name;
        public string rootNamespace;
        public string[] references;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public bool allowUnsafeCode;
        public bool overrideReferences;
        public string[] precompiledReferences;
        public bool autoReferenced;
        public string[] defineConstraints;
        public string[] versionDefines;
        public bool noEngineReferences;
        
        // Esta é a propriedade mais importante para nós.
        public string[] optionalUnityReferences; 
    }
}