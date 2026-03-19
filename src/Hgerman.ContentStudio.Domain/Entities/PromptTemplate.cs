using Hgerman.ContentStudio.Domain.Common;

namespace Hgerman.ContentStudio.Domain.Entities;

public class PromptTemplate : BaseEntity
{
    public int PromptTemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptFormat { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
