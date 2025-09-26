using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LaundryNDishes.Data;
using Scriban;
using Scriban.Runtime;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public enum PromptType { Uniti, Behavior, Integration }

    public class PromptBuilder
    {
        private readonly Dictionary<string, Template> _templateCache = new Dictionary<string, Template>();
        private readonly string _customTemplatesPath;
        private readonly string _defaultTemplatesPath;

        public PromptBuilder()
        {
            _customTemplatesPath = LnDConfig.Instance.CustomTemplatesFolder;
            string scriptPath = GetCurrentFilePath();
            string corePath = Path.GetDirectoryName(scriptPath);
            string editorPath = Path.GetDirectoryName(corePath);
            _defaultTemplatesPath = Path.Combine(editorPath, "Templates");
        }

        public Prompt BuildIntentionPrompt(PromptType promptType, string sutClass, string[] sutRelatedMethods, string extra)
        {
            var data = new ScriptObject { ["sut_class"] = sutClass, ["sut_related_methods"] = sutRelatedMethods, ["extra"] = extra };
            return BuildPromptFromTemplates(promptType, "Intention", data);
        }

        public Prompt BuildGeneratorPrompt(PromptType promptType, string intention, string sutClass, string[] sutRelatedMethods, string extra)
        {
            var data = new ScriptObject { ["intention"] = intention, ["sut_class"] = sutClass, ["sut_related_methods"] = sutRelatedMethods, ["extra"] = extra };
            return BuildPromptFromTemplates(promptType, "Generator", data);
        }

        public Prompt BuildCorrectionPrompt(PromptType promptType, string code, string errors)
        {
            var data = new ScriptObject { ["code"] = code, ["errors"] = errors };
            return BuildPromptFromTemplates(promptType, "Correction", data);
        }

        private Prompt BuildPromptFromTemplates(PromptType promptType, string baseName, ScriptObject data)
        {
            var prompt = new Prompt();
            
            // Monta o nome do arquivo com o prefixo do tipo de prompt.
            string systemTemplate = $"{promptType.ToString()}_{baseName}_System.scriban";
            string userTemplate = $"{promptType.ToString()}_{baseName}_User.scriban";

            prompt.Messages.Add(new ChatMessage { role = "system", content = RenderTemplate(systemTemplate, data) });
            prompt.Messages.Add(new ChatMessage { role = "user", content = RenderTemplate(userTemplate, data) });
            return prompt;
        }

        private string RenderTemplate(string templateFileName, ScriptObject data)
        {
            // A lógica de fallback para templates customizados permanece a mesma.
            string templatePath = "";
            bool useCustomPath = !string.IsNullOrEmpty(_customTemplatesPath) && Directory.Exists(_customTemplatesPath);
            if (useCustomPath)
            {
                string customFilePath = Path.Combine(_customTemplatesPath, templateFileName);
                if (File.Exists(customFilePath))
                {
                    templatePath = customFilePath;
                }
            }
            if (string.IsNullOrEmpty(templatePath))
            {
                templatePath = Path.Combine(_defaultTemplatesPath, templateFileName);
            }

            // O resto da lógica de cache e renderização permanece.
            if (!_templateCache.TryGetValue(templatePath, out var template))
            {
                if (!File.Exists(templatePath))
                {
                    Debug.LogError($"Template não encontrado: {templateFileName}");
                    return $"ERRO: Template '{templateFileName}' não encontrado.";
                }
                string templateContent = File.ReadAllText(templatePath);
                template = Template.Parse(templateContent);
                _templateCache[templatePath] = template;
            }
            var context = new TemplateContext { MemberRenamer = member => member.Name };
            context.PushGlobal(data);
            return template.Render(context);
        }
        
        private static string GetCurrentFilePath([CallerFilePath] string path = "") => path;
    }
}