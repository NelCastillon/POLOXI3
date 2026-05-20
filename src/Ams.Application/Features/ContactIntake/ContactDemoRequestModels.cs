using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.ContactIntake;

public sealed class CreateContactDemoRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string WorkEmail { get; set; } = string.Empty;

    [Phone, StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }

    [Required, StringLength(200)]
    public string AgencyName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string AgencySize { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Branches { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string BusinessLines { get; set; } = string.Empty;

    [StringLength(200)]
    public string? CurrentSystem { get; set; }

    [Required, StringLength(50)]
    public string Timeline { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Budget { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Message { get; set; }

    [Required]
    public bool ConsentToContact { get; set; }

    public IReadOnlyList<string> Priorities { get; set; } = [];
}

public sealed class ContactDemoRequestDto
{
    public Guid RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Title { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public string AgencySize { get; set; } = string.Empty;
    public string Branches { get; set; } = string.Empty;
    public string BusinessLines { get; set; } = string.Empty;
    public string? CurrentSystem { get; set; }
    public string Timeline { get; set; } = string.Empty;
    public string Budget { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public IReadOnlyList<string> Priorities { get; set; } = [];
}

public sealed class ContactDemoSubmissionResult
{
    public Guid RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ContactIntakeOptionDto
{
    public string OptionType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
