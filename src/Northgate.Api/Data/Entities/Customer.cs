using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Northgate.Api.Data.Entities;

/// <summary>Maps <c>public.customers</c>.</summary>
[Table("customers")]
// A document number is only unique within the country that issued it.
[Index(nameof(CountryCode), nameof(DocumentNumber), IsUnique = true, Name = "customers_document_unique_per_country")]
[Index(nameof(CountryCode), Name = "customers_country_code")]
public sealed class Customer
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Column("document_number")]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required]
    [Column("country_code", TypeName = "char(2)")]
    [StringLength(2, MinimumLength = 2)]
    public string CountryCode { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(CountryCode))]
    [InverseProperty(nameof(Entities.Country.Customers))]
    public Country? Country { get; set; }

    [InverseProperty(nameof(Transaction.Customer))]
    public ICollection<Transaction> Transactions { get; } = new List<Transaction>();
}
