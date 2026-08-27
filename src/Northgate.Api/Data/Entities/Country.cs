using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Northgate.Api.Data.Entities;

/// <summary>ISO 3166-1 alpha-2 lookup. Maps <c>public.countries</c>.</summary>
[Table("countries")]
[Index(nameof(Name), IsUnique = true, Name = "countries_name_key")]
public sealed class Country
{
    [Key]
    [Column("code", TypeName = "char(2)")]
    [StringLength(2, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [InverseProperty(nameof(Customer.Country))]
    public ICollection<Customer> Customers { get; } = new List<Customer>();
}
