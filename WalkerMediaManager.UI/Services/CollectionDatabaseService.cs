using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class CollectionDatabaseService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<CollectionDefinition>? _cache;

    public async Task<IReadOnlyList<CollectionDefinition>> GetDefinitionsAsync(bool forceReload = false)
    {
        if (!forceReload && _cache is not null)
        {
            return _cache;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (!forceReload && _cache is not null)
            {
                return _cache;
            }

            string folder = ResolveCollectionFolder();
            if (!Directory.Exists(folder))
            {
                DiagnosticsService.Log($"Collection database folder not found: {folder}");
                _cache = [];
                return _cache;
            }

            List<CollectionDefinition> definitions = [];
            foreach (string file in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(file);
                    CollectionDefinition? definition = JsonSerializer.Deserialize<CollectionDefinition>(json, JsonOptions);
                    if (definition is null)
                    {
                        DiagnosticsService.Log($"Collection definition was empty: {file}");
                        continue;
                    }

                    NormalizeAndValidate(definition, file);
                    definitions.Add(definition);
                }
                catch (Exception exception)
                {
                    DiagnosticsService.LogException($"Unable to load collection definition '{file}'.", exception);
                }
            }

            _cache = definitions
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DiagnosticsService.Log($"Loaded {_cache.Count} collection definitions from {folder}.");
            return _cache;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static string ResolveCollectionFolder()
    {
        string deployed = Path.Combine(AppContext.BaseDirectory, "Data", "Collections");
        if (Directory.Exists(deployed))
        {
            return deployed;
        }

        string development = Path.Combine(Environment.CurrentDirectory, "Data", "Collections");
        return development;
    }

    private static void NormalizeAndValidate(CollectionDefinition definition, string sourceFile)
    {
        definition.Id = definition.Id.Trim();
        definition.Name = definition.Name.Trim();
        definition.Description = definition.Description.Trim();
        definition.Category = definition.Category.Trim();
        definition.Studio = definition.Studio.Trim();
        definition.Type = string.IsNullOrWhiteSpace(definition.Type) ? "Franchise" : definition.Type.Trim();
        definition.Aliases = definition.Aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Select(alias => alias.Trim()).ToList();
        definition.Movies = definition.Movies.Where(movie => !string.IsNullOrWhiteSpace(movie.Title)).ToList();

        if (definition.Id.Length == 0 || definition.Name.Length == 0 || definition.Movies.Count == 0)
        {
            throw new InvalidDataException($"Collection definition '{sourceFile}' must include id, name, and at least one movie.");
        }

        foreach (CollectionMovieDefinition movie in definition.Movies)
        {
            movie.Title = movie.Title.Trim();
            movie.ImdbId = movie.ImdbId.Trim();
            movie.Aliases = movie.Aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)).Select(alias => alias.Trim()).ToList();
        }
    }
}
