using System;
using System.Collections.Generic;
using System.Linq;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class WatchOrderService
{
    private static readonly IReadOnlyList<WatchOrderDefinition> Definitions = BuildDefinitions();

    public IReadOnlyList<WatchOrderDefinition> GetAllOrders() => Definitions;

    public IReadOnlyList<string> GetSupportedCollections() => Definitions
        .Select(order => order.CollectionName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<WatchOrderDefinition> GetOrders(string collectionName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            return [];
        }

        string normalizedRequestedName = NormalizeCollectionName(collectionName);

        return Definitions
            .Where(order =>
                NormalizeCollectionName(order.CollectionName) == normalizedRequestedName ||
                CollectionNamesAreCompatible(order.CollectionName, collectionName))
            .ToList();
    }

    public WatchOrderDefinition? GetOrder(string collectionName, string orderName)
    {
        if (string.IsNullOrWhiteSpace(orderName))
        {
            return GetOrders(collectionName).FirstOrDefault();
        }

        return GetOrders(collectionName).FirstOrDefault(order =>
            string.Equals(order.Name, orderName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CollectionNamesAreCompatible(string definedName, string requestedName)
    {
        string defined = NormalizeCollectionName(definedName);
        string requested = NormalizeCollectionName(requestedName);

        return (defined.Contains("starwars") && requested.Contains("starwars")) ||
               (defined.Contains("marvelcinematicuniverse") &&
                    (requested.Contains("marvelcinematicuniverse") || requested == "mcu")) ||
               (defined.Contains("harrypotter") && requested.Contains("harrypotter")) ||
               (defined.Contains("lordoftherings") && requested.Contains("lordoftherings")) ||
               (defined.Contains("indianajones") && requested.Contains("indianajones")) ||
               (defined.Contains("missionimpossible") && requested.Contains("missionimpossible")) ||
               (defined.Contains("jurassic") && requested.Contains("jurassic")) ||
               (defined.Contains("matrix") && requested.Contains("matrix")) ||
               (defined.Contains("rocky") && requested.Contains("rocky")) ||
               (defined.Contains("backtothefuture") && requested.Contains("backtothefuture"));
    }

    private static string NormalizeCollectionName(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static IReadOnlyList<WatchOrderDefinition> BuildDefinitions() =>
    [
        Order(
            "Star Wars - Theatrical Movies",
            "Release Order",
            "The theatrical movies in their original release sequence.",
            E("Star Wars", 1977, "Star Wars: Episode IV - A New Hope", "A New Hope"),
            E("The Empire Strikes Back", 1980, "Star Wars: Episode V - The Empire Strikes Back"),
            E("Return of the Jedi", 1983, "Star Wars: Episode VI - Return of the Jedi"),
            E("Star Wars: Episode I - The Phantom Menace", 1999, "The Phantom Menace"),
            E("Star Wars: Episode II - Attack of the Clones", 2002, "Attack of the Clones"),
            E("Star Wars: Episode III - Revenge of the Sith", 2005, "Revenge of the Sith"),
            E("Star Wars: Episode VII - The Force Awakens", 2015, "The Force Awakens"),
            E("Rogue One: A Star Wars Story", 2016, "Rogue One"),
            E("Star Wars: Episode VIII - The Last Jedi", 2017, "The Last Jedi"),
            E("Solo: A Star Wars Story", 2018, "Solo"),
            E("Star Wars: Episode IX - The Rise of Skywalker", 2019, "The Rise of Skywalker")),

        Order(
            "Star Wars - Theatrical Movies",
            "Chronological Order",
            "The theatrical story in in-universe chronological order.",
            E("Star Wars: Episode I - The Phantom Menace", 1999, "The Phantom Menace"),
            E("Star Wars: Episode II - Attack of the Clones", 2002, "Attack of the Clones"),
            E("Star Wars: Episode III - Revenge of the Sith", 2005, "Revenge of the Sith"),
            E("Solo: A Star Wars Story", 2018, "Solo"),
            E("Rogue One: A Star Wars Story", 2016, "Rogue One"),
            E("Star Wars", 1977, "Star Wars: Episode IV - A New Hope", "A New Hope"),
            E("The Empire Strikes Back", 1980, "Star Wars: Episode V - The Empire Strikes Back"),
            E("Return of the Jedi", 1983, "Star Wars: Episode VI - Return of the Jedi"),
            E("Star Wars: Episode VII - The Force Awakens", 2015, "The Force Awakens"),
            E("Star Wars: Episode VIII - The Last Jedi", 2017, "The Last Jedi"),
            E("Star Wars: Episode IX - The Rise of Skywalker", 2019, "The Rise of Skywalker")),

        Order(
            "Star Wars - Theatrical Movies",
            "Machete Order",
            "A character-focused viewing order that begins with the original trilogy and uses the prequels as a flashback.",
            E("Star Wars", 1977, "Star Wars: Episode IV - A New Hope", "A New Hope"),
            E("The Empire Strikes Back", 1980, "Star Wars: Episode V - The Empire Strikes Back"),
            E("Star Wars: Episode II - Attack of the Clones", 2002, "Attack of the Clones"),
            E("Star Wars: Episode III - Revenge of the Sith", 2005, "Revenge of the Sith"),
            E("Return of the Jedi", 1983, "Star Wars: Episode VI - Return of the Jedi"),
            E("Star Wars: Episode VII - The Force Awakens", 2015, "The Force Awakens"),
            E("Star Wars: Episode VIII - The Last Jedi", 2017, "The Last Jedi"),
            E("Star Wars: Episode IX - The Rise of Skywalker", 2019, "The Rise of Skywalker")),

        Order(
            "Marvel Cinematic Universe",
            "Release Order",
            "The MCU feature films in theatrical release order through the end of 2025.",
            E("Iron Man", 2008), E("The Incredible Hulk", 2008), E("Iron Man 2", 2010),
            E("Thor", 2011), E("Captain America: The First Avenger", 2011), E("The Avengers", 2012, "Marvel's The Avengers"),
            E("Iron Man 3", 2013), E("Thor: The Dark World", 2013), E("Captain America: The Winter Soldier", 2014),
            E("Guardians of the Galaxy", 2014), E("Avengers: Age of Ultron", 2015), E("Ant-Man", 2015),
            E("Captain America: Civil War", 2016), E("Doctor Strange", 2016), E("Guardians of the Galaxy Vol. 2", 2017),
            E("Spider-Man: Homecoming", 2017), E("Thor: Ragnarok", 2017), E("Black Panther", 2018),
            E("Avengers: Infinity War", 2018), E("Ant-Man and the Wasp", 2018), E("Captain Marvel", 2019),
            E("Avengers: Endgame", 2019), E("Spider-Man: Far From Home", 2019), E("Black Widow", 2021),
            E("Shang-Chi and the Legend of the Ten Rings", 2021), E("Eternals", 2021), E("Spider-Man: No Way Home", 2021),
            E("Doctor Strange in the Multiverse of Madness", 2022), E("Thor: Love and Thunder", 2022), E("Black Panther: Wakanda Forever", 2022),
            E("Ant-Man and the Wasp: Quantumania", 2023), E("Guardians of the Galaxy Vol. 3", 2023), E("The Marvels", 2023),
            E("Deadpool & Wolverine", 2024, "Deadpool and Wolverine"), E("Captain America: Brave New World", 2025),
            E("Thunderbolts*", 2025, "Thunderbolts"), E("The Fantastic Four: First Steps", 2025)),

        Order(
            "Marvel Cinematic Universe",
            "Timeline Order",
            "A practical MCU feature-film timeline order. Some post-credit scenes occur later than the main story.",
            E("Captain America: The First Avenger", 2011), E("Captain Marvel", 2019), E("Iron Man", 2008),
            E("Iron Man 2", 2010), E("The Incredible Hulk", 2008), E("Thor", 2011),
            E("The Avengers", 2012, "Marvel's The Avengers"), E("Iron Man 3", 2013), E("Thor: The Dark World", 2013),
            E("Captain America: The Winter Soldier", 2014), E("Guardians of the Galaxy", 2014), E("Guardians of the Galaxy Vol. 2", 2017),
            E("Avengers: Age of Ultron", 2015), E("Ant-Man", 2015), E("Captain America: Civil War", 2016),
            E("Black Widow", 2021), E("Black Panther", 2018), E("Spider-Man: Homecoming", 2017),
            E("Doctor Strange", 2016), E("Thor: Ragnarok", 2017), E("Ant-Man and the Wasp", 2018),
            E("Avengers: Infinity War", 2018), E("Avengers: Endgame", 2019), E("Shang-Chi and the Legend of the Ten Rings", 2021),
            E("Spider-Man: Far From Home", 2019), E("Eternals", 2021), E("Spider-Man: No Way Home", 2021),
            E("Doctor Strange in the Multiverse of Madness", 2022), E("Thor: Love and Thunder", 2022), E("Black Panther: Wakanda Forever", 2022),
            E("Ant-Man and the Wasp: Quantumania", 2023), E("Guardians of the Galaxy Vol. 3", 2023), E("The Marvels", 2023),
            E("Deadpool & Wolverine", 2024, "Deadpool and Wolverine"), E("Captain America: Brave New World", 2025),
            E("Thunderbolts*", 2025, "Thunderbolts"), E("The Fantastic Four: First Steps", 2025)),

        Order("Harry Potter", "Release Order", "The eight Harry Potter films in release order.",
            E("Harry Potter and the Sorcerer's Stone", 2001, "Harry Potter and the Philosopher's Stone"),
            E("Harry Potter and the Chamber of Secrets", 2002), E("Harry Potter and the Prisoner of Azkaban", 2004),
            E("Harry Potter and the Goblet of Fire", 2005), E("Harry Potter and the Order of the Phoenix", 2007),
            E("Harry Potter and the Half-Blood Prince", 2009), E("Harry Potter and the Deathly Hallows: Part 1", 2010),
            E("Harry Potter and the Deathly Hallows: Part 2", 2011)),

        Order("The Lord of the Rings and The Hobbit", "Release Order", "The six Peter Jackson Middle-earth films in theatrical release order.",
            E("The Lord of the Rings: The Fellowship of the Ring", 2001), E("The Lord of the Rings: The Two Towers", 2002),
            E("The Lord of the Rings: The Return of the King", 2003), E("The Hobbit: An Unexpected Journey", 2012),
            E("The Hobbit: The Desolation of Smaug", 2013), E("The Hobbit: The Battle of the Five Armies", 2014)),

        Order("The Lord of the Rings and The Hobbit", "Story Order", "The Hobbit trilogy followed by The Lord of the Rings trilogy.",
            E("The Hobbit: An Unexpected Journey", 2012), E("The Hobbit: The Desolation of Smaug", 2013),
            E("The Hobbit: The Battle of the Five Armies", 2014), E("The Lord of the Rings: The Fellowship of the Ring", 2001),
            E("The Lord of the Rings: The Two Towers", 2002), E("The Lord of the Rings: The Return of the King", 2003)),

        Order("Indiana Jones", "Release Order", "The Indiana Jones films in theatrical release order.",
            E("Raiders of the Lost Ark", 1981, "Indiana Jones and the Raiders of the Lost Ark"),
            E("Indiana Jones and the Temple of Doom", 1984), E("Indiana Jones and the Last Crusade", 1989),
            E("Indiana Jones and the Kingdom of the Crystal Skull", 2008), E("Indiana Jones and the Dial of Destiny", 2023)),

        Order("Indiana Jones", "Chronological Order", "The Indiana Jones films in story chronology.",
            E("Indiana Jones and the Temple of Doom", 1984), E("Raiders of the Lost Ark", 1981, "Indiana Jones and the Raiders of the Lost Ark"),
            E("Indiana Jones and the Last Crusade", 1989), E("Indiana Jones and the Kingdom of the Crystal Skull", 2008),
            E("Indiana Jones and the Dial of Destiny", 2023)),

        Order("Mission: Impossible", "Release Order", "The Mission: Impossible films in release order.",
            E("Mission: Impossible", 1996), E("Mission: Impossible 2", 2000, "Mission: Impossible II", "Mission Impossible II"),
            E("Mission: Impossible III", 2006, "Mission Impossible 3"), E("Mission: Impossible - Ghost Protocol", 2011),
            E("Mission: Impossible - Rogue Nation", 2015), E("Mission: Impossible - Fallout", 2018),
            E("Mission: Impossible - Dead Reckoning Part One", 2023, "Mission: Impossible - Dead Reckoning"),
            E("Mission: Impossible - The Final Reckoning", 2025)),

        Order("Jurassic Park and Jurassic World", "Release Order", "The Jurassic films in theatrical release order.",
            E("Jurassic Park", 1993), E("The Lost World: Jurassic Park", 1997), E("Jurassic Park III", 2001, "Jurassic Park 3"),
            E("Jurassic World", 2015), E("Jurassic World: Fallen Kingdom", 2018), E("Jurassic World Dominion", 2022, "Jurassic World: Dominion"),
            E("Jurassic World Rebirth", 2025, "Jurassic World: Rebirth")),

        Order("The Matrix", "Release Order", "The Matrix films in release order.",
            E("The Matrix", 1999), E("The Matrix Reloaded", 2003), E("The Matrix Revolutions", 2003), E("The Matrix Resurrections", 2021)),

        Order("Rocky and Creed", "Release Order", "The Rocky saga and Creed continuation films in release order.",
            E("Rocky", 1976), E("Rocky II", 1979, "Rocky 2"), E("Rocky III", 1982, "Rocky 3"),
            E("Rocky IV", 1985, "Rocky 4"), E("Rocky V", 1990, "Rocky 5"), E("Rocky Balboa", 2006),
            E("Creed", 2015), E("Creed II", 2018, "Creed 2"), E("Creed III", 2023, "Creed 3")),

        Order("Back to the Future", "Release Order", "The Back to the Future trilogy in release order.",
            E("Back to the Future", 1985), E("Back to the Future Part II", 1989, "Back to the Future 2"),
            E("Back to the Future Part III", 1990, "Back to the Future 3"))
    ];

    private static WatchOrderDefinition Order(
        string collectionName,
        string name,
        string description,
        params WatchOrderEntry[] entries)
    {
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index].Position = index + 1;
        }

        return new WatchOrderDefinition
        {
            CollectionName = collectionName,
            Name = name,
            Description = description,
            Entries = entries
        };
    }

    private static WatchOrderEntry E(string title, int year, params string[] aliases) =>
        new()
        {
            Title = title,
            Year = year,
            Aliases = aliases
        };
}
