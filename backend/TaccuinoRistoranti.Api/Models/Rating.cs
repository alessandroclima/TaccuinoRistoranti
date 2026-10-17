namespace TaccuinoRistoranti.Api.Models;

public class Rating
{
    public Guid Id { get; set; }
    public Guid MealId { get; set; }
    public Meal? Meal { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int Location { get; set; }
    public int Menu { get; set; }
    public int Service { get; set; }
    public int Bill { get; set; }
    public int Special { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
