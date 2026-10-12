using System.ComponentModel.DataAnnotations;
namespace InternManagement.DTOs;
public sealed record ContractResponse(int InternId, string FullName, string Email, string FileName, long Size,
    DateTime UploadedAt, DateTime? ConfirmedAt, string Version);
public sealed record ConfirmContractRequest
{
    [Required] public required string Version { get; init; }
}
