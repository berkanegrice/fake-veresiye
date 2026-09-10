using System.ComponentModel.DataAnnotations;

namespace FakeVeresiye.Api.Dtos;

public record CreateCustomerRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(60)] string? Phone,
    [MaxLength(2000)] string? Notes);

public record CustomerListItem(int Id, string Name, string? Phone, decimal Balance);

public record CustomerDetailResponse(
    int Id,
    string Name,
    string? Phone,
    string? Notes,
    decimal Balance,
    int TransactionCount);
