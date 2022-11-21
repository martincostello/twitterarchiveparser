// Copyright (c) Martin Costello, 2016. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;

namespace MartinCostello.TwitterArchiveParser;

/// <summary>
/// A console application that parses the JSON of Twitter archives. This class cannot be inherited.
/// </summary>
internal sealed class Program
{
    private static readonly char[] Punctuation = new[] { '.', ',', '!', '?', '£', '$', '\"', '\'', '*', ';', ':', '(', ')', '[', ']', '&', '-', '\n', '\u2019', '“', '”', '_', '*' };

    private static readonly string[] Separators = new[] { " ", "...", "\n" };

    /// <summary>
    /// Gets or sets the path to the tweet archive directory.
    /// </summary>
    [Option(
        CommandOptionType.SingleValue,
        Description = "The path to the archive directory.",
        LongName = "archive-path",
        ShortName = "a",
        ShowInHelpText = true)]
    public string? ArchivePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to count the geotagged tweets.
    /// </summary>
    [Option(
        CommandOptionType.NoValue,
        Description = "Whether to count the number of geotagged tweets.",
        LongName = "count-geotagged",
        ShortName = "g",
        ShowInHelpText = true)]
    public bool CountGeotagged { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to count the number of likes.
    /// </summary>
    [Option(
        CommandOptionType.NoValue,
        Description = "Whether to count the number of likes.",
        LongName = "count-likes",
        ShortName = "l",
        ShowInHelpText = true)]
    public bool CountLikes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to count the tweets with media.
    /// </summary>
    [Option(
        CommandOptionType.NoValue,
        Description = "Whether to count the number of tweets with media.",
        LongName = "count-media",
        ShortName = "m",
        ShowInHelpText = true)]
    public bool CountMedia { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to count the number of retweets.
    /// </summary>
    [Option(
        CommandOptionType.NoValue,
        Description = "Whether to count the number of retweets.",
        LongName = "count-retweets",
        ShortName = "r",
        ShowInHelpText = true)]
    public bool CountRetweets { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show the first tweet.
    /// </summary>
    [Option(
        CommandOptionType.NoValue,
        Description = "Whether to show the first tweet.",
        LongName = "first-tweet",
        ShortName = "f",
        ShowInHelpText = true)]
    public bool FirstTweet { get; set; }

    /// <summary>
    /// Gets or sets whether to show the top N most used hashtags.
    /// </summary>
    [Option(
        CommandOptionType.SingleValue,
        Description = "Whether to show the top most-used hashtags.",
        LongName = "top-hashtags",
        ShortName = "th",
        ShowInHelpText = true)]
    public int? TopHashtags { get; set; }

    /// <summary>
    /// Gets or sets whether to show the top N most used mentions.
    /// </summary>
    [Option(
        CommandOptionType.SingleValue,
        Description = "Whether to show the top most-mentioned accounts.",
        LongName = "top-mentions",
        ShortName = "tm",
        ShowInHelpText = true)]
    public int? TopMentions { get; set; }

    /// <summary>
    /// Gets or sets whether to show the top N most used words.
    /// </summary>
    [Option(
        CommandOptionType.SingleValue,
        Description = "Whether to show the top most-used words.",
        LongName = "top-words",
        ShortName = "tw",
        ShowInHelpText = true)]
    public int? TopWords { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show statistics about a word.
    /// </summary>
    [Option(
        CommandOptionType.SingleValue,
        Description = "Whether to show statistics about the use of a word.",
        LongName = "word-stats",
        ShortName = "wf",
        ShowInHelpText = true)]
    public string? WordStats { get; set; }

    /// <summary>
    /// The main entry point to the application.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous execution of the application.
    /// </returns>
    internal static async Task<int> Main(string[] args)
        => await CommandLineApplication.ExecuteAsync<Program>(args);

    /// <summary>
    /// Executes the program as an asynchronous operation.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="cancellationToken">The optional cancellation token to use.</param>
    /// <returns>
    /// The result of running the application.
    /// </returns>
    internal async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken = default)
    {
        if (Environment.GetEnvironmentVariable("WT_SESSION") != null)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }

        if (string.IsNullOrEmpty(ArchivePath))
        {
            app.ShowHelp();
            return 0;
        }

        using var tweets = await ReadTweetsFromArchiveAsync(ArchivePath, cancellationToken);

        if (FirstTweet)
        {
            ShowFirstTweet(tweets);
        }

        if (CountGeotagged)
        {
            ComputeGeotagged(tweets);
        }

        if (CountLikes)
        {
            ComputeLikes(tweets);
        }

        if (CountRetweets)
        {
            ComputeRetweets(tweets);
        }

        if (CountMedia)
        {
            ComputeMedia(tweets);
        }

        if (TopHashtags.HasValue)
        {
            ComputeTopHashtags(tweets, TopHashtags.Value);
        }

        if (TopMentions.HasValue)
        {
            ComputeTopMentions(tweets, TopMentions.Value);
        }

        if (TopWords.HasValue)
        {
            ComputeTopWords(tweets, TopWords.Value);
        }

        if (WordStats != null)
        {
            ComputeWordStats(tweets, WordStats);
        }

        return 0;
    }

    /// <summary>
    /// Prints the number of tweets in the specified document that were geo-tagged.
    /// </summary>
    /// <param name="tweets">The tweets to count for geo-tags.</param>
    private static void ComputeGeotagged(JsonDocument tweets)
    {
        int count = 0;

        foreach (var tweet in tweets.GetTweets())
        {
            if (tweet.TryGetProperty("geo", out var geo) &&
                geo.TryGetProperty("coordinates", out var _))
            {
                count++;
            }
        }

        Console.WriteLine($"This Twitter archive contains {count:N0} geo-tagged tweet(s).");
    }

    /// <summary>
    /// Prints the number of likes in the tweets in the specified document.
    /// </summary>
    /// <param name="tweets">The tweets to count the likes in.</param>
    private static void ComputeLikes(JsonDocument tweets)
    {
        int likes = 0;
        int totalLikes = 0;

        foreach (var tweet in tweets.GetTweets())
        {
            if (!tweet.TryGetProperty("favorite_count", out var liked))
            {
                continue;
            }

            int timesLiked = int.Parse(liked.GetString()!, CultureInfo.InvariantCulture);

            if (timesLiked > 0)
            {
                likes++;
                totalLikes += timesLiked;
            }
        }

        Console.WriteLine($"This Twitter archive contains {likes:N0} tweet(s) that were liked for a total of {totalLikes:N0} like(s).");
    }

    /// <summary>
    /// Prints the number of media items embedded in the tweets in the specified document.
    /// </summary>
    /// <param name="tweets">The tweets to count the media in.</param>
    /// <param name="take">The number of media types to show.</param>
    private static void ComputeMedia(JsonDocument tweets, int take = 10)
    {
        var media = new List<JsonElement>();

        foreach (var tweet in tweets.GetTweets())
        {
            if ((tweet.TryGetProperty("extended_entities", out var entities) ||
                    tweet.TryGetProperty("entities", out entities)) &&
                entities.TryGetProperty("media", out var array))
            {
                media.AddRange(array.EnumerateArray());
            }
        }

        Console.WriteLine($"This Twitter archive contains {media.Count:N0} item(s) of media.");
        Console.WriteLine();

        var mediaTypes = media
            .Select((p) => p.GetProperty("type").GetString())
            .GroupBy((p) => p)
            .Select((p) => new { Type = p.Key, Count = p.Count() })
            .OrderByDescending((p) => p.Count)
            .Take(take)
            .ToArray();

        Console.WriteLine($"Top {Math.Min(mediaTypes.Length, take)} media format(s):");
        Console.WriteLine();

        for (int i = 0; i < mediaTypes.Length; i++)
        {
            var mediaType = mediaTypes[i];
            Console.WriteLine($"  {i + 1,2:N0}. {mediaType.Type} ({mediaType.Count:N0})");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints the number of retweets in the tweets in the specified document.
    /// </summary>
    /// <param name="tweets">The tweets to count the retweets in.</param>
    private static void ComputeRetweets(JsonDocument tweets)
    {
        int retweets = 0;
        int totalRetweets = 0;

        foreach (var tweet in tweets.GetTweets())
        {
            if (!tweet.TryGetProperty("retweet_count", out var retweeted))
            {
                continue;
            }

            int timesRetweeted = int.Parse(retweeted.GetString()!, CultureInfo.InvariantCulture);

            if (timesRetweeted > 0)
            {
                retweets++;
                totalRetweets += timesRetweeted;
            }
        }

        Console.WriteLine($"This Twitter archive contains {retweets:N0} tweet(s) that were retweeted for a total of {totalRetweets:N0} retweet(s).");
        Console.WriteLine();
    }

    /// <summary>
    /// Prints up to the top most used hashtags in the specified tweets.
    /// </summary>
    /// <param name="tweets">The tweets to get up to the top N words for.</param>
    /// <param name="take">The number of hashtags to show.</param>
    private static void ComputeTopHashtags(JsonDocument tweets, int take)
    {
        var hashtags = tweets
            .GetTweets()
            .Where((p) => p.TryGetProperty("entities", out var _))
            .Where((p) => p.GetProperty("entities").TryGetProperty("hashtags", out var _))
            .SelectMany((p) => p.GetProperty("entities").GetProperty("hashtags").EnumerateArray())
            .Select((p) => p.GetProperty("text").GetString()!.ToLowerInvariant())
            .GroupBy((p) => p)
            .Select((p) => new { Word = p.Key, Count = p.Count() })
            .OrderByDescending((p) => p.Count)
            .ThenBy((p) => p.Word)
            .Take(take)
            .ToArray();

        Console.WriteLine($"Top {Math.Min(hashtags.Length, take)} hashtag(s):");
        Console.WriteLine();

        for (int i = 0; i < hashtags.Length; i++)
        {
            var hashtag = hashtags[i];
            Console.WriteLine($"  {i + 1,2:N0}. {hashtag.Word} ({hashtag.Count:N0})");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints up to the top N most mentioned users in the specified tweets.
    /// </summary>
    /// <param name="tweets">The tweets to get up to the top N mentions for.</param>
    /// <param name="take">The number of mentions to show.</param>
    private static void ComputeTopMentions(JsonDocument tweets, int take)
    {
        var mentions = tweets
            .GetTweets()
            .Where((p) => p.TryGetProperty("entities", out var _))
            .Where((p) => p.GetProperty("entities").TryGetProperty("user_mentions", out var _))
            .SelectMany((p) => p.GetProperty("entities").GetProperty("user_mentions").EnumerateArray())
            .Select((p) => p.GetProperty("screen_name").GetString()!.ToLowerInvariant())
            .GroupBy((p) => p)
            .Select((p) => new { Word = p.Key, Count = p.Count() })
            .OrderByDescending((p) => p.Count)
            .ThenBy((p) => p.Word)
            .Take(take)
            .ToArray();

        Console.WriteLine($"Top {Math.Min(mentions.Length, take)} mention(s):");
        Console.WriteLine();

        for (int i = 0; i < mentions.Length; i++)
        {
            var mention = mentions[i];
            Console.WriteLine($"  {i + 1,2:N0}. {mention.Word} ({mention.Count:N0})");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints up to the top most used words in the specified tweets.
    /// </summary>
    /// <param name="tweets">The tweets to get up to the top N words for.</param>
    /// <param name="take">The number of words to show.</param>
    private static void ComputeTopWords(JsonDocument tweets, int take)
    {
        var words = GetNormalizedWords(tweets)
            .GroupBy((p) => p)
            .Select((p) => new { Word = p.Key, Count = p.Count() })
            .OrderByDescending((p) => p.Count)
            .ThenBy((p) => p.Word)
            .Take(take)
            .ToArray();

        Console.WriteLine($"Top {Math.Min(words.Length, take)} words:");
        Console.WriteLine();

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            Console.WriteLine($"  {i + 1,2:N0}. {word.Word} ({word.Count:N0})");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints statistics about the specified word in the specified tweets.
    /// </summary>
    /// <param name="tweets">The tweets to search the word for.</param>
    /// <param name="word">The word to find.</param>
    private static void ComputeWordStats(JsonDocument tweets, string word)
    {
        var wordTweets = new List<JsonElement>();

        foreach (var tweet in tweets.GetTweets())
        {
            string text = tweet.GetTweetText().ToLowerInvariant();

            var words = text
                .Split(Separators, StringSplitOptions.None)
                .Select((p) => p.Trim(Punctuation));

            if (words.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                wordTweets.Add(tweet);
            }
        }

        wordTweets = wordTweets
            .OrderBy((p) => p.GetCreatedDate())
            .ToList();

        Console.WriteLine($"You have tweeted about \"{word}\" {wordTweets.Count:N0} time(s).");
        Console.WriteLine();

        if (wordTweets.Count > 0)
        {
            var tweet = wordTweets[0];
            string tweetText = tweet.GetTweetText();

            Console.WriteLine(
    $@"  First tweet at: {tweet.GetCreatedDate()}
Text: ""{tweetText}"" ({tweetText.Length} characters)
");
            Console.WriteLine();

            if (wordTweets.Count > 1)
            {
                tweet = wordTweets[^1];
                tweetText = tweet.GetTweetText();

                Console.WriteLine(
        $@"  Latest tweet at: {tweet.GetCreatedDate()}
Text: ""{tweetText}"" ({tweetText.Length} characters)
");
                Console.WriteLine();
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Loads a list of common English words.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing common English words.
    /// </returns>
    private static IList<string> LoadCommonEnglishWords()
    {
        var type = typeof(Program);
        using var stream = type.Assembly.GetManifestResourceStream($"{type.Namespace}.common-english-words.txt");
        using var reader = new StreamReader(stream!);

        var words = new List<string>();

        while (!reader.EndOfStream)
        {
            string? value = reader.ReadLine();

            if (value != null)
            {
                words.Add(value);
            }
        }

        return words;
    }

    /// <summary>
    /// Shows the user's first tweet.
    /// </summary>
    /// <param name="tweets">The tweets to get the first tweet from.</param>
    private static void ShowFirstTweet(JsonDocument tweets)
    {
        var tweet = tweets
            .GetTweets()
            .OrderBy((p) => p.GetCreatedDate())
            .First();

        string text = tweet.GetTweetText();

        Console.WriteLine("First tweet:");
        Console.WriteLine();

        Console.WriteLine(
            $@"  Posted at: {tweet.GetCreatedDate()}
Text: ""{text}"" ({text.Length} characters)
");
    }

    /// <summary>
    /// Reads the tweet data from the specified Twitter archive directory.
    /// </summary>
    /// <param name="archivePath">The path to the directory containing the tweet archive.</param>
    /// <param name="cancellationToken">The optional cancellation token to use.</param>
    /// <returns>
    /// A <see cref="IEnumerable{T}"/> containing the tweets read from the specified Twitter archive.
    /// </returns>
    private static async Task<JsonDocument> ReadTweetsFromArchiveAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        archivePath = Path.GetFullPath(archivePath);
        string tweetsPath = Path.Combine(archivePath, "tweets.js");

        using var stream = File.OpenRead(tweetsPath);

        stream.Seek("window.YTD.tweet.part0 = ".Length, SeekOrigin.Begin);

        var options = new JsonDocumentOptions()
        {
            AllowTrailingCommas = true,
        };

        return await JsonDocument.ParseAsync(stream, options, cancellationToken);
    }

    private static IEnumerable<string> GetNormalizedWords(JsonDocument tweets)
    {
        var commonWords = LoadCommonEnglishWords();
        commonWords.Add("RT");

        return tweets
            .GetTweets()
            .Select((p) => p.GetTweetText())
            .SelectMany((p) => p.Split(Separators, StringSplitOptions.None))
            .Select((p) => p.Trim(Punctuation))
            .Where((p) => !string.IsNullOrWhiteSpace(p))
            .Select((p) => p.Replace('\u2019', '\'')) // Smart Apostrophe
            .Select((p) => p.Replace('“', '\"'))
            .Select((p) => p.Replace('”', '\"'))
            .Where((p) => !p.StartsWith("#", StringComparison.Ordinal))
            .Where((p) => !p.StartsWith("@", StringComparison.Ordinal))
            .Select((p) => p.ToLowerInvariant())
            .Where((p) => !p.StartsWith("http://", StringComparison.Ordinal))
            .Where((p) => !p.StartsWith("https://", StringComparison.Ordinal))
            .Where((p) => char.IsLetter(p[0]) || IsSingleEmoji(p))
            .Where((p) => !commonWords.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsSingleEmoji(string value)
    {
        if (value.Length != 2)
        {
            // Multiple emoji, not an emoji, or a "special" emoji like 🏳️‍🌈
            return false;
        }

        // Thanks to https://schneids.net/emojis-and-string-length
        return StringInfo.ParseCombiningCharacters(value).Length == 1;
    }
}
