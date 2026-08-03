using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Services;

public sealed class CollectionSeriesService
{
    private readonly MovieRepository _movieRepository = new();
    private readonly CollectionDatabaseService _collectionDatabaseService = new();

    public async Task<List<CollectionSeriesProgress>> GetProgressAsync()
    {
        List<Movie> movies = await _movieRepository.GetAllAsync();
        IReadOnlyList<CollectionDefinition> definitions = await _collectionDatabaseService.GetDefinitionsAsync();
        List<CollectionSeriesProgress> results = [];

        foreach (CollectionDefinition definition in definitions)
        {
            List<CollectionSeriesTitleStatus> statuses = [];

            foreach (CollectionMovieDefinition expectedTitle in definition.Movies)
            {
                Movie? ownedMovie = FindOwnedMovie(movies, expectedTitle);

                statuses.Add(new CollectionSeriesTitleStatus
                {
                    Title = expectedTitle.Title,
                    Year = expectedTitle.Year,
                    IsOwned = ownedMovie is not null,
                    MovieId = ownedMovie?.Id,
                    OwnedFormat = ownedMovie?.Format ?? string.Empty,
                    Runtime = ownedMovie?.Runtime ?? 0,
                    Rating = ownedMovie?.Rating ?? string.Empty,
                    PosterPath = ownedMovie?.PosterPath ?? string.Empty,
                    PlexRatingKey = ownedMovie?.PlexRatingKey ?? string.Empty
                });
            }

            results.Add(new CollectionSeriesProgress
            {
                Name = definition.Name,
                Description = definition.Description,
                Category = definition.Category,
                Studio = definition.Studio,
                CollectionType = definition.Type,
                Titles = statuses
            });
        }

        return results
            .OrderByDescending(result => result.CompletionPercent)
            .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<IReadOnlyList<CollectionDefinition>> GetDefinitionsAsync(bool forceReload = false) =>
        _collectionDatabaseService.GetDefinitionsAsync(forceReload);

    private static Movie? FindOwnedMovie(
        IEnumerable<Movie> movies,
        CollectionMovieDefinition expectedTitle)
    {
        IReadOnlySet<string> acceptedTitles = CollectionTitleNormalizer.BuildAcceptedKeys(
            expectedTitle.Title,
            expectedTitle.Aliases);

        return movies.FirstOrDefault(movie =>
            movie.Owned &&
            acceptedTitles.Contains(CollectionTitleNormalizer.Normalize(movie.Title)) &&
            (expectedTitle.Year <= 0 || movie.ReleaseYear <= 0 || movie.ReleaseYear == expectedTitle.Year));
    }
}
