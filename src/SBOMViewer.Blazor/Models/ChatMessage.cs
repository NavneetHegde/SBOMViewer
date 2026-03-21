namespace SBOMViewer.Blazor.Models;

public record ChatMessage(string Role, string Content, DateTime Timestamp);
