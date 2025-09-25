using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LaundryNDishes.Data;
using Scriban;
using Scriban.Runtime;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public class PromptBuilder
    {
        // Cache e caminhos agora são baseados na instância.
        private readonly Dictionary<string, Template> _templateCache = new Dictionary<string, Template>();
        private readonly string _customTemplatesPath;
        private readonly string _defaultTemplatesPath;

        public PromptBuilder()
        {
            // Pega o caminho customizado da configuração.
            _customTemplatesPath = LnDConfig.Instance.CustomTemplatesFolder;

            // Calcula o caminho padrão de forma robusta.
            string scriptPath = GetCurrentFilePath(); // Pega o caminho deste arquivo
            string corePath = Path.GetDirectoryName(scriptPath);
            string editorPath = Path.GetDirectoryName(corePath);
            _defaultTemplatesPath = Path.Combine(editorPath, "Templates");
        }

        public Prompt BuildIntentionPrompt(string sutClass, string[] sutRelatedMethods, string sutMethod)
        {
            var data = new ScriptObject { ["sut_class"] = sutClass, ["sut_related_methods"] = sutRelatedMethods, ["sut_method"] = sutMethod };
            return BuildPromptFromTemplates("Intention", data);
        }

        public Prompt BuildGeneratorPrompt(string intention, string sutClass, string[] sutRelatedMethods, string sutMethod)
        {
            var data = new ScriptObject { ["intention"] = intention, ["sut_class"] = sutClass, ["sut_related_methods"] = sutRelatedMethods, ["sut_method"] = sutMethod };
            return BuildPromptFromTemplates("Generator", data);
        }

        public Prompt BuildCorrectionPrompt(string code, string errors)
        {
            var data = new ScriptObject { ["code"] = code, ["errors"] = errors };
            return BuildPromptFromTemplates("Correction", data);
        }

        private Prompt BuildPromptFromTemplates(string baseName, ScriptObject data)
        {
            var prompt = new Prompt();
            prompt.Messages.Add(new ChatMessage { role = "system", content = RenderTemplate($"{baseName}_System.scriban", data) });
            prompt.Messages.Add(new ChatMessage { role = "user", content = RenderTemplate($"{baseName}_User.scriban", data) });
            return prompt;
        }

        private string RenderTemplate(string templateFileName, ScriptObject data)
        {
            string templatePath = "";
            bool useCustomPath = !string.IsNullOrEmpty(_customTemplatesPath) && Directory.Exists(_customTemplatesPath);
            
            if (useCustomPath)
            {
                templatePath = Path.Combine(_customTemplatesPath, templateFileName);
            }
            if (!File.Exists(templatePath))
            {
                templatePath = Path.Combine(_defaultTemplatesPath, templateFileName);
            }
            if (!_templateCache.TryGetValue(templatePath, out var template))
            {
                if (!File.Exists(templatePath))
                {
                    Debug.LogError($"Template não encontrado nos caminhos customizado ou padrão: {templateFileName}");
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