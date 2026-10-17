using TaccuinoRistoranti.Api.Models;

namespace TaccuinoRistoranti.Api.Contracts;

public record CreateMealRequest(
    string Name,
    string Street,
    string City,
    string? Notes,
    MealType MealType);

public record JoinMealRequest(string ShareCode);

public record SubmitRatingRequest(
    int Location,
    int Menu,
    int Service,
    int Bill,
    int Special);

public record RatingSummaryResponse(
    double Location,
    double Menu,
    double Service,
    double Bill,
    double Special,
    double Overall,
    int Votes);

public record ParticipantScoreResponse(
    string UserId,
    string UserEmail,
    int Location,
    int Menu,
    int Service,
    int Bill,
    int Special,
    double Overall,
    int Position,
    DateTime CreatedAtUtc);

public record UserRatingResponse(
    int Location,
    int Menu,
    int Service,
    int Bill,
    int Special,
    DateTime CreatedAtUtc);

public record MealResponse(
    Guid Id,
    string Name,
    string Street,
    string City,
    string? Notes,
    MealType MealType,
    string ShareCode,
    DateTime CreatedAtUtc,
    string CreatedBy,
    bool HasUserRated,
    UserRatingResponse? UserRating,
    RatingSummaryResponse Summary,
    IReadOnlyList<ParticipantScoreResponse> Leaderboard);
