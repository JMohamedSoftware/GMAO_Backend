using System;

namespace GMAO.Application.DTOs.Documents;

public class DocumentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public int? EquipementId { get; set; }
    public int? OTId { get; set; }
    public DateTime CreatedAt { get; set; }
}
