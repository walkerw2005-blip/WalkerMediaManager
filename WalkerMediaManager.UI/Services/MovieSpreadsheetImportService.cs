using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class MovieSpreadsheetImportService
{
    public async Task<MovieImportResult> ImportAsync(
        string suppliedFilePath)
    {
        string filePath = CleanFilePath(suppliedFilePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Enter the path to the movie spreadsheet.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "The selected spreadsheet could not be found.",
                filePath);
        }

        string extension = Path.GetExtension(filePath);

        if (!extension.Equals(
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The importer currently supports .xlsx files.");
        }

        MovieImportResult result = new();

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        HashSet<string> existingMovies =
            await LoadExistingMovieKeysAsync(connection);

        using XLWorkbook workbook = new(filePath);

        IXLWorksheet worksheet = workbook.Worksheet(1);

        IXLRange? usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            throw new InvalidOperationException(
                "The spreadsheet does not contain any data.");
        }

        int headerRowNumber =
            usedRange.FirstRow().RowNumber();

        int lastRowNumber =
            usedRange.LastRow().RowNumber();

        Dictionary<string, int> columns =
            ReadColumnMap(
                worksheet,
                headerRowNumber,
                usedRange.FirstColumn().ColumnNumber(),
                usedRange.LastColumn().ColumnNumber());

        int titleColumn =
            FindRequiredColumn(
                columns,
                "Title",
                "Movie Title",
                "Name");

        int? yearColumn =
            FindOptionalColumn(
                columns,
                "Year",
                "Release Year",
                "ReleaseYear");

        int? ratingColumn =
            FindOptionalColumn(
                columns,
                "Rating",
                "MPAA Rating",
                "MPAARating");

        int? genreColumn =
            FindOptionalColumn(
                columns,
                "Category",
                "Genre",
                "Primary Category");

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            for (
                int rowNumber = headerRowNumber + 1;
                rowNumber <= lastRowNumber;
                rowNumber++)
            {
                IXLRow row = worksheet.Row(rowNumber);

                string title =
                    row.Cell(titleColumn)
                        .GetFormattedString()
                        .Trim();

                if (string.IsNullOrWhiteSpace(title))
                {
                    result.SkippedCount++;
                    continue;
                }

                int releaseYear =
                    yearColumn.HasValue
                        ? ReadYear(row.Cell(yearColumn.Value))
                        : 0;

                string rating =
                    ratingColumn.HasValue
                        ? row.Cell(ratingColumn.Value)
                            .GetFormattedString()
                            .Trim()
                        : string.Empty;

                string genre =
                    genreColumn.HasValue
                        ? row.Cell(genreColumn.Value)
                            .GetFormattedString()
                            .Trim()
                        : string.Empty;

                string movieKey =
                    CreateMovieKey(title, releaseYear);

                if (existingMovies.Contains(movieKey))
                {
                    result.DuplicateCount++;
                    continue;
                }

                try
                {
                    await InsertMovieAsync(
                        connection,
                        transaction,
                        title,
                        releaseYear,
                        rating,
                        genre);

                    existingMovies.Add(movieKey);
                    result.ImportedCount++;
                }
                catch
                {
                    result.FailedCount++;
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return result;
    }

    private static async Task<HashSet<string>>
        LoadExistingMovieKeysAsync(
            SqliteConnection connection)
    {
        HashSet<string> keys =
            new(StringComparer.OrdinalIgnoreCase);

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Title,
                ReleaseYear
            FROM Movies;
            """;

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string title =
                reader.IsDBNull(0)
                    ? string.Empty
                    : reader.GetString(0);

            int releaseYear =
                reader.IsDBNull(1)
                    ? 0
                    : reader.GetInt32(1);

            keys.Add(
                CreateMovieKey(
                    title,
                    releaseYear));
        }

        return keys;
    }

    private static async Task InsertMovieAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string title,
        int releaseYear,
        string rating,
        string genre)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
            INSERT INTO Movies
            (
                Title,
                ReleaseYear,
                Rating,
                Runtime,
                Genre,
                Director,
                PlexGuid,
                TMDbId,
                IMDbId
            )
            VALUES
            (
                $title,
                $releaseYear,
                $rating,
                0,
                $genre,
                '',
                '',
                NULL,
                ''
            );
            """;

        command.Parameters.AddWithValue(
            "$title",
            title);

        command.Parameters.AddWithValue(
            "$releaseYear",
            releaseYear);

        command.Parameters.AddWithValue(
            "$rating",
            rating);

        command.Parameters.AddWithValue(
            "$genre",
            genre);

        await command.ExecuteNonQueryAsync();
    }

    private static Dictionary<string, int> ReadColumnMap(
        IXLWorksheet worksheet,
        int headerRow,
        int firstColumn,
        int lastColumn)
    {
        Dictionary<string, int> columns =
            new(StringComparer.OrdinalIgnoreCase);

        for (
            int columnNumber = firstColumn;
            columnNumber <= lastColumn;
            columnNumber++)
        {
            string header =
                worksheet
                    .Cell(headerRow, columnNumber)
                    .GetFormattedString()
                    .Trim();

            if (!string.IsNullOrWhiteSpace(header) &&
                !columns.ContainsKey(header))
            {
                columns.Add(
                    header,
                    columnNumber);
            }
        }

        return columns;
    }

    private static int FindRequiredColumn(
        Dictionary<string, int> columns,
        params string[] possibleNames)
    {
        int? column =
            FindOptionalColumn(
                columns,
                possibleNames);

        if (column.HasValue)
        {
            return column.Value;
        }

        throw new InvalidOperationException(
            $"The spreadsheet must contain a " +
            $"'{possibleNames[0]}' column.");
    }

    private static int? FindOptionalColumn(
        Dictionary<string, int> columns,
        params string[] possibleNames)
    {
        foreach (string possibleName in possibleNames)
        {
            if (columns.TryGetValue(
                    possibleName,
                    out int columnNumber))
            {
                return columnNumber;
            }
        }

        return null;
    }

    private static int ReadYear(
        IXLCell cell)
    {
        string formattedValue =
            cell.GetFormattedString().Trim();

        if (int.TryParse(
                formattedValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int year))
        {
            return year;
        }

        if (cell.TryGetValue(
                out double numericValue))
        {
            return Convert.ToInt32(numericValue);
        }

        return 0;
    }

    private static string CreateMovieKey(
        string title,
        int releaseYear)
    {
        return
            $"{title.Trim().ToUpperInvariant()}|" +
            $"{releaseYear}";
    }

    private static string CleanFilePath(
        string suppliedFilePath)
    {
        return suppliedFilePath
            .Trim()
            .Trim('"');
    }
}