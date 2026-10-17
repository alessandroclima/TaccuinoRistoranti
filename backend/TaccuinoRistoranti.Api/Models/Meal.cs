namespace TaccuinoRistoranti.Api.Models;

public class Meal
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public MealType MealType { get; set; }
    public string ShareCode { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}
