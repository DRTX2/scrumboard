namespace ScrumBoard.Application.Models.Reports;

public sealed record GeneratedReport(byte[] Content, string MediaType, string FileName);
