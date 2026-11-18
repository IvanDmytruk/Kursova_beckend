
using Beckend.Models;
using Beckend.Services.Teams;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public sealed class TournamentRequestIndexTests
    : IClassFixture<MongoDbFixture>
{
    private readonly IMongoCollection<TournamentParticipationRequest>
        _requests;

    private readonly TeamWorkflowIndexes _indexes;

    public TournamentRequestIndexTests(MongoDbFixture fixture)
    {
        _requests = fixture.Database
            .GetCollection<TournamentParticipationRequest>(
                "TournamentParticipationRequests");

        _indexes = new TeamWorkflowIndexes(fixture.Database);
    }

    [Fact]
    public async Task ConcurrentRequests_SameTarget_OnlyOneIsInserted()
    {
        await _indexes.EnsureAsync();

        var tournamentId = ObjectId.GenerateNewId().ToString();
        var teamId = ObjectId.GenerateNewId().ToString();

        async Task<bool> TryInsertAsync()
        {
            var request = new TournamentParticipationRequest
            {
                TournamentId = tournamentId,
                TeamId = teamId,
                PlayerId = "",
                Type = ParticipationRequestType.Application,
                Status = ParticipationRequestStatus.Pending,
                CreatedByUserId =
                    ObjectId.GenerateNewId().ToString()
            };

            try
            {
                await _requests.InsertOneAsync(request);
                return true;
            }
            catch (MongoWriteException ex)
                when (ex.WriteError.Category ==
                      ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            TryInsertAsync(),
            TryInsertAsync()
        );

        Assert.Single(results, success => success);

        var count = await _requests.CountDocumentsAsync(x =>
            x.TournamentId == tournamentId &&
            x.TeamId == teamId &&
            x.PlayerId == "" &&
            x.Status == ParticipationRequestStatus.Pending);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessedRequest_AllowsNewPendingRequest()
    {
        await _indexes.EnsureAsync();

        var tournamentId = ObjectId.GenerateNewId().ToString();
        var teamId = ObjectId.GenerateNewId().ToString();

        var first = new TournamentParticipationRequest
        {
            TournamentId = tournamentId,
            TeamId = teamId,
            PlayerId = "",
            Status = ParticipationRequestStatus.Rejected,
            Type = ParticipationRequestType.Application
        };

        var second = new TournamentParticipationRequest
        {
            TournamentId = tournamentId,
            TeamId = teamId,
            PlayerId = "",
            Status = ParticipationRequestStatus.Pending,
            Type = ParticipationRequestType.Application
        };

        await _requests.InsertOneAsync(first);
        await _requests.InsertOneAsync(second);

        var count = await _requests.CountDocumentsAsync(x =>
            x.TournamentId == tournamentId &&
            x.TeamId == teamId);

        Assert.Equal(2, count);
    }
}
