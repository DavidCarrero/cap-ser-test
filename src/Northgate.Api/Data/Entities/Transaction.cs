using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Northgate.Api.Data.Entities;

/// <summary>Maps <c>public.transactions</c>.</summary>
[Table("transactions")]
// Serves both the customer filter and the ORDER BY created_at DESC page.
[Index(nameof(CustomerId), nameof(CreatedAt), Name = "transactions_customer_created", IsDescending = new[] { false, true })]
[Index(nameof(CreatedAt), Name = "transactions_created_at", AllDescending = true)]
[Index(nameof(CurrencyCode), Name = "transactions_currency_code")]
[Index(nameof(FxRateId), Name = "transactions_fx_rate_id")]
public sealed class Transaction
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("customer_id")]
    public long CustomerId { get; set; }

    /// <summary>Money is decimal-backed <c>numeric(19,4)</c>, never float.</summary>
    [Column("amount")]
    [Precision(19, 4)]
    public decimal Amount { get; set; }

    [Required]
    [Column("currency_code", TypeName = "char(3)")]
    [StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The quote applied when this settled. NULL until converted. Stored rather
    /// than recomputed because it is a historical fact; <c>amount * rate</c> is
    /// derivable and so is deliberately not a column.
    /// </summary>
    [Column("fx_rate_id")]
    public long? FxRateId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    [InverseProperty(nameof(Entities.Customer.Transactions))]
    public Customer? Customer { get; set; }

    [ForeignKey(nameof(CurrencyCode))]
    [InverseProperty(nameof(Entities.Currency.Transactions))]
    public Currency? Currency { get; set; }

    [ForeignKey(nameof(FxRateId))]
    public FxRate? FxRate { get; set; }
}
