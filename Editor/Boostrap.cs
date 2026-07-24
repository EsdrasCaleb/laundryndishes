using System;
using System.IO;
using UnityEditor;
using LaundryNDishes.Core;
using LaundryNDishes.UI;
using UnityEngine;

namespace LaundryNDishes
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            EditorApplication.delayCall += RunStartupChecks;
        }

        private static void RunStartupChecks()
        {
            // Se o consentimento ou a configuração inicial não foi mostrada, abre o Wizard
            LnDConfig.instance.BoostrapWizardShown = false;
            if (!LnDConfig.instance.BoostrapWizardShown)
            {
                EditorApplication.delayCall += () =>
                {
                    LnDSetupWizard.ShowWindow();
                };
            }
        }
    }
}