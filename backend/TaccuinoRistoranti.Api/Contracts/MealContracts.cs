using TaccuinoRistoranti.Api.Models;

namespace TaccuinoRistoranti.Api.Contracts;

public record CreateMealRequest(
    string Name,
    string ProposedBy,
    string Street,
    string City,
    string? Notes,
    MealType MealType,
    bool VotesSecret);

public record JoinMealRequest(string ShareCode);

public record UpdateVotesSecretRequest(bool VotesSecret);

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
    string ProposedBy,
    string Street,
    string City,
    string? Notes,
    MealType MealType,
    string ShareCode,
    DateTime CreatedAtUtc,
    string CreatedBy,
    bool VotesSecret,
    bool CanToggleVotesSecret,
    bool HasUserRated,
    UserRatingResponse? UserRating,
    RatingSummaryResponse Summary,
    IReadOnlyList<ParticipantScoreResponse> Leaderboard);
