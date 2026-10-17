using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaccuinoRistoranti.Api.Contracts;
using TaccuinoRistoranti.Api.Data;
using TaccuinoRistoranti.Api.Models;

namespace TaccuinoRistoranti.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MealsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MealResponse>>> GetMyMeals()
    {
        var currentUserId = GetCurrentUserId();

        var meals = await dbContext.Meals
            .Include(x => x.Ratings)
            .ThenInclude(r => r.User)
            .Where(m => m.CreatedByUserId == currentUserId || m.Ratings.Any(r => r.UserId == currentUserId))
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync();

        return Ok(meals.Select(m => ToResponse(m, currentUserId)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MealResponse>> CreateMeal(CreateMealRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var shareCode = await GenerateUniqueShareCode();

        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Street = request.Street,
            City = request.City,
            Notes = request.Notes,
            MealType = request.MealType,
            ShareCode = shareCode,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        dbContext.Meals.Add(meal);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMealById), new { mealId = meal.Id }, ToResponse(meal, currentUserId));
    }

    [HttpGet("{mealId:guid}")]
    public async Task<ActionResult<MealResponse>> GetMealById(Guid mealId)
    {
        var currentUserId = GetCurrentUserId();
        var meal = await dbContext.Meals
            .Include(m => m.Ratings)
            .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == mealId);

        if (meal is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(meal, currentUserId));
    }

    [HttpPost("join")]
    public async Task<ActionResult<MealResponse>> JoinByCode(JoinMealRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var code = request.ShareCode.Trim().ToUpperInvariant();

        var meal = await dbContext.Meals
            .Include(m => m.Ratings)
            .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.ShareCode == code);

        if (meal is null)
        {
            return NotFound("Codice pasto non valido.");
        }

        return Ok(ToResponse(meal, currentUserId));
    }

    [HttpPost("{mealId:guid}/ratings")]
    public async Task<ActionResult<MealResponse>> SubmitRating(Guid mealId, SubmitRatingRequest request)
    {
        var currentUserId = GetCurrentUserId();

        if (!IsValidVote(request))
        {
            return BadRequest("I voti devono essere compresi tra 0 e 10.");
        }

        var meal = await dbContext.Meals
            .Include(m => m.Ratings)
            .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == mealId);

        if (meal is null)
        {
            return NotFound();
        }

        var existing = meal.Ratings.FirstOrDefault(r => r.UserId == currentUserId);
        if (existing is null)
        {
            var rating = new Rating
            {
                Id = Guid.NewGuid(),
                MealId = mealId,
                UserId = currentUserId,
                CreatedAtUtc = DateTime.UtcNow,
                Location = request.Location,
                Menu = request.Menu,
                Service = request.Service,
                Bill = request.Bill,
                Special = request.Special
            };

            dbContext.Ratings.Add(rating);
        }
        else
        {
            existing.Location = request.Location;
            existing.Menu = request.Menu;
            existing.Service = request.Service;
            existing.Bill = request.Bill;
            existing.Special = request.Special;
        }

        await dbContext.SaveChangesAsync();

        var updatedMeal = await dbContext.Meals
            .Include(m => m.Ratings)
            .ThenInclude(r => r.User)
            .FirstAsync(m => m.Id == mealId);

        return Ok(ToResponse(updatedMeal, currentUserId));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Utente non autenticato.");
    }

    private static bool IsValidVote(SubmitRatingRequest request)
    {
        var values = new[] { request.Location, request.Menu, request.Service, request.Bill, request.Special };
        return values.All(v => v is >= 0 and <= 10);
    }

    private async Task<string> GenerateUniqueShareCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        while (true)
        {
            var code = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());

            var exists = await dbContext.Meals.AnyAsync(m => m.ShareCode == code);
            if (!exists)
            {
                return code;
            }
        }
    }

    private static MealResponse ToResponse(Meal meal, string currentUserId)
    {
        var ratings = meal.Ratings.ToList();
        var votes = ratings.Count;

        var location = votes == 0 ? 0 : ratings.Average(r => r.Location);
        var menu = votes == 0 ? 0 : ratings.Average(r => r.Menu);
        var service = votes == 0 ? 0 : ratings.Average(r => r.Service);
        var bill = votes == 0 ? 0 : ratings.Average(r => r.Bill);
        var special = votes == 0 ? 0 : ratings.Average(r => r.Special);

        var summary = new RatingSummaryResponse(
            Math.Round(location, 2),
            Math.Round(menu, 2),
            Math.Round(service, 2),
            Math.Round(bill, 2),
            Math.Round(special, 2),
            Math.Round((location + menu + service + bill + special) / 5, 2),
            votes);

        var userRatingEntity = ratings.FirstOrDefault(r => r.UserId == currentUserId);
        var userRating = userRatingEntity is null
            ? null
            : new UserRatingResponse(
                userRatingEntity.Location,
                userRatingEntity.Menu,
                userRatingEntity.Service,
                userRatingEntity.Bill,
                userRatingEntity.Special,
                userRatingEntity.CreatedAtUtc);

        var leaderboard = ratings
            .Select(r => new ParticipantScoreResponse(
                r.UserId,
                r.User?.Email ?? "utente-sconosciuto",
                r.Location,
                r.Menu,
                r.Service,
                r.Bill,
                r.Special,
                Math.Round((r.Location + r.Menu + r.Service + r.Bill + r.Special) / 5.0, 2),
                0,
                r.CreatedAtUtc))
            .OrderByDescending(x => x.Overall)
            .ThenBy(x => x.CreatedAtUtc)
            .Select((x, index) => x with { Position = index + 1 })
            .ToList();

        return new MealResponse(
            meal.Id,
            meal.Name,
            meal.Street,
            meal.City,
            meal.Notes,
            meal.MealType,
            meal.ShareCode,
            meal.CreatedAtUtc,
            meal.CreatedByUserId,
            ratings.Any(r => r.UserId == currentUserId),
            userRating,
            summary,
            leaderboard);
    }
}
